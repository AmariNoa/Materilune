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
        private Button m_rootBatchButton;
        private Button m_presetExportButton;
        private Button m_presetImportButton;
        private TextField m_activeRenameField;
        private bool m_rebuildQueued;
        private Button m_overrideBatchButton;
        private Button m_overrideClearButton;
        private VisualElement m_statusBar;
        private Label m_statusMessage;
        private Button m_updateButton;
        private Button m_fixOrderButton;
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
        public static void ShowWindow()
        {
            GetOrCreateWindow();
        }

        /// <summary>
        /// Shows the Materilune window on a given object.
        /// </summary>
        /// <param name="target">The object to edit, or <see langword="null" /> to keep the
        /// current selection.</param>
        /// <remarks>
        /// The window takes its target from the hierarchy selection, and a button drawn on a
        /// hierarchy row does not select that row when it is pressed. Without this the window
        /// would open on whatever was selected beforehand, which for nested setups is the outer
        /// one rather than the row the button belongs to.
        /// </remarks>
        public static void ShowWindow(GameObject target)
        {
            if (target == null)
            {
                GetOrCreateWindow();
                return;
            }

            Selection.activeGameObject = target;
            GetOrCreateWindow().RefreshForSelection();
        }

        /// <summary>
        /// Rebuilds the window from the current hierarchy selection.
        /// </summary>
        /// <remarks>
        /// Assigning the selection does not raise Selection.selectionChanged straight away; the
        /// editor raises it on its next update. An already open window would therefore keep
        /// showing the previous target for a moment, and in a test it would never move at all.
        /// </remarks>
        private void RefreshForSelection()
        {
            m_useTestTarget = false;
            m_testTarget = null;
            if (!m_uiReady)
            {
                CreateGUI();
            }

            Rebuild();
        }

        /// <summary>
        /// Opens the window through the same path used by the Hierarchy button.
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
                m_uiReady = false;
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

            // The stylesheet is referenced by the uxml itself, so what the UI Builder
            // previews is exactly what runs; the code attaches nothing.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            m_presetRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PresetRowUxmlPath);
            m_swapEntryRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SwapEntryRowUxmlPath);
            if (visualTree == null
                || m_presetRowTemplate == null
                || m_swapEntryRowTemplate == null)
            {
                LogLoadError();
                return;
            }

            visualTree.CloneTree(rootVisualElement);
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
            rootVisualElement.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            ConfigureListAndTreeViews();
            m_languageDropdown.RegisterValueChangedCallback(OnLanguageValueChanged);

            // The box renders whatever this returns, whatever the field's value happens to
            // hold. Every failure seen so far has been the display and not the setting, so the
            // display is bound straight to the library's answer and stops being able to lie:
            // a stale placeholder or a mid-dispatch rewrite still ends up drawn as the code
            // that is actually in effect.
            m_languageDropdown.formatSelectedValueCallback = _ =>
            {
                var currentCode = MateriluneL10n.CurrentLanguageCode;
                return string.IsNullOrEmpty(currentCode) ? string.Empty : currentCode;
            };
            m_presetList.selectionChanged += OnPresetSelectionChanged;
            m_presetList.itemIndexChanged += OnPresetReordered;
            m_overrideTree.selectionChanged += OnTreeSelectionChanged;
            m_presetAddButton.clicked += OnPresetAddClicked;
            if (m_fixOrderButton != null)
            {
                m_fixOrderButton.clicked += OnFixOrderClicked;
            }

            if (m_updateButton != null)
            {
                m_updateButton.clicked += OnUpdateClicked;
            }

            if (m_rootBatchButton != null)
            {
                m_rootBatchButton.clicked += OnRootBatchClicked;
            }

            if (m_presetExportButton != null)
            {
                m_presetExportButton.clicked += OnPresetExportClicked;
            }

            if (m_presetImportButton != null)
            {
                m_presetImportButton.clicked += OnPresetImportClicked;
            }

            if (m_overrideBatchButton != null)
            {
                m_overrideBatchButton.clicked += OnOverrideBatchClicked;
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
            m_fixOrderButton = rootVisualElement.Q<Button>("btn-fix-order");
            m_rootClearButton = rootVisualElement.Q<Button>("btn-clear-swap-root-entries");
            m_rootBatchButton = rootVisualElement.Q<Button>("btn-batch-swap-settings");
            m_overrideBatchButton = rootVisualElement.Q<Button>("btn-batch-swap-settings-mesh");
            m_overrideClearButton =
                rootVisualElement.Q<Button>("btn-clear-swap-override-entries");
            m_presetList = rootVisualElement.Q<ListView>("lv-preset-list");
            m_presetExportButton = rootVisualElement.Q<Button>("btn-preset-export");
            m_presetImportButton = rootVisualElement.Q<Button>("btn-preset-import");
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
            m_fixOrderButton = null;
            m_rootClearButton = null;
            m_rootBatchButton = null;
            m_presetExportButton = null;
            m_presetImportButton = null;
            m_overrideBatchButton = null;
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
            rootVisualElement.UnregisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            m_activeRenameField = null;

            if (m_languageDropdown != null)
            {
                m_languageDropdown.UnregisterValueChangedCallback(OnLanguageValueChanged);
            }

            if (m_presetList != null)
            {
                m_presetList.selectionChanged -= OnPresetSelectionChanged;
                m_presetList.itemIndexChanged -= OnPresetReordered;
            }

            if (m_overrideTree != null)
            {
                m_overrideTree.selectionChanged -= OnTreeSelectionChanged;
            }

            if (m_presetAddButton != null)
            {
                m_presetAddButton.clicked -= OnPresetAddClicked;
            }

            if (m_fixOrderButton != null)
            {
                m_fixOrderButton.clicked -= OnFixOrderClicked;
            }

            if (m_updateButton != null)
            {
                m_updateButton.clicked -= OnUpdateClicked;
            }

            if (m_rootBatchButton != null)
            {
                m_rootBatchButton.clicked -= OnRootBatchClicked;
            }

            if (m_presetExportButton != null)
            {
                m_presetExportButton.clicked -= OnPresetExportClicked;
            }

            if (m_presetImportButton != null)
            {
                m_presetImportButton.clicked -= OnPresetImportClicked;
            }

            if (m_overrideBatchButton != null)
            {
                m_overrideBatchButton.clicked -= OnOverrideBatchClicked;
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
            if (!m_uiReady || m_rebuildQueued)
            {
                return;
            }

            // Coalesced: a drag-reorder or a setup writes the hierarchy several times in one
            // tick, and each write used to buy its own full rebuild. One deferred rebuild
            // serves them all; the flag drops the duplicates until it runs.
            m_rebuildQueued = true;
            rootVisualElement.schedule.Execute(() =>
            {
                m_rebuildQueued = false;
                if (m_uiReady)
                {
                    Rebuild();
                }
            });
        }

        private void OnLanguageChanged(string languageCode)
        {
            if (!m_uiReady)
            {
                return;
            }

            // Deferred a tick on purpose. The library raises this synchronously from inside
            // SetLanguage, which the dropdown's own change event is still dispatching; redoing
            // the dropdown's label, choices and value in the middle of that dispatch is what
            // let the label text bleed into the value display.
            rootVisualElement.schedule.Execute(() =>
            {
                if (m_uiReady)
                {
                    ApplyLocalizedTexts();
                }
            });
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

        /// <summary>
        /// Writes a drag-reorder of the preset list back into the scene.
        /// </summary>
        /// <remarks>
        /// The list only reorders its own item source, a copy; the scene's order is the
        /// sibling order under the manager, and that is what everything reads. The whole new
        /// order is applied rather than the one move, which keeps the write simple and lands
        /// in a single undo step. Order between presets carries no behavioral weight, since
        /// only one preset is ever active, so this is purely the user's arrangement.
        /// </remarks>
        private void OnPresetReordered(int fromIndex, int toIndex)
        {
            var presets = m_presetList == null ? null : m_presetList.itemsSource;
            if (presets == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            var undoLabel = MateriluneL10n.Get(
                "materilune.undo.reorder_preset",
                "Reorder Materilune Presets");
            Undo.SetCurrentGroupName(undoLabel);
            try
            {
                for (var index = 0; index < presets.Count; index++)
                {
                    var preset = presets[index] as MateriluneSwapRoot;
                    if (preset == null || preset.gameObject == null)
                    {
                        continue;
                    }

                    if (preset.transform.GetSiblingIndex() != index)
                    {
                        Undo.SetSiblingIndex(preset.transform, index, undoLabel);
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            // No rebuild here. The list already shows the new order, and the sibling writes
            // above raise hierarchyChanged, which schedules one rebuild anyway; doing another
            // synchronously inside the drop is what made the drop hitch. Only the selection
            // highlight needs re-deriving, since the displayed preset changed row.
            ApplyPresetSelection();
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
                DisplayPreset(selectedPreset);
                return;
            }

            // The list can lose its selection without anyone choosing a preset: a click on
            // the empty area below the rows, a ctrl-click on the selected row, Escape. The
            // window still shows and edits the same preset, so a list claiming that nothing
            // is chosen would be lying; the selection is put back on what is displayed.
            ApplyPresetSelection();
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
            var orderBroken = target != null && !MateriluneMarkerOrdering.IsOrderGuaranteed(FindMarker(manager));

            if (m_statusMessage != null)
            {
                m_statusMessage.text = GetStatusMessage(manager, target, needsUpdate, orderBroken);
            }

            m_statusBar.EnableInClassList(StatusWarningClass, needsUpdate || orderBroken);
            // These two are the one place in the window where a control is taken out of the
            // layout rather than just hidden. Keeping their space reserved leaves a gap at the
            // right end of the bar whenever only one of them applies, and the buttons are
            // wanted flush against that end (2026-08-16 の指示). They sit at the far right,
            // past everything else, so what moves when one appears is the other button and the
            // length of the message, not any control the row is built around.
            if (m_updateButton != null)
            {
                m_updateButton.style.display = needsUpdate ? DisplayStyle.Flex : DisplayStyle.None;
                m_updateButton.SetEnabled(needsUpdate);
            }

            if (m_fixOrderButton != null)
            {
                m_fixOrderButton.style.display = orderBroken ? DisplayStyle.Flex : DisplayStyle.None;
                m_fixOrderButton.SetEnabled(orderBroken);
            }
        }

        /// <summary>
        /// Finds the marker of a setup from its manager.
        /// </summary>
        /// <param name="manager">The manager of the setup.</param>
        /// <returns>The marker, or <see langword="null" /> when the manager has none.</returns>
        /// <remarks>
        /// The manager is placed directly under the marker by setup, so the parent is it. The
        /// component is what identifies it, never the object's name.
        /// </remarks>
        private static Materilune FindMarker(MateriluneSwap manager)
        {
            var parent = manager == null ? null : manager.transform.parent;
            return parent == null ? null : parent.GetComponent<Materilune>();
        }

        private string GetStatusMessage(
            MateriluneSwap manager,
            GameObject target,
            bool needsUpdate,
            bool orderBroken)
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

            // A broken order is reported ahead of everything else. While it holds, the
            // settings shown here are not the ones the avatar wears, which makes any other
            // message on this line misleading.
            if (orderBroken)
            {
                return string.Format(
                    MateriluneL10n.Get(
                        "materilune.ui.window.status_order_not_guaranteed",
                        "A Materilune setup nested in this object is being overridden by this one. "
                        + "Move the Materilune object to the top of the children of {0}."),
                    target.name);
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

        /// <summary>
        /// Writes the displayed preset to a .mlsp file the user picks.
        /// </summary>
        private void OnPresetExportClicked()
        {
            var preset = m_activePreset;
            if (preset == null || preset.gameObject == null)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel(
                MateriluneL10n.Get("materilune.ui.window.preset_export_title", "Export Materilune preset"),
                string.Empty,
                preset.gameObject.name + "." + MaterilunePresetFile.Extension,
                MaterilunePresetFile.Extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                System.IO.File.WriteAllText(path, MaterilunePresetFile.ExportToJson(preset));
            }
            catch (Exception exception) when (exception is System.IO.IOException
                || exception is UnauthorizedAccessException)
            {
                // The exception text stays in the log for diagnosis; the dialog speaks the
                // user's language and points at the thing they control, the chosen location.
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    MateriluneL10n.Get("materilune.ui.window.preset_export_title", "Export Materilune preset"),
                    MateriluneL10n.Get(
                        "materilune.ui.window.preset_export_failed",
                        "The file could not be written. Check the chosen location."),
                    MateriluneL10n.Get("materilune.ui.window.preset_import_close", "Close"));
                return;
            }

            // A file saved into the project should appear without a manual refresh.
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Brings a .mlsp file in as a new preset and reports what came of it.
        /// </summary>
        private void OnPresetImportClicked()
        {
            var manager = ResolvedManager;
            if (manager == null)
            {
                return;
            }

            var path = EditorUtility.OpenFilePanel(
                MateriluneL10n.Get("materilune.ui.window.preset_import_title", "Import Materilune preset"),
                string.Empty,
                MaterilunePresetFile.Extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MaterilunePresetImportResult result;
            try
            {
                result = MaterilunePresetFile.ImportFromJson(manager, System.IO.File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is System.IO.IOException
                || exception is UnauthorizedAccessException)
            {
                // The raw message is English by construction, so the dialog carries the
                // translated explanation and the log carries the specifics.
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    MateriluneL10n.Get("materilune.ui.window.preset_import_title", "Import Materilune preset"),
                    MateriluneL10n.Get(
                        "materilune.ui.window.preset_import_invalid",
                        "The file could not be read as a Materilune swap preset."),
                    MateriluneL10n.Get("materilune.ui.window.preset_import_close", "Close"));
                return;
            }

            var report = new System.Text.StringBuilder();
            report.AppendFormat(
                MateriluneL10n.Get(
                    "materilune.ui.window.preset_import_applied",
                    "{0} replacement(s) were applied."),
                result.AppliedCount);
            foreach (var missing in result.MissingMaterials)
            {
                report.AppendLine();
                report.AppendFormat(
                    MateriluneL10n.Get(
                        "materilune.ui.window.preset_import_missing",
                        "Material not found: {0}"),
                    missing);
            }

            foreach (var unmatched in result.UnmatchedOverrides)
            {
                report.AppendLine();
                report.AppendFormat(
                    MateriluneL10n.Get(
                        "materilune.ui.window.preset_import_unmatched",
                        "No mesh matched: {0}"),
                    unmatched);
            }

            EditorUtility.DisplayDialog(
                MateriluneL10n.Get("materilune.ui.window.preset_import_title", "Import Materilune preset"),
                report.ToString(),
                MateriluneL10n.Get("materilune.ui.window.preset_import_close", "Close"));
            Rebuild();
        }

        private void OnRootBatchClicked()
        {
            var preset = m_activePreset;
            OpenBatchSwap(
                m_rootSerializedObject,
                preset == null ? null : preset.Swaps,
                preset == null ? MateriluneCandidateMode.None : preset.CandidateMode);
        }

        private void OnOverrideBatchClicked()
        {
            var operationOverride = FindOverride(m_activePreset, m_selectedRenderer);
            OpenBatchSwap(
                m_overrideSerializedObject,
                operationOverride == null ? null : operationOverride.Swaps,
                operationOverride == null
                    ? MateriluneCandidateMode.None
                    : operationOverride.CandidateMode);
        }

        /// <summary>
        /// Opens the batch replacement window for one panel.
        /// </summary>
        /// <param name="serializedObject">The component the panel edits.</param>
        /// <param name="entries">The entries of that component.</param>
        /// <param name="mode">The candidate discovery mode of that component.</param>
        /// <remarks>
        /// The window decides nothing on its own; it hands back the rows the user approved and
        /// those are written here, where the serialized object and the undo group live.
        /// </remarks>
        private void OpenBatchSwap(
            SerializedObject serializedObject,
            IReadOnlyList<MateriluneMaterialSwapEntry> entries,
            MateriluneCandidateMode mode)
        {
            if (serializedObject == null || serializedObject.targetObject == null || entries == null)
            {
                return;
            }

            // The manager is captured now, not read again when the window comes back:
            // the selection may have moved to another setup in the meantime, and syncing
            // that one while writing to this one would tear the two apart.
            var manager = ResolvedManager;
            MateriluneBatchSwapWindow.Open(
                entries,
                mode,
                approved => ApplyBatchSwap(serializedObject, manager, approved));
        }

        private void ApplyBatchSwap(
            SerializedObject serializedObject,
            MateriluneSwap manager,
            IReadOnlyList<MateriluneBatchSwapPlanItem> approved)
        {
            if (manager == null
                || serializedObject == null
                || serializedObject.targetObject == null
                || approved == null
                || approved.Count == 0)
            {
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
                "materilune.undo.batch_swap",
                "Batch Swap Materilune Replacements"));
            var changed = false;
            try
            {
                foreach (var item in approved)
                {
                    // item.From is checked through Unity's lifetime-aware null too: a source
                    // destroyed since planning would compare equal to a stale property value
                    // by fake-null coincidence, and the row would be written on a dead match.
                    if (item == null
                        || item.From == null
                        || item.Index < 0
                        || item.Index >= swapsProperty.arraySize)
                    {
                        continue;
                    }

                    // The source is checked again rather than trusted: the window is not modal
                    // to the scene, so the entries could have been rebuilt while it was open.
                    var entryProperty = swapsProperty.GetArrayElementAtIndex(item.Index);
                    var fromProperty = entryProperty.FindPropertyRelative("m_from");
                    var toProperty = entryProperty.FindPropertyRelative("m_to");
                    if (fromProperty == null
                        || toProperty == null
                        || fromProperty.objectReferenceValue != item.From)
                    {
                        continue;
                    }

                    toProperty.objectReferenceValue = item.To;
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

        /// <summary>
        /// Moves the setup's marker up until the nested setups below it are reached later.
        /// </summary>
        /// <remarks>
        /// Setup already tries this, so the button exists for the state left behind when the
        /// scene refused the move at the time: once the obstacle is gone, pressing it fixes the
        /// order without setting the whole hierarchy up again. It only reorders siblings, and a
        /// scene that still refuses keeps the warning rather than being forced.
        /// </remarks>
        private void OnFixOrderClicked()
        {
            var manager = ResolvedManager;
            var marker = FindMarker(manager);
            if (marker == null)
            {
                Rebuild();
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MateriluneL10n.Get(
                "materilune.undo.fix_order",
                "Fix Materilune Order"));
            try
            {
                MateriluneMarkerOrdering.MoveAsFarUpAsPossible(marker);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            if (!MateriluneMarkerOrdering.IsOrderGuaranteed(marker))
            {
                Debug.LogWarning(string.Format(
                    MateriluneL10n.Get(
                        "materilune.setup.order_not_guaranteed",
                        "Materilune could not move its object to the front of {0}. A Materilune setup "
                        + "nested inside an object listed before it will not take effect. Move the "
                        + "Materilune object to the top of the children of {0} by hand."),
                    marker.transform.parent == null ? string.Empty : marker.transform.parent.name));
            }

            Rebuild();
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
                && m_updateButton.style.display.value == DisplayStyle.Flex;
        }

        internal bool IsOrderFixOfferedForTests()
        {
            return m_fixOrderButton != null
                && m_fixOrderButton.style.display.value == DisplayStyle.Flex;
        }

        internal void FixOrderForTests()
        {
            OnFixOrderClicked();
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

            // Reordering by drag handle. The handle keeps drags apart from everything else a
            // row already answers to: the radio, the remove button and the rename dblclick.
            m_presetList.reorderable = true;
            m_presetList.reorderMode = ListViewReorderMode.Animated;

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
            var activeToggle = element == null ? null : element.Q<RadioButton>("tgl-preset-active");
            var nameField = element == null ? null : element.Q<TextField>("txt-preset-name");
            var nameSlot = element == null ? null : element.Q<VisualElement>("elm-preset-name-slot");
            if (label == null || removeButton == null || activeToggle == null || nameField == null || nameSlot == null)
            {
                return;
            }

            label.text = string.Empty;
            label.RemoveFromClassList(ActivePresetClass);
            removeButton.text = "-";
            removeButton.SetEnabled(false);
            activeToggle.SetValueWithoutNotify(false);
            activeToggle.SetEnabled(false);
            nameField.visible = false;
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

            // The toggle activates only. Toggling the active preset off would leave nothing
            // applied through a single misclick, so switching everything off stays a hierarchy
            // operation. The change event fires for that click too, and putting the tick back
            // is what keeps the control honest about the state it shows.
            activeToggle.SetValueWithoutNotify(preset.gameObject.activeSelf);
            activeToggle.SetEnabled(true);

            EventCallback<ChangeEvent<bool>> activeChanged = changeEvent =>
            {
                if (changeEvent.newValue)
                {
                    ActivatePreset(capturedPreset);
                    return;
                }

                // Radios cannot be clicked off, so this only fires when something else turns
                // the control off behind our back. The rows are re-read from the scene rather
                // than the one control patched: fighting a group with SetValueWithoutNotify is
                // what desynchronized the display from the actual active preset before.
                m_presetList?.RefreshItems();
            };
            activeToggle.RegisterValueChangedCallback(activeChanged);

            EventCallback<ClickEvent> nameSlotClicked;
            EventCallback<ChangeEvent<string>> nameChanged;
            EventCallback<KeyDownEvent> nameKeyDown;
            EventCallback<FocusOutEvent> nameFocusOut;
            ConfigurePresetRename(
                nameSlot,
                label,
                nameField,
                capturedPreset,
                out nameSlotClicked,
                out nameChanged,
                out nameKeyDown,
                out nameFocusOut);
            m_presetRowBindings[element] = new PresetRowBinding(
                removeButton,
                removeAction,
                activeToggle,
                activeChanged,
                nameSlot,
                nameField,
                nameSlotClicked,
                nameChanged,
                nameKeyDown,
                nameFocusOut);
            ApplyPresetRowLocalizedText(removeButton, activeToggle);
        }

        /// <summary>
        /// Ends a rename when the pointer goes down anywhere outside the field.
        /// </summary>
        /// <remarks>
        /// Focus alone cannot be relied on for this: clicking a plain label or an empty area
        /// moves no focus in UI Toolkit, so the field would stay open. The pointer is watched
        /// at the window root during the capture phase instead, and a press outside the field
        /// blurs it, which commits a changed value through the delayed change event before the
        /// field is put away.
        /// </remarks>
        private void OnRootPointerDown(PointerDownEvent pointerEvent)
        {
            var field = m_activeRenameField;
            if (field == null)
            {
                return;
            }

            if (!field.visible)
            {
                m_activeRenameField = null;
                return;
            }

            if (field.worldBound.Contains((Vector2)pointerEvent.position))
            {
                return;
            }

            field.Blur();
            field.visible = false;
            m_activeRenameField = null;
        }

        /// <summary>
        /// Wires the in-place rename: double-click to edit, Enter to keep, Esc to drop.
        /// </summary>
        /// <remarks>
        /// The field lies over the label and is hidden with visibility, so starting and ending
        /// an edit moves nothing. Only the object's name changes; nothing matches on names, so
        /// the rename cannot break a reference. A name that is empty once trimmed is treated
        /// as a cancel rather than written, since a nameless row cannot be told apart.
        /// </remarks>
        private void ConfigurePresetRename(
            VisualElement nameSlot,
            Label label,
            TextField nameField,
            MateriluneSwapRoot preset,
            out EventCallback<ClickEvent> nameSlotClicked,
            out EventCallback<ChangeEvent<string>> nameChanged,
            out EventCallback<KeyDownEvent> nameKeyDown,
            out EventCallback<FocusOutEvent> nameFocusOut)
        {
            nameField.isDelayed = true;

            // Only the name area opens the edit. The whole row would also catch double clicks
            // bubbling out of the toggle and the remove button.
            nameSlotClicked = clickEvent =>
            {
                if (clickEvent.clickCount != 2 || preset == null || preset.gameObject == null)
                {
                    return;
                }

                nameField.SetValueWithoutNotify(preset.gameObject.name);
                nameField.visible = true;
                m_activeRenameField = nameField;

                // No select-all here. Programmatic focus and text selection race each other in
                // this editor version and the attempts to sequence them did not hold up, so the
                // edit simply opens with a caret (2026-08-17 の判断で自動選択は見送り).
                nameField.Focus();
            };
            nameSlot.RegisterCallback(nameSlotClicked);

            // isDelayed makes the change event fire on Enter or focus loss, not per keystroke.
            nameChanged = changeEvent =>
            {
                nameField.visible = false;
                var newName = changeEvent.newValue == null ? string.Empty : changeEvent.newValue.Trim();
                if (preset == null
                    || preset.gameObject == null
                    || newName.Length == 0
                    || newName == preset.gameObject.name)
                {
                    return;
                }

                Undo.RecordObject(preset.gameObject, MateriluneL10n.Get(
                    "materilune.undo.rename_preset",
                    "Rename Materilune Preset"));
                preset.gameObject.name = newName;
                label.text = newName;
            };
            nameField.RegisterValueChangedCallback(nameChanged);

            nameKeyDown = keyEvent =>
            {
                if (keyEvent.keyCode == KeyCode.Escape)
                {
                    nameField.visible = false;
                }
            };
            nameField.RegisterCallback(nameKeyDown);

            // Clicking anywhere else ends the edit too. The delayed change event only fires
            // when the value differs, so an untouched field would otherwise sit open over the
            // label until Enter or Escape was pressed inside it.
            nameFocusOut = _ => nameField.visible = false;
            nameField.RegisterCallback(nameFocusOut);
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
                binding.Unregister();
                if (m_activeRenameField != null && m_activeRenameField == binding.NameField)
                {
                    m_activeRenameField.visible = false;
                    m_activeRenameField = null;
                }

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
                    // Reference identity on purpose: the row shows the same instance the
                    // window holds, and the compiler is told so to silence CS0252.
                    if (ReferenceEquals(presets[index], m_activePreset))
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

        /// <summary>
        /// Shows a preset in the editing panels without touching its active state.
        /// </summary>
        /// <param name="preset">The preset to edit.</param>
        /// <remarks>
        /// Selecting a row used to activate the preset as a side effect. With the activation
        /// toggle in the row, the two are separate: the row picks what is edited, the toggle
        /// picks what the avatar wears, and an inactive preset can be worked on in peace.
        /// </remarks>
        private void DisplayPreset(MateriluneSwapRoot preset)
        {
            var manager = ResolvedManager;
            if (manager == null || preset == null || preset.gameObject == null || !ContainsPreset(manager, preset))
            {
                return;
            }

            m_activePreset = preset;
            BindRoot(preset);
            ApplyPresetSelection();
            BindOverride(m_selectedRenderer);
        }

        /// <summary>
        /// Makes one preset the active one, putting every other preset out itself.
        /// </summary>
        /// <param name="preset">The preset to activate.</param>
        /// <remarks>
        /// The whole change lands in one undo group: the activation, the deactivations and the
        /// prefab bookkeeping, so one undo restores the previous arrangement entire.
        /// </remarks>
        private void ActivatePreset(MateriluneSwapRoot preset)
        {
            var manager = ResolvedManager;
            if (manager == null || preset == null || preset.gameObject == null || !ContainsPreset(manager, preset))
            {
                return;
            }

            if (preset.gameObject.activeSelf)
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

            // The argument order puts Cancel in the primary (left) slot, per the requested
            // layout (2026-08-17 の指示: 左を中止、右を除去). DisplayDialog answers true for
            // that first button, so the answer is inverted to keep meaning "remove".
            var shouldRemove = !EditorUtility.DisplayDialog(
                MateriluneL10n.Get("materilune.ui.window.preset_remove_title", "Remove preset"),
                MateriluneL10n.Get(
                    "materilune.ui.window.preset_remove_message",
                    "Remove this preset and all of its replacement settings?"),
                MateriluneL10n.Get("materilune.setup.orphan.cancel", "Cancel"),
                MateriluneL10n.Get("materilune.setup.orphan.remove", "Remove"));
            if (!shouldRemove)
            {
                return;
            }

            RemovePresetCore(manager, preset);
        }

        /// <summary>
        /// Removes a preset and, when it was the active one, hands its duty to another.
        /// </summary>
        /// <param name="manager">The manager the preset belongs to.</param>
        /// <param name="preset">The preset to remove.</param>
        /// <remarks>
        /// Removing the active preset used to leave every remaining preset off, which read as
        /// nothing being selected at all (2026-08-17 の指示で自動アクティブ化に変更, T35-Q2).
        /// The replacement is only activated when the removed preset was active: removing an
        /// inactive one changes nothing about what the avatar wears, and the removal plus the
        /// hand-over land in one undo group so a single undo restores the old arrangement.
        /// </remarks>
        private void RemovePresetCore(MateriluneSwap manager, MateriluneSwapRoot preset)
        {
            var wasDisplayed = m_activePreset == preset;
            var wasActive = preset.gameObject.activeSelf;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            var undoLabel = MateriluneL10n.Get(
                "materilune.ui.window.preset_remove_title",
                "Remove preset");
            Undo.SetCurrentGroupName(undoLabel);
            try
            {
                Undo.DestroyObjectImmediate(preset.gameObject);
                if (wasDisplayed)
                {
                    m_activePreset = null;
                }

                if (wasActive)
                {
                    var fallback = m_activePreset != null && m_activePreset.gameObject != null
                        ? m_activePreset
                        : FirstPreset(manager);
                    if (fallback != null && !fallback.gameObject.activeSelf)
                    {
                        Undo.RecordObject(fallback.gameObject, undoLabel);
                        fallback.gameObject.SetActive(true);
                        EditorUtility.SetDirty(fallback.gameObject);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(fallback.gameObject);
                    }
                }

                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            Rebuild();
        }

        private static MateriluneSwapRoot FirstPreset(MateriluneSwap manager)
        {
            foreach (var preset in manager.GetPresets())
            {
                if (preset != null && preset.gameObject != null)
                {
                    return preset;
                }
            }

            return null;
        }

        internal void ReorderPresetForTests(int fromIndex, int toIndex)
        {
            OnPresetReordered(fromIndex, toIndex);
        }

        internal void RemovePresetForTests(MateriluneSwapRoot preset)
        {
            var manager = ResolvedManager;
            if (manager == null || preset == null)
            {
                return;
            }

            RemovePresetCore(manager, preset);
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
        internal void ActivatePresetForTests(MateriluneSwapRoot preset)
        {
            ActivatePreset(preset);
        }

        internal void RebindPresetRowForTests(VisualElement row, int index)
        {
            BindPresetItem(row, index);
        }

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
            var preset = m_activePreset;
            BindSwapItem(
                element,
                index,
                m_rootSerializedObject,
                preset == null ? null : preset.AvailableMaterials,
                preset == null ? MateriluneCandidateMode.None : preset.CandidateMode,
                OnRootViewChanged,
                from => MateriluneInheritedSwaps.ResolveForRoot(preset, from));
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
                OnOverrideViewChanged,
                from => MateriluneInheritedSwaps.ResolveForOverride(operationOverride, from));
        }

        private void BindSwapItem(
            VisualElement element,
            int index,
            SerializedObject serializedObject,
            IReadOnlyList<Material> fromCandidates,
            MateriluneCandidateMode candidateMode,
            Action changedAction,
            Func<Material, Material> resolveInherited)
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

            var entryProperty = swapsProperty.GetArrayElementAtIndex(index);
            entryView.Bind(entryProperty, fromCandidates, candidateMode);

            // What an enclosing setup applies to this material, so a row left empty here still
            // shows what the avatar will actually wear.
            var fromProperty = entryProperty.FindPropertyRelative("m_from");
            var from = fromProperty == null ? null : fromProperty.objectReferenceValue as Material;
            entryView.SetInheritedReplacement(resolveInherited == null ? null : resolveInherited(from));
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
                binding.Unregister();
            }

            m_presetRowBindings.Clear();
            m_activeRenameField = null;
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

            // The root row starts open (2026-08-17 の指示). A tree whose only visible row is a
            // collapsed root reads as empty, and the first click every time was always to open
            // it. Children below keep whatever fold state the user gave them.
            if (target != null && target.transform != null && m_treeItems.Count > 0)
            {
                m_overrideTree.ExpandItem(target.transform.GetInstanceID());
            }

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
                if (child == null
                    || MateriluneSetupService.IsExcludedObject(child)
                    || !CarriesAnyRenderer(child))
                {
                    continue;
                }

                children.Add(BuildTreeItem(child));
            }

            return new TreeViewItemData<Transform>(transform.GetInstanceID(), transform, children);
        }

        /// <summary>
        /// Tells whether a subtree holds any renderer at all.
        /// </summary>
        /// <param name="transform">The root of the subtree.</param>
        /// <returns><see langword="true" /> when a renderer exists on or below it.</returns>
        /// <remarks>
        /// The tree exists to pick a mesh, so a branch that cannot end in one is dead weight:
        /// armature bones, colliders, anchor points (2026-08-17 の指示で非表示に). The excluded
        /// operation hierarchy does not count; a branch whose only renderers belong to it
        /// would otherwise stay visible with nothing selectable inside.
        /// </remarks>
        private static bool CarriesAnyRenderer(Transform transform)
        {
            if (transform.GetComponent<Renderer>() != null)
            {
                return true;
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child == null || MateriluneSetupService.IsExcludedObject(child))
                {
                    continue;
                }

                if (CarriesAnyRenderer(child))
                {
                    return true;
                }
            }

            return false;
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

            var activePreset = FindActivePreset(manager);
            if (activePreset != null)
            {
                return activePreset;
            }

            // No active preset is a legitimate arrangement, but a window with presets and no
            // selection is not: removing the shown preset or importing an inactive one would
            // otherwise leave every panel empty. The first preset stands in.
            foreach (var preset in manager.GetPresets())
            {
                if (preset != null)
                {
                    return preset;
                }
            }

            return null;
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

                // The note rides as a tooltip (2026-08-17 の判断 T37-Q2/Q4): the setting is
                // not Materilune's own, and a switch here follows every tool built on the
                // same localization core.
                m_languageDropdown.tooltip = MateriluneL10n.Get(
                    "materilune.language.shared_note",
                    "This language setting is shared between tools that use Unity Editor Localization Core.");
                RefreshLanguageDropdown();
            }

            if (m_swapButton != null)
            {
                // The tab it opens is the tab on screen: there is only one page today, so the
                // button is always the chosen one and always disabled (2026-08-17 の指示で
                // 表示中タブは選択済み = 操作不能の表示). When a second page arrives, this
                // becomes per-tab state toggled by whichever tab is shown.
                m_swapButton.SetEnabled(false);
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
            ApplyBatchButtonText(m_rootBatchButton);
            ApplyBatchButtonText(m_overrideBatchButton);

            if (m_presetExportButton != null)
            {
                m_presetExportButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.preset_export_button",
                    "Export");
            }

            if (m_presetImportButton != null)
            {
                m_presetImportButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.preset_import_button",
                    "Import");
            }

            if (m_fixOrderButton != null)
            {
                m_fixOrderButton.text = MateriluneL10n.Get(
                    "materilune.ui.window.fix_order_label",
                    "Fix order");
                m_fixOrderButton.tooltip = MateriluneL10n.Get(
                    "materilune.ui.window.fix_order_tooltip",
                    "Move the Materilune object above the nested setups so they take effect.");
            }

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
                ApplyPresetRowLocalizedText(binding.RemoveButton, binding.ActiveToggle);
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

        private static void ApplyBatchButtonText(Button batchButton)
        {
            if (batchButton == null)
            {
                return;
            }

            batchButton.text = MateriluneL10n.Get(
                "materilune.ui.window.batch_swap_button",
                "Batch swap");
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

        private static void ApplyPresetRowLocalizedText(Button removeButton, RadioButton activeToggle)
        {
            if (activeToggle != null)
            {
                activeToggle.tooltip = MateriluneL10n.Get(
                    "materilune.ui.window.preset_active_tooltip",
                    "Make this the active preset");
            }

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

            // Reassigned only when the set actually changed: swapping the list makes the
            // field rebuild its text, and that churn is wasted on every localization pass.
            var existing = m_languageDropdown.choices;
            var sameChoices = existing != null && existing.Count == choices.Count;
            if (sameChoices)
            {
                for (var position = 0; position < choices.Count; position++)
                {
                    if (existing[position] != choices[position])
                    {
                        sameChoices = false;
                        break;
                    }
                }
            }

            if (!sameChoices)
            {
                m_languageDropdown.choices = choices;
            }

            // The value goes in by index. Handing SetValueWithoutNotify a string the choices
            // do not contain leaves the previous text standing on some editor versions, and
            // the previous text of a freshly cloned field is the placeholder from the uxml —
            // which is how the box came to read "Language" while everything else was fine.
            var current = MateriluneL10n.CurrentLanguageCode;
            var index = current == null ? -1 : choices.IndexOf(current);
            if (index < 0 && choices.Count > 0)
            {
                index = 0;
            }

            if (index >= 0)
            {
                m_languageDropdown.SetValueWithoutNotify(choices[index]);
            }
        }

        private sealed class PresetRowBinding
        {
            internal readonly Button RemoveButton;
            internal readonly Action RemoveAction;
            internal readonly RadioButton ActiveToggle;
            internal readonly EventCallback<ChangeEvent<bool>> ActiveChanged;
            internal readonly VisualElement NameSlot;
            internal readonly TextField NameField;
            internal readonly EventCallback<ClickEvent> NameSlotClicked;
            internal readonly EventCallback<ChangeEvent<string>> NameChanged;
            internal readonly EventCallback<KeyDownEvent> NameKeyDown;
            internal readonly EventCallback<FocusOutEvent> NameFocusOut;

            internal PresetRowBinding(
                Button removeButton,
                Action removeAction,
                RadioButton activeToggle,
                EventCallback<ChangeEvent<bool>> activeChanged,
                VisualElement nameSlot,
                TextField nameField,
                EventCallback<ClickEvent> nameSlotClicked,
                EventCallback<ChangeEvent<string>> nameChanged,
                EventCallback<KeyDownEvent> nameKeyDown,
                EventCallback<FocusOutEvent> nameFocusOut)
            {
                RemoveButton = removeButton;
                RemoveAction = removeAction;
                ActiveToggle = activeToggle;
                ActiveChanged = activeChanged;
                NameSlot = nameSlot;
                NameField = nameField;
                NameSlotClicked = nameSlotClicked;
                NameChanged = nameChanged;
                NameKeyDown = nameKeyDown;
                NameFocusOut = nameFocusOut;
            }

            /// <summary>
            /// Takes every callback this row registered back off it.
            /// </summary>
            /// <remarks>
            /// The rows are pooled: one left registered would keep its captured preset alive
            /// and fire for it long after the row shows another, which for the rename means
            /// writing the new name onto a preset that is no longer on screen.
            /// </remarks>
            internal void Unregister()
            {
                RemoveButton.clicked -= RemoveAction;
                ActiveToggle?.UnregisterValueChangedCallback(ActiveChanged);
                NameSlot?.UnregisterCallback(NameSlotClicked);
                if (NameField != null)
                {
                    NameField.UnregisterValueChangedCallback(NameChanged);
                    NameField.UnregisterCallback(NameKeyDown);
                    NameField.UnregisterCallback(NameFocusOut);
                }
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
