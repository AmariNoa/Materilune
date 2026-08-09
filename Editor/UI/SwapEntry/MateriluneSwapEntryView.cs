using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides a single material replacement entry editor.
    /// </summary>
    public class MateriluneSwapEntryView : VisualElement
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/SwapEntry/MateriluneSwapEntryView.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/SwapEntry/MateriluneSwapEntryView.uss";
        private const string RowClass = "materilune-swap-entry";

        /// <summary>
        /// Marks a row whose source material is no longer offered by the component that
        /// holds it. Public so a host can style the state to match its own window.
        /// </summary>
        public const string OrphanedClass = "materilune-swap-entry--orphaned";

        private VisualElement m_row;
        private ObjectField m_fromField;
        private ObjectField m_toField;
        private Button m_toPrevious;
        private Button m_toNext;
        private SerializedProperty m_swapEntryProperty;
        private MateriluneCandidateMode m_candidateMode;
        private bool m_isBound;
        private bool m_isOrphaned;

        /// <summary>
        /// Creates the UXML factory for this element.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<MateriluneSwapEntryView>
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MateriluneSwapEntryView"/> class.
        /// </summary>
        public MateriluneSwapEntryView()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.swap_entry.load_error",
                    "Materilune could not load the material swap entry UI assets."));
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_row = this.Q<VisualElement>(className: RowClass);
            m_fromField = this.Q<ObjectField>("from-field");
            m_toField = this.Q<ObjectField>("to-field");
            m_toPrevious = this.Q<Button>("to-previous");
            m_toNext = this.Q<Button>("to-next");
            if (m_fromField == null || m_toField == null || m_toPrevious == null || m_toNext == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.swap_entry.missing_element_error",
                    "Materilune material swap entry UI is missing a required element."));
                Clear();
                return;
            }

            ConfigureFields();
            ConfigureButtons();
            SetControlsEnabled(false);

            // Tooltips are language dependent; refresh them while attached to a panel so a
            // language switch does not require recreating the element.
            RegisterCallback<AttachToPanelEvent>(_ => MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged));
            RegisterCallback<DetachFromPanelEvent>(_ => MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged));
        }

        private void OnLanguageChanged(string languageCode)
        {
            if (!HasControls())
            {
                return;
            }

            ApplyLocalizedTexts();
        }

        /// <summary>
        /// Occurs after an entry value has been changed and applied.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// Binds this view to a material replacement entry.
        /// </summary>
        /// <param name="swapEntryProperty">The material replacement entry property.</param>
        /// <param name="fromCandidates">
        /// Ignored. The replacement source is generated from the target meshes and cannot be
        /// chosen, so no candidate list is needed. The parameter stays for the public contract.
        /// </param>
        /// <param name="candidateMode">The mode used to discover replacement candidates.</param>
        public void Bind(
            SerializedProperty swapEntryProperty,
            IReadOnlyList<Material> fromCandidates,
            MateriluneCandidateMode candidateMode)
        {
            Unbind();
            if (!HasControls() || swapEntryProperty == null)
            {
                return;
            }

            m_swapEntryProperty = swapEntryProperty;
            m_candidateMode = candidateMode;

            var fromProperty = m_swapEntryProperty.FindPropertyRelative("m_from");
            var toProperty = m_swapEntryProperty.FindPropertyRelative("m_to");
            if (fromProperty == null || toProperty == null)
            {
                Unbind();
                return;
            }

            // The fields are driven by hand rather than by a serialized binding. A binding
            // pushes the property value into the field on its own schedule, and the resulting
            // change event is indistinguishable from a user edit, so every rebind would look
            // like an edit and re-enter the host that triggered it. Writing the values here
            // keeps change events limited to actual user interaction.
            m_fromField.SetValueWithoutNotify(fromProperty.objectReferenceValue);
            m_toField.SetValueWithoutNotify(toProperty.objectReferenceValue);
            m_toField.RegisterValueChangedCallback(OnToFieldValueChanged);
            m_isBound = true;
            // The materials the owning component offers double as the test for an orphan:
            // a source that is no longer among them cannot reach the Material Swap, so the
            // row says so rather than looking like every other row.
            m_isOrphaned = IsOrphaned(fromProperty.objectReferenceValue as Material, fromCandidates);
            ApplyOrphanState();
            UpdateControlState();
        }

        /// <summary>
        /// Removes the current property binding and associated state.
        /// </summary>
        public void Unbind()
        {
            if (!HasControls())
            {
                return;
            }

            if (m_isBound)
            {
                m_toField.UnregisterValueChangedCallback(OnToFieldValueChanged);
                m_fromField.SetValueWithoutNotify(null);
                m_toField.SetValueWithoutNotify(null);
            }

            m_swapEntryProperty = null;
            m_candidateMode = MateriluneCandidateMode.None;
            m_isBound = false;
            m_isOrphaned = false;
            ApplyOrphanState();
            SetControlsEnabled(false);
        }

        /// <summary>
        /// Selects the preceding or following replacement candidate.
        /// </summary>
        /// <param name="direction">A negative value selects the preceding candidate; a positive value selects the following candidate.</param>
        internal void StepToCandidate(int direction)
        {
            if (!CanEdit() || direction == 0 || m_candidateMode == MateriluneCandidateMode.None)
            {
                return;
            }

            var serializedObject = m_swapEntryProperty.serializedObject;
            serializedObject.Update();
            var fromProperty = m_swapEntryProperty.FindPropertyRelative("m_from");
            var toProperty = m_swapEntryProperty.FindPropertyRelative("m_to");
            if (fromProperty == null || toProperty == null)
            {
                return;
            }

            var current = toProperty.objectReferenceValue as Material ?? fromProperty.objectReferenceValue as Material;
            var candidates = MateriluneMaterialCandidates.GetCandidates(current, m_candidateMode);
            if (candidates.Count < 2)
            {
                UpdateControlState();
                return;
            }

            var currentIndex = candidates.IndexOf(current);
            if (currentIndex < 0)
            {
                UpdateControlState();
                return;
            }

            var offset = direction < 0 ? -1 : 1;
            var nextIndex = (currentIndex + offset + candidates.Count) % candidates.Count;
            toProperty.objectReferenceValue = candidates[nextIndex];
            ApplyChanges(serializedObject);
        }

        private void ConfigureFields()
        {
            m_fromField.objectType = typeof(Material);
            m_fromField.allowSceneObjects = false;
            m_fromField.label = string.Empty;

            m_toField.objectType = typeof(Material);
            m_toField.allowSceneObjects = false;
            m_toField.label = string.Empty;

            ApplyLocalizedTexts();
        }

        private void ConfigureButtons()
        {
            // Plain glyphs rather than built-in icon USS variables, whose names are not a stable
            // contract across Unity versions.
            m_toPrevious.text = "<";
            m_toNext.text = ">";
            m_toPrevious.clicked += () => StepToCandidate(-1);
            m_toNext.clicked += () => StepToCandidate(1);
        }

        private static bool IsOrphaned(Material from, IReadOnlyList<Material> offeredMaterials)
        {
            // An empty list means the component never recorded what it offers, so there is
            // nothing to judge against. The synchronizer skips the same test in that case.
            if (offeredMaterials == null || offeredMaterials.Count == 0)
            {
                return false;
            }

            foreach (var material in offeredMaterials)
            {
                if (material != null && material == from)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyOrphanState()
        {
            if (m_row != null)
            {
                m_row.EnableInClassList(OrphanedClass, m_isOrphaned);
            }

            if (m_fromField != null)
            {
                m_fromField.tooltip = m_isOrphaned
                    ? MateriluneL10n.Get(
                        "materilune.ui.swap_entry.orphan_tooltip",
                        "This material is no longer used by the target meshes. The replacement is kept but not applied.")
                    : MateriluneL10n.Get(
                        "materilune.ui.swap_entry.from_tooltip",
                        "Material to replace. Generated from the target meshes and shown for reference");
            }
        }

        private void ApplyLocalizedTexts()
        {
            m_toField.tooltip = MateriluneL10n.Get("materilune.ui.swap_entry.to_tooltip", "Replacement material");
            m_toPrevious.tooltip = MateriluneL10n.Get("materilune.ui.swap_entry.previous_tooltip", "Previous candidate");
            m_toNext.tooltip = MateriluneL10n.Get("materilune.ui.swap_entry.next_tooltip", "Next candidate");
            ApplyOrphanState();
        }

        private void OnToFieldValueChanged(ChangeEvent<UnityEngine.Object> changeEvent)
        {
            ApplyFieldValue("m_to", changeEvent.newValue as Material);
        }

        private void ApplyFieldValue(string relativePath, Material material)
        {
            if (!CanEdit())
            {
                return;
            }

            var serializedObject = m_swapEntryProperty.serializedObject;
            serializedObject.Update();
            var property = m_swapEntryProperty.FindPropertyRelative(relativePath);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = material;
            ApplyChanges(serializedObject);
        }

        private void ApplyChanges(SerializedObject serializedObject)
        {
            // Changed reports an actual data change. Reporting a no-op edit would make hosts
            // rebuild for nothing, and a rebuild rebinds this view, which would loop.
            if (!serializedObject.ApplyModifiedProperties())
            {
                UpdateControlState();
                return;
            }

            SyncFieldsFromProperty();
            UpdateControlState();
            Changed?.Invoke();
        }

        private void SyncFieldsFromProperty()
        {
            if (!CanEdit())
            {
                return;
            }

            var fromProperty = m_swapEntryProperty.FindPropertyRelative("m_from");
            var toProperty = m_swapEntryProperty.FindPropertyRelative("m_to");
            if (fromProperty != null)
            {
                m_fromField.SetValueWithoutNotify(fromProperty.objectReferenceValue);
            }

            if (toProperty != null)
            {
                m_toField.SetValueWithoutNotify(toProperty.objectReferenceValue);
            }
        }

        private void UpdateControlState()
        {
            if (!CanEdit())
            {
                SetControlsEnabled(false);
                return;
            }

            // The replacement source comes from the target meshes and is shown for reference
            // only, so it stays disabled even while the rest of the row is editable.
            m_fromField.SetEnabled(false);
            m_toField.SetEnabled(true);

            var current = GetCurrentTo() ?? GetCurrentFrom();
            var canStep = m_candidateMode != MateriluneCandidateMode.None
                && MateriluneMaterialCandidates.GetCandidates(current, m_candidateMode).Count >= 2;
            m_toPrevious.SetEnabled(canStep);
            m_toNext.SetEnabled(canStep);
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (!HasControls())
            {
                return;
            }

            m_fromField.SetEnabled(false);
            m_toField.SetEnabled(enabled);
            m_toPrevious.SetEnabled(enabled);
            m_toNext.SetEnabled(enabled);
        }

        private Material GetCurrentFrom()
        {
            return GetCurrentMaterial("m_from");
        }

        private Material GetCurrentTo()
        {
            return GetCurrentMaterial("m_to");
        }

        private Material GetCurrentMaterial(string relativePath)
        {
            if (!CanEdit())
            {
                return null;
            }

            m_swapEntryProperty.serializedObject.Update();
            var property = m_swapEntryProperty.FindPropertyRelative(relativePath);
            return property == null ? null : property.objectReferenceValue as Material;
        }

        private bool CanEdit()
        {
            // targetObject reports null once the bound component is destroyed; touching the
            // SerializedObject after that would throw from Update().
            return HasControls()
                && m_isBound
                && m_swapEntryProperty != null
                && m_swapEntryProperty.serializedObject != null
                && m_swapEntryProperty.serializedObject.targetObject != null;
        }

        private bool HasControls()
        {
            return m_fromField != null
                && m_toField != null
                && m_toPrevious != null
                && m_toNext != null;
        }
    }
}
