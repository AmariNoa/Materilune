using System;
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
    /// Provides the single Materilune editing window.
    /// </summary>
    public class MateriluneWindow : EditorWindow
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindowLayout.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindowLayout.uss";
        private const string PresetRowUxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindowPresetRow.uxml";
        private const string SwapEntryRowUxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/Window/MateriluneWindowSwapEntryRow.uxml";
        private const string ActivePresetClass = "materilune-window__preset--active";
        private const string StatusWarningClass = "materilune-window__status--warning";
        private const float MinimumWindowWidth = 720f;
        private const float MinimumWindowHeight = 360f;
        private const float DefaultWindowWidth = 1000f;
        private const float DefaultWindowHeight = 560f;
        private const float LabelRowHeight = 20f;
        private const float SwapRowHeight = 22f;

        private readonly List<MateriluneSwapRoot> m_emptyPresets = new List<MateriluneSwapRoot>();
        private readonly List<MateriluneMaterialSwapEntry> m_emptySwapEntries =
            new List<MateriluneMaterialSwapEntry>();
        private readonly List<TreeViewItemData<Transform>> m_emptyTreeItems =
            new List<TreeViewItemData<Transform>>();
        private readonly List<TreeViewItemData<Transform>> m_treeItems =
            new List<TreeViewItemData<Transform>>();
        private readonly Dictionary<VisualElement, PresetRowBinding> m_presetRowBindings =
            new Dictionary<VisualElement, PresetRowBinding>();
        private readonly Dictionary<VisualElement, SwapRowBinding> m_swapRowBindings =
            new Dictionary<VisualElement, SwapRowBinding>();

        private ObjectField m_targetField;
        private DropdownField m_languageDropdown;
        private Button m_swapButton;
        private Button m_presetAddButton;
        private Button m_rootClearButton;
        private Button m_overrideClearButton;
        private VisualElement m_statusBar;
        private Label m_statusMessage;
        private Button m_updateButton;
        private ListView m_presetList;
        private ListView m_rootSwapList;
        private TreeView m_overrideTree;
        private ListView m_overrideSwapList;
        private Label m_presetHeader;
        private Label m_rootHeader;
        private Label m_overrideHeader;
        private Label m_treeHeader;
        private VisualTreeAsset m_presetRowTemplate;
        private VisualTreeAsset m_swapEntryRowTemplate;

        private MateriluneSwap m_manager;
        private MateriluneSwapRoot m_activePreset;
        private MateriluneSwapRoot m_lastActivePreset;
        private Renderer m_selectedRenderer;
        private SerializedObject m_rootSerializedObject;
        private SerializedObject m_overrideSerializedObject;
        private bool m_uiReady;
        private bool m_isSubscribed;
        private bool m_isRebuilding;
        private int m_bindingDepth;
        private bool m_isRestoringPresetSelection;
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
        /// Gets the renderer currently selected in the target tree.
        /// </summary>
        internal Renderer SelectedRenderer => m_selectedRenderer == null ? null : m_selectedRenderer;

        /// <summary>
        /// Shows a specific preset without going through the preset bar.
        /// </summary>
        /// <param name="preset">The preset to display.</param>
        internal void SetDisplayedPresetForTests(MateriluneSwapRoot preset)
        {
            m_activePreset = preset;
            BindRoot(preset);
            ApplyPresetSelection();
            UpdateAddButtonStates();
        }

        private static MateriluneWindow GetOrCreateWindow()
        {
            var window = GetWindow<MateriluneWindow>();
            window.titleContent = new GUIContent(MateriluneL10n.Get(
                "materilune.ui.window.title",
                "Materilune"));

            // Four columns and a toolbar need room. Below the minimum the columns collapse into
            // each other, so the window refuses to go smaller and opens wider than that.
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            if (window.position.width < DefaultWindowWidth || window.position.height < DefaultWindowHeight)
            {
                var position = window.position;
                position.width = Mathf.Max(position.width, DefaultWindowWidth);
                position.height = Mathf.Max(position.height, DefaultWindowHeight);
                window.position = position;
            }

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
            m_presetRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PresetRowUxmlPath);
            m_swapEntryRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SwapEntryRowUxmlPath);
            if (visualTree == null
                || styleSheet == null
                || m_presetRowTemplate == null
                || m_swapEntryRowTemplate == null)
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
            ConfigureListAndTreeViews();
            m_languageDropdown.RegisterValueChangedCallback(OnLanguageValueChanged);
            m_presetList.selectionChanged += OnPresetSelectionChanged;
            m_overrideTree.selectionChanged += OnTreeSelectionChanged;
            m_presetAddButton.clicked += OnPresetAddClicked;
            if (m_updateButton != null)
            {
                m_updateButton.clicked += OnUpdateClicked;
            }

            if (m_rootClearButton != null)
            {
                m_rootClearButton.clicked += OnRootClearClicked;
            }

            if (m_overrideClearButton != null)
            {
                m_overrideClearButton.clicked += OnOverrideClearClicked;
            }

            m_uiReady = true;
            ApplyLocalizedTexts();
            Rebuild();
        }

        private void CacheControls()
        {
            m_targetField = rootVisualElement.Q<ObjectField>("target-field");
            m_languageDropdown = rootVisualElement.Q<DropdownField>("dd-language");
            m_swapButton = rootVisualElement.Q<Button>("btn-swap");
            m_presetAddButton = rootVisualElement.Q<Button>("btn-preset-add");

            // The status bar is optional so that a layout edit that drops it only loses the
            // status line instead of stopping the whole window from loading.
            m_statusBar = rootVisualElement.Q<VisualElement>("elm-status-bar");
            m_statusMessage = rootVisualElement.Q<Label>("lbl-status-message");
            m_updateButton = rootVisualElement.Q<Button>("btn-update");
            m_rootClearButton = rootVisualElement.Q<Button>("btn-clear-swap-root-entries");
            m_overrideClearButton =
                rootVisualElement.Q<Button>("btn-clear-swap-override-entries");
            m_presetList = rootVisualElement.Q<ListView>("lv-preset-list");
            m_rootSwapList = rootVisualElement.Q<ListView>("lv-swap-root-entries");
            m_overrideTree = rootVisualElement.Q<TreeView>("tv-swap-override-components");
            m_overrideSwapList = rootVisualElement.Q<ListView>("lv-swap-override-entries");

            m_presetHeader = FindSiblingLabel(m_presetList);
            m_rootHeader = FindSiblingLabel(m_rootSwapList);
            m_treeHeader = FindSiblingLabel(m_overrideTree);
            m_overrideHeader = FindSiblingLabel(m_overrideSwapList);
        }

        private bool HasRequiredControls()
        {
            return m_targetField != null
                && m_languageDropdown != null
                && m_swapButton != null
                && m_presetAddButton != null
                && m_presetList != null
                && m_rootSwapList != null
                && m_overrideTree != null
                && m_overrideSwapList != null;
        }

        private void ClearControlReferences()
        {
            m_targetField = null;
            m_languageDropdown = null;
            m_swapButton = null;
            m_presetAddButton = null;
            m_statusBar = null;
            m_statusMessage = null;
            m_updateButton = null;
            m_rootClearButton = null;
            m_overrideClearButton = null;
            m_presetList = null;
            m_rootSwapList = null;
            m_overrideTree = null;
            m_overrideSwapList = null;
            m_presetHeader = null;
            m_rootHeader = null;
            m_overrideHeader = null;
            m_treeHeader = null;
        }

        private void UnsubscribeUiCallbacks()
        {
            if (m_languageDropdown != null)
            {
                m_languageDropdown.UnregisterValueChangedCallback(OnLanguageValueChanged);
            }

            if (m_presetList != null)
            {
                m_presetList.selectionChanged -= OnPresetSelectionChanged;
            }

            if (m_overrideTree != null)
            {
                m_overrideTree.selectionChanged -= OnTreeSelectionChanged;
            }

            if (m_presetAddButton != null)
            {
                m_presetAddButton.clicked -= OnPresetAddClicked;
            }

            if (m_updateButton != null)
            {
                m_updateButton.clicked -= OnUpdateClicked;
            }

            if (m_rootClearButton != null)
            {
                m_rootClearButton.clicked -= OnRootClearClicked;
            }

            if (m_overrideClearButton != null)
            {
                m_overrideClearButton.clicked -= OnOverrideClearClicked;
            }
        }

        private void UnbindViews()
        {
            ClearListView(m_presetList, m_emptyPresets);
            ClearListView(m_rootSwapList, m_emptySwapEntries);
            ClearListView(m_overrideSwapList, m_emptySwapEntries);

            if (m_overrideTree != null)
            {
                m_treeItems.Clear();
                m_overrideTree.SetRootItems(m_emptyTreeItems);
                m_overrideTree.Rebuild();
            }

            ClearPresetRowBindings();
            ClearSwapRowBindings();

            m_rootSerializedObject = null;
            m_overrideSerializedObject = null;
        }

        private void OnSelectionChanged()
        {
            if (!m_uiReady)
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

        private void OnLanguageValueChanged(ChangeEvent<string> changeEvent)
        {
            if (m_languageDropdown == null)
            {
                return;
            }

            if (!IsAvailableLanguage(changeEvent.newValue)
                || !MateriluneL10n.SetLanguage(changeEvent.newValue))
            {
                m_languageDropdown.SetValueWithoutNotify(changeEvent.previousValue);
            }
        }

        private void OnPresetSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (m_isRebuilding || m_isRestoringPresetSelection || selectedItems == null)
            {
                return;
            }

            MateriluneSwapRoot selectedPreset = null;
            foreach (var selectedItem in selectedItems)
            {
                selectedPreset = selectedItem as MateriluneSwapRoot;
                if (selectedPreset != null)
                {
                    break;
                }
            }

            if (selectedPreset != null)
            {
                ActivatePreset(selectedPreset);
            }
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (m_isRebuilding || selectedItems == null)
            {
                return;
            }

            m_selectedRenderer = null;
            foreach (var selectedItem in selectedItems)
            {
                var transform = selectedItem as Transform;
                if (transform != null)
                {
                    m_selectedRenderer = FindFirstRenderer(transform);
                    break;
                }
            }

            BindOverride(m_selectedRenderer);
        }

        private void OnRootViewChanged()
        {
            OnViewChanged(m_rootSerializedObject);
        }

        private void OnOverrideViewChanged()
        {
            OnViewChanged(m_overrideSerializedObject);
        }

        private void OnViewChanged(SerializedObject changedObject)
        {
            // A row can only report an edit that the user made. Anything raised while the window
            // is filling its own views comes from the rebuild itself, and acting on it would
            // rebuild again from inside the rebuild.
            if (m_isRebuilding || m_bindingDepth > 0)
            {
                return;
            }

            var manager = ResolvedManager;
            if (manager == null)
            {
                Rebuild();
                return;
            }

            // The row already recorded the user's edit in the current group. Fold the
            // synchronization into it so one undo takes back both the edit and its effect on
            // the Material Swap components.
            var undoGroup = Undo.GetCurrentGroup();
            MarkObjectDirty(changedObject == null ? null : changedObject.targetObject);
            MateriluneSwapSynchronizer.Sync(manager);
            Undo.CollapseUndoOperations(undoGroup);
            Rebuild();
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
            m_isRebuilding = true;
            try
            {
                UnbindViews();
                var candidate = GetCandidate();
                var manager = ResolveManager(candidate);
                var target = GetTargetObject(manager);
                m_manager = manager;

                if (candidate == null)
                {
                    m_manager = null;
                    m_activePreset = null;
                    m_lastActivePreset = null;
                    m_selectedRenderer = null;
                    SetTargetField(null);
                    return;
                }

                SetTargetField(manager != null && target != null ? target : candidate);
                if (manager == null || target == null)
                {
                    m_manager = null;
                    m_activePreset = null;
                    m_lastActivePreset = null;
                    m_selectedRenderer = null;
                    return;
                }

                var activePreset = FindActiveOnly(manager);
                var activeChanged = activePreset != m_lastActivePreset;
                m_lastActivePreset = activePreset;
                m_activePreset = activeChanged && activePreset != null
                    ? activePreset
                    : ResolvePreset(manager, previousPreset);

                BindPresetList(manager);
                BindRoot(m_activePreset);
                m_selectedRenderer = IsRendererInTarget(target, previousRenderer)
                    ? previousRenderer
                    : null;
                BindTargetTree(target);
                BindOverride(m_selectedRenderer);
            }
            finally
            {
                m_isRebuilding = false;
                UpdateAddButtonStates();
                RefreshStatusBar();
            }
        }

        /// <summary>
        /// Reports what the window is looking at and whether the entries still match the target.
        /// </summary>
        /// <remarks>
        /// The bar always says something, so the space it occupies never reads as an empty gap,
        /// and it never changes the layout: the row keeps its place and the update button keeps
        /// its space while hidden (AGENTS.md 2.4 (7)).
        /// </remarks>
        private void RefreshStatusBar()
        {
            if (m_statusBar == null)
            {
                return;
            }

            var manager = ResolvedManager;
            var target = manager == null ? null : GetTargetObject(manager);
            var needsUpdate = target != null && MateriluneSwapEntries.NeedsUpdate(manager);

            if (m_statusMessage != null)
            {
                m_statusMessage.text = GetStatusMessage(manager, target, needsUpdate);
            }

            m_statusBar.EnableInClassList(StatusWarningClass, needsUpdate);
            if (m_updateButton != null)
            {
                m_updateButton.style.visibility = needsUpdate
                    ? Visibility.Visible
                    : Visibility.Hidden;
                m_updateButton.SetEnabled(needsUpdate);
            }
        }

        private string GetStatusMessage(MateriluneSwap manager, GameObject target, bool needsUpdate)
        {
            if (GetCandidate() == null)
            {
                return MateriluneL10n.Get(
                    "materilune.ui.window.status_no_target",
                    "Select the object to work on in the hierarchy.");
            }

            if (manager == null || target == null)
            {
                return MateriluneL10n.Get(
                    "materilune.ui.window.status_not_set_up",
                    "Materilune is not set up on this object.");
            }

            // The warning replaces the summary rather than joining it. It is the one state that
            // asks for an action, and a single line has to stay readable.
            if (needsUpdate)
            {
                return MateriluneL10n.Get(
                    "materilune.ui.window.status_update_required",
                    "The target meshes carry materials that are not listed yet.");
            }

            return BuildSummary(manager, target);
        }

        private string BuildSummary(MateriluneSwap manager, GameObject target)
        {
            var presets = manager.GetPresets();
            var rendererCount = MateriluneSetupService.CollectTargetRenderers(target).Count;
            int total;
            int assigned;
            int orphaned;
            MateriluneSwapEntries.CountEntries(m_activePreset, out total, out assigned, out orphaned);

            var summary = string.Format(
                MateriluneL10n.Get(
                    "materilune.ui.window.status_summary",
                    "{0} presets, {1} target meshes, {2} of {3} replacements set"),
                presets == null ? 0 : presets.Count,
                rendererCount,
                assigned,
                total);
            if (orphaned == 0)
            {
                return summary;
            }

            return summary + string.Format(
                MateriluneL10n.Get(
                    "materilune.ui.window.status_summary_orphans",
                    " (orphaned entries: {0})"),
                orphaned);
        }

        private void OnRootClearClicked()
        {
            ClearReplacements(m_rootSerializedObject);
        }

        private void OnOverrideClearClicked()
        {
            ClearReplacements(m_overrideSerializedObject);
        }

        /// <summary>
        /// Clears the replacement of every entry a component holds, leaving the entries in place.
        /// </summary>
        /// <param name="serializedObject">The component whose replacements are cleared.</param>
        /// <remarks>
        /// The entries are generated from the target meshes and are not the user's to remove, so
        /// only the replacements go back to none. One undo takes the whole panel back.
        /// </remarks>
        private void ClearReplacements(SerializedObject serializedObject)
        {
            var manager = ResolvedManager;
            if (manager == null || serializedObject == null || serializedObject.targetObject == null)
            {
                Rebuild();
                return;
            }

            serializedObject.Update();
            var swapsProperty = serializedObject.FindProperty("m_swaps");
            if (swapsProperty == null || !swapsProperty.isArray)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MateriluneL10n.Get(
                "materilune.undo.clear_replacements",
                "Clear Materilune Replacements"));
            var changed = false;
            try
            {
                for (var index = 0; index < swapsProperty.arraySize; index++)
                {
                    var toProperty = swapsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("m_to");
                    if (toProperty != null)
                    {
                        toProperty.objectReferenceValue = null;
                    }
                }

                changed = serializedObject.ApplyModifiedProperties();
                if (changed)
                {
                    MarkObjectDirty(serializedObject.targetObject);
                    MateriluneSwapSynchronizer.Sync(manager);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            if (changed)
            {
                Rebuild();
            }
        }

        private void OnUpdateClicked()
        {
            var manager = ResolvedManager;
            var target = manager == null ? null : GetTargetObject(manager);
            if (target == null)
            {
                Rebuild();
                return;
            }

            // Setting up again is what brings the recorded material lists back in line with the
            // meshes, and it creates the operation objects a newly added mesh still lacks. The
            // orphan action is Keep so the run cannot delete anything without being asked.
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MateriluneL10n.Get(
                "materilune.undo.update_entries",
                "Update Materilune Entries"));
            try
            {
                MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Rebuild();
        }

        internal void UpdateEntriesForTests()
        {
            OnUpdateClicked();
        }

        internal bool IsUpdateOfferedForTests()
        {
            return m_updateButton != null
                && m_updateButton.style.visibility.value == Visibility.Visible;
        }

        internal string GetStatusMessageForTests()
        {
            return m_statusMessage == null ? null : m_statusMessage.text;
        }

        private GameObject GetCandidate()
        {
            if (m_useTestTarget)
            {
                return m_testTarget;
            }

            return Selection.activeGameObject;
        }

        private void BindRoot(MateriluneSwapRoot preset)
        {
            m_bindingDepth++;
            try
            {
                ClearListView(m_rootSwapList, m_emptySwapEntries);
                m_rootSerializedObject = null;
                if (preset == null || m_rootSwapList == null || preset.gameObject == null)
                {
                    return;
                }

                m_rootSerializedObject = new SerializedObject(preset);
                var swapsProperty = m_rootSerializedObject.FindProperty("m_swaps");
                if (m_rootSerializedObject.targetObject != null && swapsProperty != null && swapsProperty.isArray)
                {
                    m_rootSwapList.itemsSource = preset.Swaps;
                    m_rootSwapList.Rebuild();
                }
            }
            finally
            {
                m_bindingDepth--;
                UpdateAddButtonStates();
            }
        }

        private void BindOverride(Renderer renderer)
        {
            // The button state follows the binding on every path, including the ones that leave
            // the list empty. Selecting an object without a renderer would otherwise keep the
            // add button enabled from the previous selection.
            m_bindingDepth++;
            try
            {
                ClearListView(m_overrideSwapList, m_emptySwapEntries);
                m_overrideSerializedObject = null;
                var preset = m_activePreset;
                if (m_overrideSwapList == null || preset == null || renderer == null || preset.gameObject == null)
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
                if (m_overrideSerializedObject.targetObject != null && swapsProperty != null && swapsProperty.isArray)
                {
                    m_overrideSwapList.itemsSource = operationOverride.Swaps;
                    m_overrideSwapList.Rebuild();
                }
            }
            finally
            {
                m_bindingDepth--;
                UpdateAddButtonStates();
            }
        }

        private void ConfigureListAndTreeViews()
        {
            // The collection views virtualize by fixed height, so every row height is stated
            // here. A swap row holds an object field on each side and needs more room than a
            // plain label.
            m_presetList.makeItem = MakePresetItem;
            m_presetList.bindItem = BindPresetItem;
            m_presetList.unbindItem = UnbindPresetItem;
            m_presetList.fixedItemHeight = LabelRowHeight;
            m_presetList.selectionType = SelectionType.Single;

            m_rootSwapList.makeItem = MakeSwapItem;
            m_rootSwapList.bindItem = BindRootSwapItem;
            m_rootSwapList.unbindItem = UnbindSwapItem;
            m_rootSwapList.fixedItemHeight = SwapRowHeight;
            m_rootSwapList.selectionType = SelectionType.None;

            m_overrideSwapList.makeItem = MakeSwapItem;
            m_overrideSwapList.bindItem = BindOverrideSwapItem;
            m_overrideSwapList.unbindItem = UnbindSwapItem;
            m_overrideSwapList.fixedItemHeight = SwapRowHeight;
            m_overrideSwapList.selectionType = SelectionType.None;

            m_overrideTree.makeItem = () => new Label();
            m_overrideTree.bindItem = BindTreeItem;
            m_overrideTree.fixedItemHeight = LabelRowHeight;
            m_overrideTree.selectionType = SelectionType.Single;
        }

        private static Label FindSiblingLabel(VisualElement control)
        {
            if (control == null || control.parent == null)
            {
                return null;
            }

            foreach (var child in control.parent.Children())
            {
                if (child is Label label)
                {
                    return label;
                }
            }

            return null;
        }

        private void BindPresetList(MateriluneSwap manager)
        {
            ClearListView(m_presetList, m_emptyPresets);
            if (manager == null || m_presetList == null)
            {
                return;
            }

            var presets = manager.GetPresets();
            m_presetList.itemsSource = presets ?? m_emptyPresets;
            m_presetList.Rebuild();
            ApplyPresetSelection();
        }

        private void BindPresetItem(VisualElement element, int index)
        {
            UnbindPresetItem(element, index);
            var label = element == null ? null : element.Q<Label>("lbl-preset-name");
            var removeButton = element == null ? null : element.Q<Button>("btn-preset-remove");
            if (label == null || removeButton == null)
            {
                return;
            }

            label.text = string.Empty;
            label.RemoveFromClassList(ActivePresetClass);
            removeButton.text = "-";
            removeButton.SetEnabled(false);
            var presets = m_presetList == null ? null : m_presetList.itemsSource;
            if (presets == null || index < 0 || index >= presets.Count)
            {
                return;
            }

            var preset = presets[index] as MateriluneSwapRoot;
            if (preset == null || preset.gameObject == null)
            {
                return;
            }

            label.text = preset.gameObject.name;
            if (preset.gameObject.activeSelf)
            {
                label.AddToClassList(ActivePresetClass);
            }

            var capturedPreset = preset;
            Action removeAction = () => RemovePreset(capturedPreset);
            removeButton.clicked += removeAction;
            removeButton.SetEnabled(presets.Count > 1);
            m_presetRowBindings[element] = new PresetRowBinding(removeButton, removeAction);
            ApplyPresetRowLocalizedText(removeButton);
        }

        private VisualElement MakePresetItem()
        {
            var item = new VisualElement();
            if (m_presetRowTemplate != null)
            {
                m_presetRowTemplate.CloneTree(item);
            }

            return item;
        }

        private void UnbindPresetItem(VisualElement element, int index)
        {
            if (element == null)
            {
                return;
            }

            PresetRowBinding binding;
            if (m_presetRowBindings.TryGetValue(element, out binding))
            {
                binding.RemoveButton.clicked -= binding.RemoveAction;
                m_presetRowBindings.Remove(element);
            }

            var label = element.Q<Label>("lbl-preset-name");
            if (label != null)
            {
                label.text = string.Empty;
                label.RemoveFromClassList(ActivePresetClass);
            }

            var removeButton = element.Q<Button>("btn-preset-remove");
            if (removeButton != null)
            {
                removeButton.text = "-";
                removeButton.SetEnabled(false);
            }
        }

        private void ApplyPresetSelection()
        {
            if (m_presetList == null)
            {
                return;
            }

            var selectedIndex = -1;
            var presets = m_presetList.itemsSource;
            if (presets != null && m_activePreset != null)
            {
                for (var index = 0; index < presets.Count; index++)
                {
                    if (presets[index] == m_activePreset)
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            m_isRestoringPresetSelection = true;
            try
            {
                m_presetList.SetSelectionWithoutNotify(
                    selectedIndex < 0 ? new int[0] : new[] { selectedIndex });
            }
            finally
            {
                m_isRestoringPresetSelection = false;
            }
        }

        private void ActivatePreset(MateriluneSwapRoot preset)
        {
            var manager = ResolvedManager;
            if (manager == null || preset == null || preset.gameObject == null || !ContainsPreset(manager, preset))
            {
                return;
            }

            if (preset.gameObject.activeSelf)
            {
                m_activePreset = preset;
                BindRoot(preset);
                ApplyPresetSelection();
                BindOverride(m_selectedRenderer);
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

                foreach (var currentPreset in manager.GetPresets())
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

                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Rebuild();
        }

        private static bool ContainsPreset(MateriluneSwap manager, MateriluneSwapRoot targetPreset)
        {
            if (manager == null || targetPreset == null)
            {
                return false;
            }

            foreach (var preset in manager.GetPresets())
            {
                if (preset == targetPreset)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnPresetAddClicked()
        {
            var manager = ResolvedManager;
            if (manager == null)
            {
                UpdateAddButtonStates();
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MateriluneL10n.Get(
                "materilune.undo.add_preset",
                "Add Materilune Preset"));
            try
            {
                MateriluneSetupService.AddPreset(manager);
                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Rebuild();
        }

        private void RemovePreset(MateriluneSwapRoot preset)
        {
            var manager = ResolvedManager;
            if (manager == null || preset == null || preset.gameObject == null)
            {
                Rebuild();
                return;
            }

            var presets = manager.GetPresets();
            if (presets == null || presets.Count <= 1 || !ContainsPreset(manager, preset))
            {
                Rebuild();
                return;
            }

            var shouldRemove = EditorUtility.DisplayDialog(
                MateriluneL10n.Get("materilune.ui.window.preset_remove_title", "Remove preset"),
                MateriluneL10n.Get(
                    "materilune.ui.window.preset_remove_message",
                    "Remove this preset and all of its replacement settings?"),
                MateriluneL10n.Get("materilune.setup.orphan.remove", "Remove"),
                MateriluneL10n.Get("materilune.setup.orphan.cancel", "Cancel"));
            if (!shouldRemove)
            {
                return;
            }

            var wasDisplayed = m_activePreset == preset;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MateriluneL10n.Get(
                "materilune.ui.window.preset_remove_title",
                "Remove preset"));
            try
            {
                Undo.DestroyObjectImmediate(preset.gameObject);
                if (wasDisplayed)
                {
                    m_activePreset = null;
                }

                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Rebuild();
        }

        internal void ClearRootReplacementsForTests()
        {
            OnRootClearClicked();
        }

        internal bool IsRootClearOfferedForTests()
        {
            return m_rootClearButton != null && m_rootClearButton.enabledSelf;
        }

        internal void AddPresetForTests()
        {
            OnPresetAddClicked();
        }

        /// <summary>
        /// Builds and binds one preset row outside the list view, so tests can inspect a row
        /// without relying on the layout pass that fills a virtualized list.
        /// </summary>
        /// <param name="index">The preset index to bind.</param>
        /// <returns>The bound row, or <see langword="null" /> when the row cannot be built.</returns>
        internal VisualElement BuildPresetRowForTests(int index)
        {
            var row = MakePresetItem();
            if (row == null)
            {
                return null;
            }

            BindPresetItem(row, index);
            return row;
        }

        private void BindRootSwapItem(VisualElement element, int index)
        {
            BindSwapItem(
                element,
                index,
                m_rootSerializedObject,
                m_activePreset == null ? null : m_activePreset.AvailableMaterials,
                m_activePreset == null ? MateriluneCandidateMode.None : m_activePreset.CandidateMode,
                OnRootViewChanged);
        }

        private void BindOverrideSwapItem(VisualElement element, int index)
        {
            var operationOverride = FindOverride(m_activePreset, m_selectedRenderer);
            BindSwapItem(
                element,
                index,
                m_overrideSerializedObject,
                operationOverride == null ? null : operationOverride.AvailableMaterials,
                operationOverride == null
                    ? MateriluneCandidateMode.None
                    : operationOverride.CandidateMode,
                OnOverrideViewChanged);
        }

        private void BindSwapItem(
            VisualElement element,
            int index,
            SerializedObject serializedObject,
            IReadOnlyList<Material> fromCandidates,
            MateriluneCandidateMode candidateMode,
            Action changedAction)
        {
            ClearSwapRowBinding(element);
            var entryView = element == null ? null : element.Q<MateriluneSwapEntryView>();
            if (entryView == null)
            {
                return;
            }

            entryView.Unbind();
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            serializedObject.Update();
            var swapsProperty = serializedObject.FindProperty("m_swaps");
            if (swapsProperty == null || !swapsProperty.isArray || index < 0 || index >= swapsProperty.arraySize)
            {
                return;
            }

            entryView.Bind(swapsProperty.GetArrayElementAtIndex(index), fromCandidates, candidateMode);
            entryView.Changed += changedAction;
            m_swapRowBindings[element] = new SwapRowBinding(entryView, changedAction);
        }

        private void UnbindSwapItem(VisualElement element, int index)
        {
            ClearSwapRowBinding(element);
        }

        private VisualElement MakeSwapItem()
        {
            var item = new VisualElement();
            if (m_swapEntryRowTemplate == null)
            {
                return item;
            }

            m_swapEntryRowTemplate.CloneTree(item);
            var slot = item.Q<VisualElement>("elm-swap-entry-slot");
            if (slot != null)
            {
                slot.Add(new MateriluneSwapEntryView());
            }

            return item;
        }

        private void ClearSwapRowBinding(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            SwapRowBinding binding;
            if (m_swapRowBindings.TryGetValue(element, out binding))
            {
                binding.EntryView.Changed -= binding.ChangedAction;
                binding.EntryView.Unbind();
                m_swapRowBindings.Remove(element);
                return;
            }

            var entryView = element.Q<MateriluneSwapEntryView>();
            if (entryView != null)
            {
                entryView.Unbind();
            }
        }

        private void ClearPresetRowBindings()
        {
            foreach (var binding in new List<PresetRowBinding>(m_presetRowBindings.Values))
            {
                binding.RemoveButton.clicked -= binding.RemoveAction;
            }

            m_presetRowBindings.Clear();
        }

        private void ClearSwapRowBindings()
        {
            foreach (var binding in new List<SwapRowBinding>(m_swapRowBindings.Values))
            {
                binding.EntryView.Changed -= binding.ChangedAction;
                binding.EntryView.Unbind();
            }

            m_swapRowBindings.Clear();
        }

        private void BindTargetTree(GameObject target)
        {
            if (m_overrideTree == null)
            {
                return;
            }

            m_treeItems.Clear();
            if (target != null && target.transform != null
                && !MateriluneSetupService.IsExcludedObject(target.transform))
            {
                m_treeItems.Add(BuildTreeItem(target.transform));
            }

            m_overrideTree.SetRootItems(m_treeItems);
            m_overrideTree.Rebuild();

            if (m_selectedRenderer != null)
            {
                var selectedTransform = FindTransformForRenderer(target, m_selectedRenderer);
                if (selectedTransform != null)
                {
                    // Tree items are keyed by instance id, so the selection is restored by id.
                    // Restoring it without notifying keeps the rebuild from re-entering the
                    // selection handler that started it.
                    m_overrideTree.SetSelectionByIdWithoutNotify(
                        new[] { selectedTransform.GetInstanceID() });
                }
            }
        }

        private static TreeViewItemData<Transform> BuildTreeItem(Transform transform)
        {
            var children = new List<TreeViewItemData<Transform>>();
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child == null || MateriluneSetupService.IsExcludedObject(child))
                {
                    continue;
                }

                children.Add(BuildTreeItem(child));
            }

            return new TreeViewItemData<Transform>(transform.GetInstanceID(), transform, children);
        }

        private void BindTreeItem(VisualElement element, int index)
        {
            var label = element as Label;
            if (label == null || m_overrideTree == null)
            {
                return;
            }

            var transform = m_overrideTree.GetItemDataForIndex<Transform>(index);
            label.text = transform == null || transform.gameObject == null
                ? string.Empty
                : transform.gameObject.name;
        }

        private static Renderer FindFirstRenderer(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            foreach (var renderer in transform.GetComponents<Renderer>())
            {
                if (renderer != null)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static bool IsRendererInTarget(GameObject target, Renderer renderer)
        {
            return renderer != null && FindTransformForRenderer(target, renderer) != null;
        }

        private static Transform FindTransformForRenderer(GameObject target, Renderer renderer)
        {
            if (target == null || renderer == null)
            {
                return null;
            }

            return FindTransformForRenderer(target.transform, renderer);
        }

        private static Transform FindTransformForRenderer(Transform transform, Renderer renderer)
        {
            if (transform == null || MateriluneSetupService.IsExcludedObject(transform))
            {
                return null;
            }

            foreach (var currentRenderer in transform.GetComponents<Renderer>())
            {
                if (currentRenderer == renderer)
                {
                    return transform;
                }
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                var result = FindTransformForRenderer(transform.GetChild(index), renderer);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static bool IsAvailableLanguage(string languageCode)
        {
            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            if (string.IsNullOrEmpty(languageCode) || availableLanguages == null)
            {
                return false;
            }

            foreach (var availableLanguage in availableLanguages)
            {
                if (availableLanguage == languageCode)
                {
                    return true;
                }
            }

            return false;
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

        private static void MarkObjectDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private void UpdateAddButtonStates()
        {
            var manager = ResolvedManager;
            if (m_presetAddButton != null)
            {
                m_presetAddButton.SetEnabled(manager != null);
            }

            // Enabled only while there is something to clear, so pressing it always does
            // something. Disabling never changes the layout, so nothing moves.
            if (m_rootClearButton != null)
            {
                m_rootClearButton.SetEnabled(HasAnyReplacement(m_rootSerializedObject));
            }

            if (m_overrideClearButton != null)
            {
                m_overrideClearButton.SetEnabled(HasAnyReplacement(m_overrideSerializedObject));
            }
        }

        /// <summary>
        /// Determines whether a component holds at least one entry that names a replacement.
        /// </summary>
        /// <param name="serializedObject">The component to inspect.</param>
        /// <returns><see langword="true" /> when there is something to clear.</returns>
        private static bool HasAnyReplacement(SerializedObject serializedObject)
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return false;
            }

            serializedObject.Update();
            var swapsProperty = serializedObject.FindProperty("m_swaps");
            if (swapsProperty == null || !swapsProperty.isArray)
            {
                return false;
            }

            for (var index = 0; index < swapsProperty.arraySize; index++)
            {
                var toProperty = swapsProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("m_to");
                if (toProperty != null && toProperty.objectReferenceValue != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearListView(ListView listView, System.Collections.IList emptyItems)
        {
            if (listView == null)
            {
                return;
            }

            listView.itemsSource = emptyItems;
            listView.Rebuild();
        }

        private void ApplyLocalizedTexts()
        {
            titleContent = new GUIContent(MateriluneL10n.Get(
                "materilune.ui.window.title",
                "Materilune"));
            if (m_targetField != null)
            {
                m_targetField.label = MateriluneL10n.Get(
                    "materilune.ui.window.target_label",
                    "Target");
            }

            if (m_languageDropdown != null)
            {
                m_languageDropdown.label = MateriluneL10n.Get(
                    "materilune.language.label",
                    "Language");
                RefreshLanguageDropdown();
            }

            if (m_swapButton != null)
            {
                m_swapButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.swap_tab",
                    "Material Swap");
            }

            if (m_presetAddButton != null)
            {
                m_presetAddButton.text = "+";
                m_presetAddButton.tooltip = MateriluneL10n.Get(
                    "materilune.ui.window.preset_add_tooltip",
                    "Add preset");
            }

            ApplyClearButtonText(m_rootClearButton);
            ApplyClearButtonText(m_overrideClearButton);

            if (m_updateButton != null)
            {
                m_updateButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.update_button",
                    "Update");
                m_updateButton.tooltip = MateriluneL10n.Get(
                    "materilune.ui.window.update_tooltip",
                    "Rebuild the replacement entries from the target meshes");
            }

            foreach (var binding in m_presetRowBindings.Values)
            {
                ApplyPresetRowLocalizedText(binding.RemoveButton);
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

            UpdateAddButtonStates();
            RefreshStatusBar();
        }

        private static void ApplyClearButtonText(Button clearButton)
        {
            if (clearButton == null)
            {
                return;
            }

            clearButton.text = MateriluneL10n.Get(
                "materilune.ui.window.clear_replacements_button",
                "Clear replacements");
            clearButton.tooltip = MateriluneL10n.Get(
                "materilune.ui.window.clear_replacements_tooltip",
                "Set every replacement in this panel back to none");
        }

        private static void ApplyPresetRowLocalizedText(Button removeButton)
        {
            if (removeButton == null)
            {
                return;
            }

            removeButton.text = "-";
            removeButton.tooltip = MateriluneL10n.Get(
                "materilune.ui.window.preset_remove_tooltip",
                "Remove this preset");
        }

        private void RefreshLanguageDropdown()
        {
            if (m_languageDropdown == null)
            {
                return;
            }

            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            var choices = availableLanguages == null
                ? new List<string>()
                : new List<string>(availableLanguages);
            m_languageDropdown.choices = choices;
            m_languageDropdown.SetValueWithoutNotify(MateriluneL10n.CurrentLanguageCode);
        }

        private sealed class PresetRowBinding
        {
            internal readonly Button RemoveButton;
            internal readonly Action RemoveAction;

            internal PresetRowBinding(Button removeButton, Action removeAction)
            {
                RemoveButton = removeButton;
                RemoveAction = removeAction;
            }
        }

        private sealed class SwapRowBinding
        {
            internal readonly MateriluneSwapEntryView EntryView;
            internal readonly Action ChangedAction;

            internal SwapRowBinding(MateriluneSwapEntryView entryView, Action changedAction)
            {
                EntryView = entryView;
                ChangedAction = changedAction;
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
