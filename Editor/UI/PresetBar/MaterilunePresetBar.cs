using System;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides a bar for switching and adding Materilune presets.
    /// </summary>
    public class MaterilunePresetBar : VisualElement
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/PresetBar/MaterilunePresetBar.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/PresetBar/MaterilunePresetBar.uss";
        private const string ActiveClass = "materilune-preset-bar__item--active";

        private VisualElement m_presets;
        private Button m_addButton;
        private MateriluneSwap m_manager;
        private bool m_isBound;

        /// <summary>
        /// Creates the UXML factory for this element.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<MaterilunePresetBar>
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterilunePresetBar"/> class.
        /// </summary>
        public MaterilunePresetBar()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.preset_bar.load_error",
                    "Materilune could not load the preset bar UI assets."));
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_presets = this.Q<VisualElement>("presets");
            m_addButton = this.Q<Button>("add-button");
            if (m_presets == null || m_addButton == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.preset_bar.load_error",
                    "Materilune could not load the preset bar UI assets."));
                Clear();
                m_presets = null;
                m_addButton = null;
                return;
            }

            m_addButton.text = "+";
            m_addButton.clicked += AddPresetEntry;
            ApplyLocalizedTexts();
            UpdateControlState();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>
        /// Occurs after a preset has been activated or added.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// Binds this bar to a Materilune preset manager.
        /// </summary>
        /// <param name="manager">The manager whose direct child presets are displayed.</param>
        public void Bind(MateriluneSwap manager)
        {
            Unbind();
            if (m_presets == null || m_addButton == null || manager == null)
            {
                return;
            }

            m_manager = manager;
            m_isBound = true;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Refresh();
        }

        /// <summary>
        /// Removes the current manager binding and generated preset buttons.
        /// </summary>
        public void Unbind()
        {
            if (m_isBound)
            {
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            }

            if (m_presets != null)
            {
                m_presets.Clear();
            }

            m_manager = null;
            m_isBound = false;
            UpdateControlState();
        }

        /// <summary>
        /// Rebuilds the preset button list from the manager's current direct children.
        /// </summary>
        public void Refresh()
        {
            if (m_presets == null || m_addButton == null)
            {
                return;
            }

            m_presets.Clear();
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            var presets = m_manager.GetPresets();
            foreach (var preset in presets)
            {
                if (preset == null || preset.gameObject == null)
                {
                    continue;
                }

                var button = new Button();
                button.text = preset.gameObject.name;
                button.tooltip = MateriluneL10n.Get(
                    "materilune.ui.preset_bar.activate_tooltip",
                    "Activate this preset");
                if (preset.gameObject.activeSelf)
                {
                    button.AddToClassList(ActiveClass);
                }

                var capturedPreset = preset;
                button.clicked += () => ActivatePreset(capturedPreset);
                button.SetEnabled(true);
                m_presets.Add(button);
            }

            UpdateControlState();
        }

        /// <summary>
        /// Activates a preset and deactivates the manager's other active presets.
        /// </summary>
        /// <param name="preset">The preset to activate.</param>
        internal void ActivatePreset(MateriluneSwapRoot preset)
        {
            if (!CanEdit() || preset == null || preset.gameObject == null)
            {
                UpdateControlState();
                return;
            }

            var presets = m_manager.GetPresets();
            var isCurrentPreset = false;
            foreach (var currentPreset in presets)
            {
                if (currentPreset == preset)
                {
                    isCurrentPreset = true;
                    break;
                }
            }

            if (!isCurrentPreset || preset.gameObject.activeSelf)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            var undoLabel = MateriluneL10n.Get(
                "materilune.undo.activate_preset",
                "Activate Materilune Preset");
            Undo.SetCurrentGroupName(undoLabel);
            try
            {
                Undo.RecordObject(preset.gameObject, undoLabel);
                preset.gameObject.SetActive(true);
                EditorUtility.SetDirty(preset.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(preset.gameObject);
                foreach (var currentPreset in presets)
                {
                    if (currentPreset == null
                        || currentPreset == preset
                        || currentPreset.gameObject == null
                        || !currentPreset.gameObject.activeSelf)
                    {
                        continue;
                    }

                    Undo.RecordObject(currentPreset.gameObject, undoLabel);
                    currentPreset.gameObject.SetActive(false);
                    EditorUtility.SetDirty(currentPreset.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(currentPreset.gameObject);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Refresh();
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds a preset through the setup service.
        /// </summary>
        internal void AddPresetEntry()
        {
            if (!CanEdit())
            {
                UpdateControlState();
                return;
            }

            MateriluneSetupService.AddPreset(m_manager);
            Refresh();
            Changed?.Invoke();
        }

        private void OnUndoRedoPerformed()
        {
            if (m_isBound)
            {
                Refresh();
            }
        }

        private void OnHierarchyChanged()
        {
            if (m_isBound)
            {
                Refresh();
            }
        }

        private void OnLanguageChanged(string languageCode)
        {
            ApplyLocalizedTexts();
        }

        private void OnAttachToPanel(AttachToPanelEvent attachEvent)
        {
            MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged);
            ApplyLocalizedTexts();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
        {
            MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged);
        }

        private void ApplyLocalizedTexts()
        {
            if (m_addButton == null)
            {
                return;
            }

            m_addButton.tooltip = MateriluneL10n.Get(
                "materilune.ui.preset_bar.add_tooltip",
                "Add preset");
            if (m_presets == null)
            {
                return;
            }

            var tooltip = MateriluneL10n.Get(
                "materilune.ui.preset_bar.activate_tooltip",
                "Activate this preset");
            foreach (var child in m_presets.Children())
            {
                var button = child as Button;
                if (button != null)
                {
                    button.tooltip = tooltip;
                }
            }
        }

        private void UpdateControlState()
        {
            var canEdit = CanEdit();
            if (m_addButton != null)
            {
                m_addButton.SetEnabled(canEdit);
            }

            if (m_presets == null)
            {
                return;
            }

            if (!canEdit && m_manager == null)
            {
                m_presets.Clear();
                return;
            }

            foreach (var child in m_presets.Children())
            {
                var button = child as Button;
                if (button != null)
                {
                    button.SetEnabled(canEdit);
                }
            }
        }

        private bool CanEdit()
        {
            return m_isBound
                && m_manager != null
                && m_presets != null
                && m_addButton != null;
        }
    }
}
