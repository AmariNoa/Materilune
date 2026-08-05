using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides the single Materilune editing window.
    /// </summary>
    public class MateriluneWindow : EditorWindow
    {
        // Points at the current layout. Switch both paths to MateriluneWindowLayout once that
        // document defines the elements this window looks up.
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindow.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindow.uss";

        private ObjectField m_targetField;
        private Toggle m_lockToggle;
        private VisualElement m_setupContainer;
        private Label m_setupMessage;
        private Button m_setupButton;
        private VisualElement m_contentContainer;
        private Label m_presetHeader;
        private Label m_rootHeader;
        private Label m_overrideHeader;
        private Label m_treeHeader;
        private Label m_emptyMessage;
        private VisualElement m_languageSlot;
        private VisualElement m_presetSlot;
        private VisualElement m_rootSlot;
        private VisualElement m_treeSlot;
        private VisualElement m_overrideSlot;

        private MaterilunePresetBar m_presetBar;
        private MateriluneSwapListView m_rootSwapList;
        private MateriluneSwapListView m_overrideSwapList;
        private MateriluneTargetTreeView m_targetTree;
        private MateriluneLanguageSelector m_languageSelector;

        private MateriluneSwap m_manager;
        private MateriluneSwapRoot m_activePreset;
        private MateriluneSwapRoot m_lastActivePreset;
        private Renderer m_selectedRenderer;
        private SerializedObject m_rootSerializedObject;
        private SerializedObject m_overrideSerializedObject;
        private GameObject m_currentCandidate;
        private GameObject m_setupCandidate;
        private bool m_isLocked;
        private bool m_uiReady;
        private bool m_isSubscribed;
        private bool m_useTestTarget;
        private GameObject m_testTarget;

        /// <summary>
        /// Shows the Materilune window, reusing the existing instance when present.
        /// </summary>
        [MenuItem("Tools/Materilune/Materilune Window")]
        public static void ShowWindow()
        {
            GetOrCreateWindow();
        }

        /// <summary>
        /// Opens the window through the same path used by the menu command.
        /// </summary>
        /// <returns>The single Materilune window instance.</returns>
        internal static MateriluneWindow OpenForTests()
        {
            return GetOrCreateWindow();
        }

        /// <summary>
        /// Sets a target without relying on the current hierarchy selection.
        /// </summary>
        /// <param name="target">The object to resolve.</param>
        internal void SetTargetForTests(GameObject target)
        {
            m_useTestTarget = true;
            m_testTarget = target;
            m_currentCandidate = target;
            if (!m_uiReady)
            {
                CreateGUI();
            }

            Rebuild();
        }

        /// <summary>
        /// Gets the manager currently resolved by the window.
        /// </summary>
        internal MateriluneSwap ResolvedManager => m_manager == null ? null : m_manager;

        /// <summary>
        /// Gets the preset whose replacements the window is showing.
        /// </summary>
        internal MateriluneSwapRoot DisplayedPreset => m_activePreset == null ? null : m_activePreset;

        /// <summary>
        /// Shows a specific preset without going through the preset bar.
        /// </summary>
        /// <param name="preset">The preset to display.</param>
        internal void SetDisplayedPresetForTests(MateriluneSwapRoot preset)
        {
            m_activePreset = preset;
            BindRoot(preset);
        }

        private static MateriluneWindow GetOrCreateWindow()
        {
            var window = GetWindow<MateriluneWindow>();
            window.titleContent = new GUIContent(MateriluneL10n.Get(
                "materilune.ui.window.title",
                "Materilune"));
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            if (m_isSubscribed)
            {
                return;
            }

            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged);
            m_isSubscribed = true;

            if (m_uiReady)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            if (m_isSubscribed)
            {
                Selection.selectionChanged -= OnSelectionChanged;
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
                MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged);
                m_isSubscribed = false;
            }

            UnsubscribeUiCallbacks();
            UnbindViews();
        }

        private void CreateGUI()
        {
            UnsubscribeUiCallbacks();
            UnbindViews();
            m_uiReady = false;
            rootVisualElement.Clear();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                LogLoadError();
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(styleSheet);
            CacheControls();
            if (!HasRequiredControls())
            {
                LogLoadError();
                rootVisualElement.Clear();
                ClearControlReferences();
                return;
            }

            m_targetField.objectType = typeof(GameObject);
            m_targetField.allowSceneObjects = true;
            m_targetField.SetEnabled(false);
            m_lockToggle.SetValueWithoutNotify(m_isLocked);

            m_presetBar = new MaterilunePresetBar();
            m_rootSwapList = new MateriluneSwapListView();
            m_overrideSwapList = new MateriluneSwapListView();
            m_targetTree = new MateriluneTargetTreeView();
            m_languageSelector = new MateriluneLanguageSelector();
            m_languageSlot.Add(m_languageSelector);
            m_presetSlot.Add(m_presetBar);
            m_rootSlot.Add(m_rootSwapList);
            m_treeSlot.Add(m_targetTree);
            m_overrideSlot.Add(m_overrideSwapList);

            // The tree carries its own scroll view, so it has to take the pane's height instead
            // of growing with its contents. The other parts sit inside the window's scroll views
            // and are meant to be as tall as their contents.
            m_targetTree.style.flexGrow = 1f;
            m_targetTree.style.minHeight = 0f;

            m_lockToggle.RegisterValueChangedCallback(OnLockChanged);
            m_setupButton.clicked += OnSetupClicked;
            m_presetBar.Changed += OnViewChanged;
            m_rootSwapList.Changed += OnViewChanged;
            m_overrideSwapList.Changed += OnViewChanged;
            m_targetTree.RendererSelected += OnRendererSelected;

            m_uiReady = true;
            ApplyLocalizedTexts();
            Rebuild();
        }

        private void CacheControls()
        {
            m_targetField = rootVisualElement.Q<ObjectField>("target-field");
            m_lockToggle = rootVisualElement.Q<Toggle>("lock-toggle");
            m_setupContainer = rootVisualElement.Q<VisualElement>("setup-container");
            m_setupMessage = rootVisualElement.Q<Label>("setup-message");
            m_setupButton = rootVisualElement.Q<Button>("setup-button");
            m_contentContainer = rootVisualElement.Q<VisualElement>("content-container");
            m_presetHeader = rootVisualElement.Q<Label>("preset-header");
            m_rootHeader = rootVisualElement.Q<Label>("root-header");
            m_overrideHeader = rootVisualElement.Q<Label>("override-header");
            m_treeHeader = rootVisualElement.Q<Label>("tree-header");
            m_emptyMessage = rootVisualElement.Q<Label>("empty-message");
            m_languageSlot = rootVisualElement.Q<VisualElement>("language-slot");
            m_presetSlot = rootVisualElement.Q<VisualElement>("preset-slot");
            m_rootSlot = rootVisualElement.Q<VisualElement>("root-slot");
            m_treeSlot = rootVisualElement.Q<VisualElement>("tree-slot");
            m_overrideSlot = rootVisualElement.Q<VisualElement>("override-slot");
        }

        private bool HasRequiredControls()
        {
            return m_targetField != null
                && m_lockToggle != null
                && m_setupContainer != null
                && m_setupMessage != null
                && m_setupButton != null
                && m_contentContainer != null
                && m_presetHeader != null
                && m_rootHeader != null
                && m_overrideHeader != null
                && m_treeHeader != null
                && m_emptyMessage != null
                && m_languageSlot != null
                && m_presetSlot != null
                && m_rootSlot != null
                && m_treeSlot != null
                && m_overrideSlot != null;
        }

        private void ClearControlReferences()
        {
            m_targetField = null;
            m_lockToggle = null;
            m_setupContainer = null;
            m_setupMessage = null;
            m_setupButton = null;
            m_contentContainer = null;
            m_presetHeader = null;
            m_rootHeader = null;
            m_overrideHeader = null;
            m_treeHeader = null;
            m_emptyMessage = null;
            m_languageSlot = null;
            m_presetSlot = null;
            m_rootSlot = null;
            m_treeSlot = null;
            m_overrideSlot = null;
        }

        private void UnsubscribeUiCallbacks()
        {
            if (m_lockToggle != null)
            {
                m_lockToggle.UnregisterValueChangedCallback(OnLockChanged);
            }

            if (m_setupButton != null)
            {
                m_setupButton.clicked -= OnSetupClicked;
            }

            if (m_presetBar != null)
            {
                m_presetBar.Changed -= OnViewChanged;
            }

            if (m_rootSwapList != null)
            {
                m_rootSwapList.Changed -= OnViewChanged;
            }

            if (m_overrideSwapList != null)
            {
                m_overrideSwapList.Changed -= OnViewChanged;
            }

            if (m_targetTree != null)
            {
                m_targetTree.RendererSelected -= OnRendererSelected;
            }
        }

        private void UnbindViews()
        {
            if (m_presetBar != null)
            {
                m_presetBar.Unbind();
            }

            if (m_rootSwapList != null)
            {
                m_rootSwapList.Unbind();
            }

            if (m_overrideSwapList != null)
            {
                m_overrideSwapList.Unbind();
            }

            if (m_targetTree != null)
            {
                m_targetTree.Unbind();
            }

            m_rootSerializedObject = null;
            m_overrideSerializedObject = null;
        }

        private void OnSelectionChanged()
        {
            if (!m_uiReady || m_isLocked)
            {
                return;
            }

            m_useTestTarget = false;
            m_testTarget = null;
            Rebuild();
        }

        private void OnUndoRedoPerformed()
        {
            if (m_uiReady)
            {
                Rebuild();
            }
        }

        private void OnHierarchyChanged()
        {
            if (m_uiReady)
            {
                Rebuild();
            }
        }

        private void OnLanguageChanged(string languageCode)
        {
            if (m_uiReady)
            {
                ApplyLocalizedTexts();
            }
        }

        private void OnLockChanged(ChangeEvent<bool> changeEvent)
        {
            m_isLocked = changeEvent.newValue;
            if (!m_isLocked)
            {
                m_useTestTarget = false;
                m_testTarget = null;
            }

            if (m_uiReady)
            {
                Rebuild();
            }
        }

        private void OnSetupClicked()
        {
            var candidate = m_setupCandidate;
            if (candidate == null)
            {
                Rebuild();
                return;
            }

            MateriluneSetupService.Setup(candidate);
            m_currentCandidate = candidate;
            Rebuild();
        }

        private void OnViewChanged()
        {
            var manager = ResolvedManager;
            if (manager == null)
            {
                Rebuild();
                return;
            }

            // The part already recorded the user's edit in the current group. Fold the
            // synchronization into it so one undo takes back both the edit and its effect on
            // the Material Swap components.
            var undoGroup = Undo.GetCurrentGroup();
            MateriluneSwapSynchronizer.Sync(manager);
            Undo.CollapseUndoOperations(undoGroup);
            Rebuild();
        }

        private void OnRendererSelected(Renderer renderer)
        {
            if (!m_uiReady)
            {
                return;
            }

            m_selectedRenderer = renderer;
            BindOverride(renderer);
        }

        private void Rebuild()
        {
            if (!m_uiReady)
            {
                return;
            }

            // The renderer and preset the user was working on survive a rebuild as long as they
            // still belong to the resolved manager, so editing a row does not reset the panes.
            var previousRenderer = m_selectedRenderer;
            var previousPreset = m_activePreset;
            UnbindViews();
            var candidate = GetCandidate();
            m_currentCandidate = candidate;
            m_setupCandidate = candidate;
            var manager = ResolveManager(candidate);
            m_manager = manager;
            var target = GetTargetObject(manager);

            if (candidate == null)
            {
                ShowEmpty();
                return;
            }

            if (manager == null || target == null)
            {
                m_manager = null;
                m_activePreset = null;
                m_selectedRenderer = null;
                SetDisplay(m_setupContainer, true);
                SetDisplay(m_contentContainer, false);
                SetDisplay(m_emptyMessage, false);
                SetTargetField(candidate);
                return;
            }

            SetDisplay(m_setupContainer, false);
            SetDisplay(m_contentContainer, true);
            SetDisplay(m_emptyMessage, false);
            SetTargetField(target);

            m_presetBar.Bind(manager);
            m_targetTree.Bind(target);
            // Follow whichever preset became active, whatever caused it: the preset bar, an undo
            // or an outside change. Anything that leaves the active preset alone, such as adding
            // one, keeps the preset the user was looking at on screen.
            var activePreset = FindActiveOnly(manager);
            var activeChanged = activePreset != m_lastActivePreset;
            m_lastActivePreset = activePreset;
            m_activePreset = activeChanged && activePreset != null
                ? activePreset
                : ResolvePreset(manager, previousPreset);
            BindRoot(m_activePreset);

            m_selectedRenderer = FindOverride(m_activePreset, previousRenderer) == null
                ? null
                : previousRenderer;
            if (m_selectedRenderer != null)
            {
                m_targetTree.SelectRenderer(m_selectedRenderer);
            }

            BindOverride(m_selectedRenderer);
        }

        private void ShowEmpty()
        {
            m_manager = null;
            m_activePreset = null;
            m_selectedRenderer = null;
            SetDisplay(m_setupContainer, false);
            SetDisplay(m_contentContainer, false);
            SetDisplay(m_emptyMessage, true);
            SetTargetField(null);
        }

        private GameObject GetCandidate()
        {
            if (m_isLocked || m_useTestTarget)
            {
                return m_currentCandidate == null ? m_testTarget : m_currentCandidate;
            }

            return Selection.activeGameObject;
        }

        private void BindRoot(MateriluneSwapRoot preset)
        {
            if (preset == null || m_rootSwapList == null)
            {
                return;
            }

            m_rootSerializedObject = new SerializedObject(preset);
            var swapsProperty = m_rootSerializedObject.FindProperty("m_swaps");
            if (swapsProperty != null)
            {
                m_rootSwapList.Bind(swapsProperty, preset.AvailableMaterials, preset.CandidateMode);
            }
        }

        private void BindOverride(Renderer renderer)
        {
            if (m_overrideSwapList == null)
            {
                return;
            }

            m_overrideSwapList.Unbind();
            m_overrideSerializedObject = null;
            var preset = m_activePreset;
            if (preset == null || renderer == null)
            {
                return;
            }

            var operationOverride = FindOverride(preset, renderer);
            if (operationOverride == null)
            {
                return;
            }

            m_overrideSerializedObject = new SerializedObject(operationOverride);
            var swapsProperty = m_overrideSerializedObject.FindProperty("m_swaps");
            if (swapsProperty != null)
            {
                m_overrideSwapList.Bind(
                    swapsProperty,
                    operationOverride.AvailableMaterials,
                    operationOverride.CandidateMode);
            }
        }

        /// <summary>
        /// Keeps the preset the window was showing while it still belongs to the manager.
        /// </summary>
        /// <param name="manager">The resolved manager.</param>
        /// <param name="previousPreset">The preset shown before the rebuild.</param>
        /// <returns>The preset to display.</returns>
        private static MateriluneSwapRoot ResolvePreset(MateriluneSwap manager, MateriluneSwapRoot previousPreset)
        {
            if (manager == null)
            {
                return null;
            }

            if (previousPreset != null)
            {
                foreach (var preset in manager.GetPresets())
                {
                    if (preset == previousPreset)
                    {
                        return preset;
                    }
                }
            }

            return FindActivePreset(manager);
        }

        /// <summary>
        /// Finds the preset that is actually active, without falling back to the first one.
        /// Presets may all be inactive, and that state has to stay distinguishable so the
        /// window can tell an activation apart from a mere fallback.
        /// </summary>
        /// <param name="manager">The manager whose presets are inspected.</param>
        /// <returns>The active preset, or <see langword="null" /> when none is active.</returns>
        private static MateriluneSwapRoot FindActiveOnly(MateriluneSwap manager)
        {
            var presets = manager == null ? null : manager.GetPresets();
            if (presets == null)
            {
                return null;
            }

            foreach (var preset in presets)
            {
                if (preset != null && preset.gameObject != null && preset.gameObject.activeSelf)
                {
                    return preset;
                }
            }

            return null;
        }

        private static MateriluneSwapRoot FindActivePreset(MateriluneSwap manager)
        {
            var presets = manager == null ? null : manager.GetPresets();
            if (presets == null || presets.Count == 0)
            {
                return null;
            }

            MateriluneSwapRoot firstPreset = null;
            foreach (var preset in presets)
            {
                if (preset == null || preset.gameObject == null)
                {
                    continue;
                }

                firstPreset = firstPreset ?? preset;
                if (preset.gameObject.activeSelf)
                {
                    return preset;
                }
            }

            return firstPreset;
        }

        private static MateriluneSwapOverride FindOverride(MateriluneSwapRoot preset, Renderer renderer)
        {
            if (preset == null || renderer == null)
            {
                return null;
            }

            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride != null && operationOverride.TargetRenderer == renderer)
                {
                    return operationOverride;
                }
            }

            return null;
        }

        private static MateriluneSwap ResolveManager(GameObject candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            // The candidate's own manager wins over an outer one, so selecting a target that
            // sits inside another set up hierarchy still edits the target's own presets.
            var manager = FindManagerInManagedChildren(candidate.transform);
            if (manager != null)
            {
                return manager;
            }

            manager = candidate.GetComponentInParent<MateriluneSwap>();
            if (manager != null)
            {
                return manager;
            }

            // Setup places the manager beside the target's selected child. Walk the
            // candidate's ancestors and inspect their direct children by reference so
            // selecting a renderer child still resolves the same manager.
            for (var ancestor = candidate.transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                manager = FindManagerInManagedChildren(ancestor);
                if (manager != null)
                {
                    return manager;
                }
            }

            return null;
        }

        private static MateriluneSwap FindManagerInManagedChildren(Transform parent)
        {
            var manager = FindDirectChildManager(parent);
            if (manager != null)
            {
                return manager;
            }

            foreach (Transform child in parent)
            {
                if (child == null || child.GetComponent<Materilune>() == null)
                {
                    continue;
                }

                manager = FindDirectChildManager(child);
                if (manager != null)
                {
                    return manager;
                }
            }

            return null;
        }

        private static MateriluneSwap FindDirectChildManager(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child == null)
                {
                    continue;
                }

                var manager = child.GetComponent<MateriluneSwap>();
                if (manager != null)
                {
                    return manager;
                }
            }

            return null;
        }

        private static GameObject GetTargetObject(MateriluneSwap manager)
        {
            if (manager == null)
            {
                return null;
            }

            // The target is only well defined when the manager sits under a marker that in turn
            // sits under the target. Falling back to the manager or the marker itself when the
            // hierarchy is incomplete would point the swaps at the wrong object, and a marker
            // nested inside another marker has no target of its own.
            var marker = manager.transform.parent;
            if (marker == null || marker.GetComponent<Materilune>() == null || marker.parent == null)
            {
                return null;
            }

            return marker.parent.GetComponent<Materilune>() != null
                ? null
                : marker.parent.gameObject;
        }

        private void SetTargetField(GameObject target)
        {
            if (m_targetField != null)
            {
                m_targetField.SetValueWithoutNotify(target);
            }
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ApplyLocalizedTexts()
        {
            titleContent = new GUIContent(MateriluneL10n.Get(
                "materilune.ui.window.title",
                "Materilune"));
            if (m_lockToggle != null)
            {
                m_lockToggle.tooltip = MateriluneL10n.Get(
                    "materilune.ui.window.lock_tooltip",
                    "Keep the current target when the selection changes");
            }

            if (m_setupMessage != null)
            {
                m_setupMessage.text = MateriluneL10n.Get(
                    "materilune.ui.window.setup_message",
                    "Materilune is not set up on this object yet.");
            }

            if (m_setupButton != null)
            {
                m_setupButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.setup_button",
                    "Run setup");
            }

            if (m_rootHeader != null)
            {
                m_rootHeader.text = MateriluneL10n.Get(
                    "materilune.ui.window.root_header",
                    "Preset-wide replacements");
            }

            if (m_presetHeader != null)
            {
                m_presetHeader.text = MateriluneL10n.Get(
                    "materilune.ui.window.preset_header",
                    "Presets");
            }

            if (m_overrideHeader != null)
            {
                m_overrideHeader.text = MateriluneL10n.Get(
                    "materilune.ui.window.override_header",
                    "Selected mesh replacements");
            }

            if (m_treeHeader != null)
            {
                m_treeHeader.text = MateriluneL10n.Get(
                    "materilune.ui.window.tree_header",
                    "Target meshes");
            }

            if (m_emptyMessage != null)
            {
                m_emptyMessage.text = MateriluneL10n.Get(
                    "materilune.ui.window.empty_message",
                    "Select an avatar or outfit object in the hierarchy.");
            }

            if (m_languageSelector != null)
            {
                m_languageSelector.Refresh();
            }
        }

        private static void LogLoadError()
        {
            Debug.LogError(MateriluneL10n.Get(
                "materilune.ui.window.load_error",
                "Materilune could not load the window UI assets."));
        }
    }
}
