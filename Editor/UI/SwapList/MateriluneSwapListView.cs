using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides a material replacement list editor.
    /// </summary>
    public class MateriluneSwapListView : VisualElement
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/SwapList/MateriluneSwapListView.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/SwapList/MateriluneSwapListView.uss";

        private VisualElement m_entries;
        private Button m_addButton;
        private SerializedProperty m_swapsProperty;
        private IReadOnlyList<Material> m_fromCandidates;
        private MateriluneCandidateMode m_candidateMode;
        private bool m_isBound;
        private readonly List<RowBinding> m_rows = new List<RowBinding>();

        /// <summary>
        /// Creates the UXML factory for this element.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<MateriluneSwapListView>
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MateriluneSwapListView"/> class.
        /// </summary>
        public MateriluneSwapListView()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.swap_list.load_error",
                    "Materilune could not load the material swap list UI assets."));
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_entries = this.Q<VisualElement>("entries");
            m_addButton = this.Q<Button>("add-button");
            if (m_entries == null || m_addButton == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.swap_list.load_error",
                    "Materilune could not load the material swap list UI assets."));
                Clear();
                m_entries = null;
                m_addButton = null;
                return;
            }

            m_addButton.text = "+";
            m_addButton.clicked += AddEntry;
            ApplyLocalizedTexts();
            UpdateControlState();

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<AttachToPanelEvent>(_ => MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged));
            RegisterCallback<DetachFromPanelEvent>(_ => MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged));
        }

        /// <summary>
        /// Occurs after a list or entry value has been changed and applied.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// Binds this view to a material replacement array.
        /// </summary>
        /// <param name="swapsProperty">The serialized m_swaps array property.</param>
        /// <param name="fromCandidates">Materials that can be selected as replacement sources.</param>
        /// <param name="candidateMode">The mode used to discover replacement candidates.</param>
        public void Bind(
            SerializedProperty swapsProperty,
            IReadOnlyList<Material> fromCandidates,
            MateriluneCandidateMode candidateMode)
        {
            Unbind();
            if (m_entries == null
                || m_addButton == null
                || swapsProperty == null
                || !swapsProperty.isArray
                || swapsProperty.serializedObject == null)
            {
                return;
            }

            m_swapsProperty = swapsProperty;
            m_fromCandidates = fromCandidates;
            m_candidateMode = candidateMode;
            m_isBound = true;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            RebuildEntries();
        }

        /// <summary>
        /// Removes the current property binding and all generated rows.
        /// </summary>
        public void Unbind()
        {
            if (m_isBound)
            {
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            }

            ClearRows();
            m_swapsProperty = null;
            m_fromCandidates = null;
            m_candidateMode = MateriluneCandidateMode.None;
            m_isBound = false;
            UpdateControlState();
        }

        /// <summary>
        /// Adds an empty material replacement entry.
        /// </summary>
        internal void AddEntry()
        {
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            var serializedObject = m_swapsProperty.serializedObject;
            serializedObject.Update();
            var newIndex = m_swapsProperty.arraySize;
            m_swapsProperty.arraySize = newIndex + 1;
            var newEntry = m_swapsProperty.GetArrayElementAtIndex(newIndex);
            var fromProperty = newEntry.FindPropertyRelative("m_from");
            var toProperty = newEntry.FindPropertyRelative("m_to");
            if (fromProperty != null)
            {
                fromProperty.objectReferenceValue = null;
            }

            if (toProperty != null)
            {
                toProperty.objectReferenceValue = null;
            }

            if (!serializedObject.ApplyModifiedProperties())
            {
                UpdateControlState();
                return;
            }

            RebuildEntries();
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes the replacement entry at the specified array index.
        /// </summary>
        /// <param name="index">The zero-based array index.</param>
        internal void RemoveEntryAt(int index)
        {
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            var serializedObject = m_swapsProperty.serializedObject;
            serializedObject.Update();
            if (index < 0 || index >= m_swapsProperty.arraySize)
            {
                return;
            }

            m_swapsProperty.DeleteArrayElementAtIndex(index);
            if (!serializedObject.ApplyModifiedProperties())
            {
                UpdateControlState();
                return;
            }

            RebuildEntries();
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds new material references as replacement entries.
        /// </summary>
        /// <param name="objects">Objects received from a material drag-and-drop operation.</param>
        /// <returns>The number of entries added.</returns>
        internal int AddDroppedMaterials(IEnumerable<UnityEngine.Object> objects)
        {
            if (!CanEdit() || objects == null)
            {
                UpdateControlState();
                return 0;
            }

            var serializedObject = m_swapsProperty.serializedObject;
            serializedObject.Update();
            var materials = CollectNewMaterials(objects);
            if (materials.Count == 0)
            {
                return 0;
            }

            var firstNewIndex = m_swapsProperty.arraySize;
            m_swapsProperty.arraySize = firstNewIndex + materials.Count;
            for (var offset = 0; offset < materials.Count; offset++)
            {
                var entry = m_swapsProperty.GetArrayElementAtIndex(firstNewIndex + offset);
                var fromProperty = entry.FindPropertyRelative("m_from");
                var toProperty = entry.FindPropertyRelative("m_to");
                if (fromProperty != null)
                {
                    fromProperty.objectReferenceValue = materials[offset];
                }

                if (toProperty != null)
                {
                    toProperty.objectReferenceValue = materials[offset];
                }
            }

            if (!serializedObject.ApplyModifiedProperties())
            {
                UpdateControlState();
                return 0;
            }

            RebuildEntries();
            Changed?.Invoke();
            return materials.Count;
        }

        private void OnLanguageChanged(string languageCode)
        {
            ApplyLocalizedTexts();
        }

        private void OnUndoRedoPerformed()
        {
            if (!m_isBound)
            {
                return;
            }

            RebuildEntries();
        }

        private void ApplyLocalizedTexts()
        {
            if (m_addButton == null)
            {
                return;
            }

            m_addButton.tooltip = MateriluneL10n.Get(
                "materilune.ui.swap_list.add_tooltip",
                "Add replacement entry");
            foreach (var row in m_rows)
            {
                row.RemoveButton.tooltip = MateriluneL10n.Get(
                    "materilune.ui.swap_list.remove_tooltip",
                    "Remove this entry");
            }
        }

        private void OnEntryChanged()
        {
            if (CanEdit())
            {
                Changed?.Invoke();
            }
        }

        private void OnDragUpdated(DragUpdatedEvent dragEvent)
        {
            if (CanEdit() && HasNewDroppedMaterials(DragAndDrop.objectReferences))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragPerform(DragPerformEvent dragEvent)
        {
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            if (AddDroppedMaterials(DragAndDrop.objectReferences) > 0)
            {
                DragAndDrop.AcceptDrag();
            }
        }

        private bool HasNewDroppedMaterials(IEnumerable<UnityEngine.Object> objects)
        {
            if (!CanEdit() || objects == null)
            {
                return false;
            }

            m_swapsProperty.serializedObject.Update();
            return CollectNewMaterials(objects).Count > 0;
        }

        private List<Material> CollectNewMaterials(IEnumerable<UnityEngine.Object> objects)
        {
            var existingMaterials = new List<Material>();
            for (var index = 0; index < m_swapsProperty.arraySize; index++)
            {
                var entry = m_swapsProperty.GetArrayElementAtIndex(index);
                var fromProperty = entry.FindPropertyRelative("m_from");
                var material = fromProperty == null ? null : fromProperty.objectReferenceValue as Material;
                if (material != null)
                {
                    existingMaterials.Add(material);
                }
            }

            var materials = new List<Material>();
            foreach (var obj in objects)
            {
                var material = obj as Material;
                if (material == null
                    || ContainsMaterial(existingMaterials, material)
                    || ContainsMaterial(materials, material))
                {
                    continue;
                }

                materials.Add(material);
            }

            return materials;
        }

        private static bool ContainsMaterial(IEnumerable<Material> materials, Material material)
        {
            foreach (var candidate in materials)
            {
                if (candidate == material)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildEntries()
        {
            ClearRows();
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            m_swapsProperty.serializedObject.Update();
            var canEdit = CanEdit();
            for (var index = 0; index < m_swapsProperty.arraySize; index++)
            {
                var row = new VisualElement();
                row.AddToClassList("materilune-swap-list__row");

                var entryView = new MateriluneSwapEntryView();
                entryView.Bind(
                    m_swapsProperty.GetArrayElementAtIndex(index),
                    m_fromCandidates,
                    m_candidateMode);
                entryView.Changed += OnEntryChanged;

                var removeButton = new Button();
                removeButton.text = "-";
                removeButton.tooltip = MateriluneL10n.Get(
                    "materilune.ui.swap_list.remove_tooltip",
                    "Remove this entry");
                var rowIndex = index;
                Action removeAction = () => RemoveEntryAt(rowIndex);
                removeButton.clicked += removeAction;
                removeButton.SetEnabled(canEdit);

                row.Add(entryView);
                row.Add(removeButton);
                m_entries.Add(row);
                m_rows.Add(new RowBinding(entryView, removeButton, removeAction));
            }

            UpdateControlState();
        }

        private void ClearRows()
        {
            foreach (var row in m_rows)
            {
                row.EntryView.Changed -= OnEntryChanged;
                row.RemoveButton.clicked -= row.RemoveAction;
                row.EntryView.Unbind();
            }

            m_rows.Clear();
            if (m_entries != null)
            {
                m_entries.Clear();
            }
        }

        private void UpdateControlState()
        {
            var canEdit = CanEdit();
            if (m_addButton != null)
            {
                m_addButton.SetEnabled(canEdit);
            }

            foreach (var row in m_rows)
            {
                row.RemoveButton.SetEnabled(canEdit);
            }
        }

        private bool CanEdit()
        {
            if (!m_isBound || m_swapsProperty == null)
            {
                return false;
            }

            var serializedObject = m_swapsProperty.serializedObject;
            return serializedObject != null
                && serializedObject.targetObject != null
                && m_swapsProperty.isArray;
        }

        private sealed class RowBinding
        {
            internal readonly MateriluneSwapEntryView EntryView;
            internal readonly Button RemoveButton;
            internal readonly Action RemoveAction;

            internal RowBinding(
                MateriluneSwapEntryView entryView,
                Button removeButton,
                Action removeAction)
            {
                EntryView = entryView;
                RemoveButton = removeButton;
                RemoveAction = removeAction;
            }
        }
    }
}
