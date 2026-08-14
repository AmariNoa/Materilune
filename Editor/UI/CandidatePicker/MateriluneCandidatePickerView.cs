using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides tabbed material candidate selection for a replacement field.
    /// </summary>
    internal sealed class MateriluneCandidatePickerView : VisualElement
    {
        private const string UxmlPath =
            "Packages/com.amari-noa.materilune/Editor/UI/CandidatePicker/MateriluneCandidatePickerView.uxml";
        private const string UssPath =
            "Packages/com.amari-noa.materilune/Editor/UI/CandidatePicker/MateriluneCandidatePickerView.uss";
        private const string SelectedTabClass = "materilune-candidate-picker__tab--selected";
        private const string RowUxmlPath =
            "Packages/com.amari-noa.materilune/Editor/UI/CandidatePicker/MateriluneCandidatePickerRow.uxml";

        // Tall enough for a legible material preview beside the name. The preview keeps its
        // square slot whether or not the thumbnail has been generated yet, so the rows never
        // change height (AGENTS.md 2.4 (7)).
        private const float CandidateRowHeight = 32f;
        private const long PreviewPollMilliseconds = 100;

        // The list always starts with a row that clears the replacement. It is carried as a null
        // material, which the discovery service never returns, so a null row is unambiguous.
        private const int ClearRowCount = 1;

        private VisualElement m_tabs;
        private Button m_sameDirectoryTab;
        private Button m_siblingDirectoryTab;
        private ListView m_candidateList;
        private Label m_emptyLabel;
        private List<Material> m_candidates = new List<Material>();
        private Material m_current;
        private MateriluneCandidateMode m_selectedMode;
        private bool m_isRefreshing;
        private bool m_isCleared;
        private VisualTreeAsset m_rowTemplate;

        /// <summary>
        /// Initializes a new instance of the <see cref="MateriluneCandidatePickerView"/> class.
        /// </summary>
        internal MateriluneCandidatePickerView()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.load_error",
                    "Materilune could not load the material candidate picker UI assets."));
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_tabs = this.Q<VisualElement>("elm-tabs");
            m_sameDirectoryTab = this.Q<Button>("btn-tab-same-directory");
            m_siblingDirectoryTab = this.Q<Button>("btn-tab-sibling-directory");
            m_candidateList = this.Q<ListView>("lv-candidates");
            m_emptyLabel = this.Q<Label>("lbl-empty");
            if (!HasControls())
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.missing_element_error",
                    "Materilune material candidate picker UI is missing a required element."));
                Clear();
                return;
            }

            m_rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RowUxmlPath);
            m_candidateList.makeItem = MakeCandidateItem;
            m_candidateList.bindItem = BindCandidateItem;
            m_candidateList.fixedItemHeight = CandidateRowHeight;
            m_candidateList.selectionType = SelectionType.Single;
            m_candidateList.selectionChanged += OnCandidateSelectionChanged;
            m_sameDirectoryTab.clicked += OnSameDirectoryTabClicked;
            m_siblingDirectoryTab.clicked += OnSiblingDirectoryTabClicked;
            m_emptyLabel.style.visibility = Visibility.Hidden;
            ApplyLocalizedTexts();
            RegisterCallback<AttachToPanelEvent>(
                _ => MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged));
            RegisterCallback<DetachFromPanelEvent>(
                _ => MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged));
        }

        /// <summary>
        /// Occurs when a material is selected from the candidate list.
        /// </summary>
        internal event Action<Material> CandidateSelected;

        /// <summary>
        /// Shows candidates for the current material using the specified initial tab.
        /// </summary>
        /// <param name="current">The current replacement material or its source material.</param>
        /// <param name="initialTab">The tab selected when the picker is shown.</param>
        internal void Show(Material current, MateriluneCandidateMode initialTab)
        {
            if (m_isCleared || !HasControls())
            {
                return;
            }

            m_current = current;
            m_selectedMode = ResolveInitialTab(initialTab);
            ApplyTabState();
            RefreshCandidates();
        }

        /// <summary>
        /// Releases the current material references and event subscriptions.
        /// </summary>
        internal void Clear()
        {
            if (m_isCleared)
            {
                return;
            }

            if (m_candidateList != null)
            {
                m_candidateList.selectionChanged -= OnCandidateSelectionChanged;
                m_candidateList.itemsSource = null;
                m_candidateList.Rebuild();
            }

            if (m_sameDirectoryTab != null)
            {
                m_sameDirectoryTab.clicked -= OnSameDirectoryTabClicked;
            }

            if (m_siblingDirectoryTab != null)
            {
                m_siblingDirectoryTab.clicked -= OnSiblingDirectoryTabClicked;
            }

            MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged);
            CandidateSelected = null;
            m_candidates = null;
            m_current = null;
            m_tabs = null;
            m_sameDirectoryTab = null;
            m_siblingDirectoryTab = null;
            m_candidateList = null;
            m_emptyLabel = null;
            m_isCleared = true;
        }

        private static MateriluneCandidateMode ResolveInitialTab(MateriluneCandidateMode initialTab)
        {
            switch (initialTab)
            {
                case MateriluneCandidateMode.None:
                case MateriluneCandidateMode.SameDirectory:
                    return MateriluneCandidateMode.SameDirectory;
                case MateriluneCandidateMode.SiblingDirectory:
                    return MateriluneCandidateMode.SiblingDirectory;
                default:
                    throw new ArgumentOutOfRangeException(nameof(initialTab), initialTab, "Unknown candidate mode.");
            }
        }

        private VisualElement MakeCandidateItem()
        {
            var row = new VisualElement();
            if (m_rowTemplate == null)
            {
                // Without the template the list still has to show the names, so the row falls
                // back to a bare label rather than leaving the popup empty.
                row.Add(new Label { name = "lbl-material-name" });
                return row;
            }

            m_rowTemplate.CloneTree(row);
            var preview = row.Q<Image>("img-material-preview");
            if (preview != null)
            {
                // Unity renders asset previews in the background, so the first call usually
                // returns nothing. The row polls while it is attached to a panel and stops
                // touching the image as soon as it has one for the material it is showing.
                preview.schedule.Execute(() => RefreshPreview(preview)).Every(PreviewPollMilliseconds);
            }

            return row;
        }

        private void BindCandidateItem(VisualElement element, int index)
        {
            if (element == null)
            {
                return;
            }

            var label = element.Q<Label>("lbl-material-name");
            var preview = element.Q<Image>("img-material-preview");
            var material = m_candidates != null && index >= 0 && index < m_candidates.Count
                ? m_candidates[index]
                : null;

            if (label != null)
            {
                label.text = material == null
                    ? MateriluneL10n.Get(
                        "materilune.ui.candidate_picker.clear",
                        "None (clear the replacement)")
                    : material.name;
            }

            if (preview != null)
            {
                // The row is recycled, so the previous material's thumbnail has to go before the
                // new one is requested.
                preview.image = null;
                preview.userData = material;
                RefreshPreview(preview);
            }
        }

        /// <summary>
        /// Puts the material's asset preview into the image once Unity has rendered it.
        /// </summary>
        /// <param name="preview">The image showing the preview.</param>
        private static void RefreshPreview(Image preview)
        {
            var material = preview.userData as Material;
            if (material == null)
            {
                preview.image = null;
                return;
            }

            if (preview.image != null)
            {
                return;
            }

            preview.image = AssetPreview.GetAssetPreview(material);
        }

        /// <summary>
        /// Builds and binds one candidate row outside the list view, so a test can inspect a row
        /// without the layout pass that a virtualized list needs to fill itself.
        /// </summary>
        /// <param name="index">The candidate index to bind.</param>
        /// <returns>The bound row, or <see langword="null" /> when the row cannot be built.</returns>
        internal VisualElement BuildCandidateRowForTests(int index)
        {
            var row = MakeCandidateItem();
            if (row == null)
            {
                return null;
            }

            BindCandidateItem(row, index);
            return row;
        }

        private void OnSameDirectoryTabClicked()
        {
            SelectTab(MateriluneCandidateMode.SameDirectory);
        }

        private void OnSiblingDirectoryTabClicked()
        {
            SelectTab(MateriluneCandidateMode.SiblingDirectory);
        }

        /// <summary>
        /// Selects a tab and reloads the candidates it lists. Internal so a test can drive the
        /// same path the tab buttons take: Button.clicked is an event with accessors, so a test
        /// outside this assembly cannot raise it.
        /// </summary>
        /// <param name="mode">The mode whose tab is selected.</param>
        internal void SelectTab(MateriluneCandidateMode mode)
        {
            if (m_isCleared || !HasControls())
            {
                return;
            }

            m_selectedMode = mode;
            ApplyTabState();
            RefreshCandidates();
        }

        private void ApplyTabState()
        {
            m_sameDirectoryTab.EnableInClassList(
                SelectedTabClass,
                m_selectedMode == MateriluneCandidateMode.SameDirectory);
            m_siblingDirectoryTab.EnableInClassList(
                SelectedTabClass,
                m_selectedMode == MateriluneCandidateMode.SiblingDirectory);
        }

        private void RefreshCandidates()
        {
            m_isRefreshing = true;
            try
            {
                m_candidateList.ClearSelection();
                var found = MateriluneMaterialCandidates.GetCandidates(m_current, m_selectedMode);
                m_candidates = new List<Material>(found.Count + ClearRowCount) { null };
                m_candidates.AddRange(found);
                m_candidateList.itemsSource = m_candidates;
                m_candidateList.Rebuild();

                // The clear row is always there, so the message reports on the discovered
                // materials rather than on the number of rows.
                m_emptyLabel.style.visibility = found.Count == 0
                    ? Visibility.Visible
                    : Visibility.Hidden;
            }
            finally
            {
                m_isRefreshing = false;
            }
        }

        private void OnCandidateSelectionChanged(IEnumerable<object> selection)
        {
            if (m_isRefreshing)
            {
                return;
            }

            // The clear row carries a null material, so the picked index is what identifies the
            // row. Reading the selected objects could not tell that row from an empty selection.
            var index = m_candidateList.selectedIndex;
            if (index < 0 || m_candidates == null || index >= m_candidates.Count)
            {
                return;
            }

            CandidateSelected?.Invoke(m_candidates[index]);
        }

        private void OnLanguageChanged(string languageCode)
        {
            ApplyLocalizedTexts();
        }

        private void ApplyLocalizedTexts()
        {
            if (!HasControls())
            {
                return;
            }

            m_sameDirectoryTab.text = MateriluneL10n.Get(
                "materilune.ui.candidate_picker.same_directory_tab",
                "Same directory");
            m_siblingDirectoryTab.text = MateriluneL10n.Get(
                "materilune.ui.candidate_picker.sibling_directory_tab",
                "Sibling directories");
            m_emptyLabel.text = MateriluneL10n.Get(
                "materilune.ui.candidate_picker.empty_message",
                "No candidate materials were found.");
        }

        private bool HasControls()
        {
            return m_tabs != null
                && m_sameDirectoryTab != null
                && m_siblingDirectoryTab != null
                && m_candidateList != null
                && m_emptyLabel != null;
        }
    }
}
