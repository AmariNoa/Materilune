using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Displays a target hierarchy and allows mesh renderer selection.
    /// </summary>
    public class MateriluneTargetTreeView : VisualElement
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/TargetTree/MateriluneTargetTreeView.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/TargetTree/MateriluneTargetTreeView.uss";
        private const string RowClass = "materilune-target-tree__row";
        private const string SelectableRowClass = "materilune-target-tree__row--selectable";
        private const string SelectedRowClass = "materilune-target-tree__row--selected";
        private const string InactiveRowClass = "materilune-target-tree__row--inactive";
        private const int IndentWidth = 16;

        private ScrollView m_tree;
        private GameObject m_target;
        private Renderer m_selectedRenderer;
        private bool m_isBound;
        private readonly List<VisualElement> m_rows = new List<VisualElement>();
        private readonly Dictionary<Renderer, VisualElement> m_rowsByRenderer =
            new Dictionary<Renderer, VisualElement>();

        /// <summary>
        /// Creates the UXML factory for this element.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<MateriluneTargetTreeView>
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MateriluneTargetTreeView"/> class.
        /// </summary>
        public MateriluneTargetTreeView()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.target_tree.load_error",
                    "Materilune could not load the target tree UI assets."));
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_tree = this.Q<ScrollView>("tree");
            if (m_tree == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.target_tree.load_error",
                    "Materilune could not load the target tree UI assets."));
                Clear();
                return;
            }

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>
        /// Occurs when a renderer row is selected.
        /// </summary>
        public event Action<Renderer> RendererSelected;

        /// <summary>
        /// Gets the currently selected renderer, or <see langword="null"/> when none is selected.
        /// </summary>
        public Renderer SelectedRenderer
        {
            get
            {
                if ((m_selectedRenderer == null || !m_rowsByRenderer.ContainsKey(m_selectedRenderer))
                    && !ReferenceEquals(m_selectedRenderer, null))
                {
                    m_selectedRenderer = null;
                    ApplySelectedRow();
                }

                return m_selectedRenderer;
            }
        }

        /// <summary>
        /// Binds this view to a target hierarchy.
        /// </summary>
        /// <param name="target">The target object whose hierarchy is displayed.</param>
        public void Bind(GameObject target)
        {
            Unbind();
            if (m_tree == null || target == null)
            {
                return;
            }

            m_target = target;
            m_isBound = true;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Refresh();
        }

        /// <summary>
        /// Removes the current target binding and generated rows.
        /// </summary>
        public void Unbind()
        {
            if (m_isBound)
            {
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            }

            ClearRows();
            m_target = null;
            m_selectedRenderer = null;
            m_isBound = false;
        }

        /// <summary>
        /// Rebuilds the rows from the target's current hierarchy.
        /// </summary>
        public void Refresh()
        {
            if (m_tree == null || !m_isBound)
            {
                return;
            }

            var previousSelection = m_selectedRenderer;
            ClearRows();
            if (m_target == null)
            {
                m_selectedRenderer = null;
                return;
            }

            var renderers = MateriluneSetupService.CollectTargetRenderers(m_target);
            var rendererSet = new HashSet<Renderer>(renderers);
            AddRows(m_target.transform, 0, rendererSet);

            if (previousSelection == null || !m_rowsByRenderer.ContainsKey(previousSelection))
            {
                m_selectedRenderer = null;
            }

            ApplySelectedRow();
        }

        /// <summary>
        /// Selects a renderer represented by the current row collection.
        /// </summary>
        /// <param name="renderer">The renderer to select.</param>
        internal void SelectRenderer(Renderer renderer)
        {
            if (!m_isBound || m_tree == null || renderer == null ||
                !m_rowsByRenderer.ContainsKey(renderer) || m_selectedRenderer == renderer)
            {
                return;
            }

            m_selectedRenderer = renderer;
            ApplySelectedRow();
            RendererSelected?.Invoke(renderer);
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

        private void AddRows(Transform current, int depth, ISet<Renderer> renderers)
        {
            if (current == null || (depth > 0 && IsExcludedNode(current)))
            {
                return;
            }

            var row = CreateRow(current, depth, renderers);
            m_tree.contentContainer.Add(row);
            m_rows.Add(row);

            // The target itself is always represented by the depth-zero row. If it carries
            // an exclusion marker, its descendants are excluded by the same rule as collection.
            if (IsExcludedNode(current))
            {
                return;
            }

            for (var index = 0; index < current.childCount; index++)
            {
                var child = current.GetChild(index);
                if (child == null || IsExcludedNode(child))
                {
                    continue;
                }

                AddRows(child, depth + 1, renderers);
            }
        }

        private VisualElement CreateRow(Transform source, int depth, ISet<Renderer> renderers)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.style.marginLeft = new Length(depth * IndentWidth, LengthUnit.Pixel);
            if (!source.gameObject.activeSelf)
            {
                row.AddToClassList(InactiveRowClass);
            }

            var selectableRenderer = FindFirstRenderer(source.gameObject, renderers);
            if (selectableRenderer == null)
            {
                var label = new Label(source.gameObject.name);
                label.pickingMode = PickingMode.Ignore;
                row.Add(label);
                return row;
            }

            row.AddToClassList(SelectableRowClass);
            row.tooltip = MateriluneL10n.Get(
                "materilune.ui.target_tree.select_tooltip",
                "Select this mesh");
            var capturedRenderer = selectableRenderer;
            row.AddManipulator(new Clickable(() => SelectRenderer(capturedRenderer)));

            var selectableLabel = new Label(source.gameObject.name);
            selectableLabel.pickingMode = PickingMode.Ignore;
            row.Add(selectableLabel);

            foreach (var renderer in source.GetComponents<Renderer>())
            {
                if (renderer != null && renderers.Contains(renderer))
                {
                    m_rowsByRenderer[renderer] = row;
                }
            }

            return row;
        }

        private static Renderer FindFirstRenderer(GameObject gameObject, ISet<Renderer> renderers)
        {
            foreach (var renderer in gameObject.GetComponents<Renderer>())
            {
                if (renderer != null && renderers.Contains(renderer))
                {
                    return renderer;
                }
            }

            return null;
        }

        private static bool IsExcludedNode(Transform transform)
        {
            return transform.GetComponent<MateriluneSwap>() != null ||
                transform.GetComponent<MateriluneSwapRoot>() != null ||
                transform.gameObject.tag == "EditorOnly";
        }

        private void ApplySelectedRow()
        {
            foreach (var row in m_rows)
            {
                row.RemoveFromClassList(SelectedRowClass);
            }

            if (m_selectedRenderer != null && m_rowsByRenderer.TryGetValue(m_selectedRenderer, out var selectedRow))
            {
                selectedRow.AddToClassList(SelectedRowClass);
            }
        }

        private void ApplyLocalizedTexts()
        {
            if (m_tree == null)
            {
                return;
            }

            var tooltip = MateriluneL10n.Get(
                "materilune.ui.target_tree.select_tooltip",
                "Select this mesh");
            foreach (var row in m_rows)
            {
                if (row.ClassListContains(SelectableRowClass))
                {
                    row.tooltip = tooltip;
                }
            }
        }

        private void ClearRows()
        {
            m_rows.Clear();
            m_rowsByRenderer.Clear();
            if (m_tree != null)
            {
                m_tree.contentContainer.Clear();
            }
        }
    }
}
