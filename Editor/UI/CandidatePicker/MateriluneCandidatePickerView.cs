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
        private const long PreviewPollMilliseconds = 100;

        // The list always starts with a row that clears the replacement. It is carried as a null
        // material, which the discovery service never returns, so a null row is unambiguous.
        private const int ClearRowCount = 1;

        // Everything a row spends beside the name: the preview slot and its margin, the row and
        // list padding, the picker padding, and room for a vertical scroll bar.
        private const float RowChromeWidth = 78f;

        // The tabs only pay the picker padding and their own margins.
        private const float TabChromeWidth = 20f;
        private const float FallbackCharacterWidth = 7f;

        private static readonly MateriluneCandidateMode[] SearchModes =
        {
            MateriluneCandidateMode.SameDirectory,
            MateriluneCandidateMode.SiblingDirectory,
        };

        // One polling handle per recycled row, so a poll can pause once its thumbnail landed
        // and resume only when the row is bound to a material still waiting for one.
        private readonly Dictionary<Image, IVisualElementScheduledItem> m_previewPolls =
            new Dictionary<Image, IVisualElementScheduledItem>();

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
            // The stylesheet is referenced by the uxml itself, so what the UI Builder
            // previews is exactly what runs; the code attaches nothing.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.load_error",
                    "Materilune could not load the material candidate picker UI assets."));
                return;
            }

            visualTree.CloneTree(this);

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
            m_candidateList.destroyItem = DestroyCandidateItem;
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

        /// <summary>
        /// Measures the width the popup needs for the longest text it will show.
        /// </summary>
        /// <param name="current">The material the candidates are searched around.</param>
        /// <returns>The width in pixels, before the caller clamps it.</returns>
        /// <remarks>
        /// Both tabs are measured, not just the one that opens first. The popup is sized once,
        /// when it opens, so a width that fitted only the first tab would have to change when
        /// the other tab is selected, and the layout may not move while the user is working
        /// (AGENTS.md 2.4 (7)).
        /// </remarks>
        internal static float MeasureRequiredWidth(Material current)
        {
            var widest = MeasureText(MateriluneL10n.Get(
                "materilune.ui.candidate_picker.clear",
                "None (clear the replacement)"));
            widest = Mathf.Max(widest, MeasureText(MateriluneL10n.Get(
                "materilune.ui.candidate_picker.empty_message",
                "No candidate materials were found.")));

            foreach (var mode in SearchModes)
            {
                foreach (var material in MateriluneMaterialCandidates.GetCandidates(current, mode))
                {
                    if (material != null)
                    {
                        widest = Mathf.Max(widest, MeasureText(material.name));
                    }
                }
            }

            // The tabs sit side by side above the list, so together they have to fit as well.
            var tabs = MeasureText(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.same_directory_tab",
                    "Same directory"))
                + MeasureText(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.sibling_directory_tab",
                    "Sibling directories"));

            return Mathf.Max(widest + RowChromeWidth, tabs + TabChromeWidth);
        }

        private static float MeasureText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            // EditorStyles is unavailable very early in a domain reload, so the width falls back
            // to a rough per-character estimate rather than throwing while a popup is opening.
            var style = EditorStyles.label;
            return style == null
                ? text.Length * FallbackCharacterWidth
                : style.CalcSize(new GUIContent(text)).x;
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
            // The template is the only source of the row's layout. Building a substitute row
            // here would put layout back into code, which this project keeps out of it; a
            // missing template is a broken installation and is reported as such instead.
            var row = new VisualElement();
            if (m_rowTemplate == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.candidate_picker.row_template_error",
                    "Materilune could not load the material candidate row template."));
                return row;
            }

            m_rowTemplate.CloneTree(row);
            var preview = row.Q<Image>("img-material-preview");
            if (preview != null)
            {
                // Unity renders asset previews in the background, so the first call usually
                // returns nothing. The row polls until its thumbnail has arrived and then
                // pauses; binding resumes it when a new material comes in.
                m_previewPolls[preview] = preview.schedule
                    .Execute(() => PollPreview(preview))
                    .Every(PreviewPollMilliseconds);
            }

            return row;
        }

        /// <summary>
        /// Refreshes one preview and pauses its polling once there is nothing left to wait for.
        /// </summary>
        /// <param name="preview">The image showing the preview.</param>
        private void PollPreview(Image preview)
        {
            if (RefreshPreview(preview) && m_previewPolls.TryGetValue(preview, out var poll))
            {
                poll.Pause();
            }
        }

        /// <summary>
        /// Lets go of a row the list threw away, so rebuilt lists do not pile up handles.
        /// </summary>
        /// <param name="element">The row being discarded.</param>
        private void DestroyCandidateItem(VisualElement element)
        {
            var preview = element == null ? null : element.Q<Image>("img-material-preview");
            if (preview != null && m_previewPolls.TryGetValue(preview, out var poll))
            {
                poll.Pause();
                m_previewPolls.Remove(preview);
            }
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
                // new one is requested. The poll paused when the last thumbnail landed; a new
                // material means new waiting, and the refresh pauses it again at once if there
                // is nothing to wait for.
                preview.image = null;
                preview.userData = material;
                if (m_previewPolls.TryGetValue(preview, out var poll))
                {
                    poll.Resume();
                }

                PollPreview(preview);
            }
        }

        /// <summary>
        /// Puts the material's asset preview into the image once Unity has rendered it.
        /// </summary>
        /// <param name="preview">The image showing the preview.</param>
        /// <returns>Whether the preview is settled, with nothing left to wait for.</returns>
        private static bool RefreshPreview(Image preview)
        {
            var material = preview.userData as Material;
            if (material == null)
            {
                preview.image = null;
                return true;
            }

            if (preview.image != null)
            {
                return true;
            }

            preview.image = AssetPreview.GetAssetPreview(material);
            return preview.image != null;
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
