using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Walks the user through replacing many materials from one example.
    /// </summary>
    /// <remarks>
    /// Two steps in one window. First an example source is picked, which decides nothing on its
    /// own; the candidate picker then supplies the material it should become, and the difference
    /// between the two names becomes the rule. The result of applying that rule to every row is
    /// shown for approval before anything is written, since it can overwrite work already done.
    /// </remarks>
    internal sealed class MateriluneBatchSwapWindow : EditorWindow
    {
        private const string UxmlPath =
            "Packages/com.amari-noa.materilune/Editor/UI/BatchSwap/MateriluneBatchSwapView.uxml";
        private const string RowUxmlPath =
            "Packages/com.amari-noa.materilune/Editor/UI/BatchSwap/MateriluneBatchSwapRow.uxml";
        private const string UnavailableRowClass = "materilune-batch-swap-row--unavailable";

        private const float WindowWidth = 520f;
        private const float WindowHeight = 380f;

        // Unity draws asset previews in the background; this is how often a row asks again
        // until its thumbnail has arrived, matching the candidate picker's cadence.
        private const long PreviewPollMilliseconds = 100;

        // The one open instance. Tracked directly rather than searched for: a search over
        // every loaded object can surface a never-shown instance, and closing one of those
        // throws from inside EditorWindow. Only windows that reached ShowUtility land here.
        private static MateriluneBatchSwapWindow s_openWindow;

        private readonly List<Material> m_sources = new List<Material>();
        private readonly List<MateriluneBatchSwapPlanItem> m_plan = new List<MateriluneBatchSwapPlanItem>();
        private readonly HashSet<int> m_selected = new HashSet<int>();

        private List<MateriluneMaterialSwapEntry> m_entries;
        private MateriluneCandidateMode m_mode;
        private Action<IReadOnlyList<MateriluneBatchSwapPlanItem>> m_onApply;
        private MateriluneBatchSwapRule m_rule;
        private Material m_exampleSource;
        private Material m_exampleReplacement;
        private bool m_hasRule;

        // One polling handle per recycled row, so a poll can pause once its thumbnail landed
        // and resume only when the row is bound to a material still waiting for one.
        private readonly Dictionary<Image, IVisualElementScheduledItem> m_previewPolls =
            new Dictionary<Image, IVisualElementScheduledItem>();

        private VisualTreeAsset m_rowTemplate;
        private Label m_step;
        private Label m_summary;
        private ListView m_rows;
        private Button m_apply;
        private Button m_cancel;
        private Button m_selectAll;
        private Button m_selectNone;

        /// <summary>
        /// Opens the batch replacement window for one component's entries.
        /// </summary>
        /// <param name="entries">The entries of the component being edited.</param>
        /// <param name="mode">The candidate discovery mode of that component.</param>
        /// <param name="onApply">Invoked with the rows to write when the user approves.</param>
        internal static void Open(
            IReadOnlyList<MateriluneMaterialSwapEntry> entries,
            MateriluneCandidateMode mode,
            Action<IReadOnlyList<MateriluneBatchSwapPlanItem>> onApply)
        {
            // Only one of these at a time. Stacked windows left no way to tell which preset
            // or mesh each one aimed at, so a press replaces whatever is open and the one
            // window on screen always belongs to the button pressed last.
            if (s_openWindow != null)
            {
                s_openWindow.Close();
            }

            var window = CreateInstance<MateriluneBatchSwapWindow>();
            window.titleContent = new GUIContent(MateriluneL10n.Get(
                "materilune.ui.batch_swap.title",
                "Batch swap"));
            window.m_entries = entries == null
                ? new List<MateriluneMaterialSwapEntry>()
                : new List<MateriluneMaterialSwapEntry>(entries);
            window.m_mode = mode;
            window.m_onApply = onApply;
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.ShowUtility();
            s_openWindow = window;
        }

        private void OnDestroy()
        {
            if (s_openWindow == this)
            {
                s_openWindow = null;
            }
        }

        private void CreateGUI()
        {
            // A domain reload rebuilds the window but not this state: the entries and the
            // callback live only in memory, and without them the window can neither show nor
            // apply anything. Closing is the honest outcome; the button reopens it in one press.
            if (m_entries == null)
            {
                EditorApplication.delayCall += Close;
                return;
            }

            rootVisualElement.Clear();
            // The stylesheet is referenced by the uxml itself, so what the UI Builder
            // previews is exactly what runs; the code attaches nothing.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            m_rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RowUxmlPath);
            if (visualTree == null || m_rowTemplate == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.batch_swap.load_error",
                    "Materilune could not load the batch swap UI assets."));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            m_step = rootVisualElement.Q<Label>("lbl-step");
            m_summary = rootVisualElement.Q<Label>("lbl-summary");
            m_rows = rootVisualElement.Q<ListView>("lv-batch-rows");
            m_apply = rootVisualElement.Q<Button>("btn-apply");
            m_cancel = rootVisualElement.Q<Button>("btn-cancel");
            m_selectAll = rootVisualElement.Q<Button>("btn-select-all");
            m_selectNone = rootVisualElement.Q<Button>("btn-select-none");
            if (m_step == null
                || m_summary == null
                || m_rows == null
                || m_apply == null
                || m_cancel == null
                || m_selectAll == null
                || m_selectNone == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.ui.batch_swap.missing_element_error",
                    "Materilune batch swap UI is missing a required element."));
                rootVisualElement.Clear();
                return;
            }

            m_rows.selectionType = SelectionType.None;
            m_rows.makeItem = MakeRow;
            m_rows.bindItem = BindRow;
            m_apply.text = MateriluneL10n.Get("materilune.ui.batch_swap.apply", "Apply");
            m_cancel.text = MateriluneL10n.Get("materilune.ui.batch_swap.cancel", "Cancel");
            m_selectAll.text = MateriluneL10n.Get("materilune.ui.batch_swap.select_all", "Select all");
            m_selectNone.text = MateriluneL10n.Get("materilune.ui.batch_swap.select_none", "Select none");
            m_selectAll.tooltip = MateriluneL10n.Get(
                "materilune.ui.batch_swap.select_all_tooltip",
                "Tick every row that can be applied, including the ones that overwrite a setting");
            m_apply.clicked += OnApplyClicked;
            m_cancel.clicked += Close;
            m_selectAll.clicked += OnSelectAllClicked;
            m_selectNone.clicked += OnSelectNoneClicked;

            ShowSourceStep();
        }

        private void OnDisable()
        {
            if (m_apply != null)
            {
                m_apply.clicked -= OnApplyClicked;
            }

            if (m_cancel != null)
            {
                m_cancel.clicked -= Close;
            }

            if (m_selectAll != null)
            {
                m_selectAll.clicked -= OnSelectAllClicked;
            }

            if (m_selectNone != null)
            {
                m_selectNone.clicked -= OnSelectNoneClicked;
            }

            m_onApply = null;
        }

        /// <summary>
        /// Shows the materials of this component so one can serve as the example.
        /// </summary>
        private void ShowSourceStep()
        {
            m_hasRule = false;
            m_plan.Clear();
            m_selected.Clear();
            m_sources.Clear();
            foreach (var entry in m_entries)
            {
                if (entry.From != null && !m_sources.Contains(entry.From))
                {
                    m_sources.Add(entry.From);
                }
            }

            m_step.text = MateriluneL10n.Get(
                "materilune.ui.batch_swap.step_source",
                "Detect a pattern from one replacement and apply it to the other materials");
            // The line is reserved in both steps, and a reserved line showing nothing reads
            // as a layout mistake, so it carries the count while there is nothing to sum up.
            m_summary.text = string.Format(
                MateriluneL10n.Get("materilune.ui.batch_swap.summary_sources", "{0} material(s)"),
                m_sources.Count);

            // Nothing is applied or selected in this step, so the buttons are present but
            // inert. Removing them would move the list underneath between the steps.
            m_apply.SetEnabled(false);
            m_selectAll.SetEnabled(false);
            m_selectNone.SetEnabled(false);
            m_rows.itemsSource = m_sources;
            m_rows.Rebuild();
        }

        /// <summary>
        /// Shows what the learned rule would do to every row.
        /// </summary>
        private void ShowPlanStep()
        {
            // The example pair heads the window: the names the rule was cut from say more at a
            // glance than the cut-out rule alone, which can be as short as one digit.
            m_step.text = string.Format(
                MateriluneL10n.Get(
                    "materilune.ui.batch_swap.step_plan",
                    "Example: {0}  ->  {1}\nRule: \"{2}\" becomes \"{3}\". Tick the rows to apply."),
                m_exampleSource == null ? string.Empty : m_exampleSource.name,
                m_exampleReplacement == null ? string.Empty : m_exampleReplacement.name,
                m_rule.From,
                m_rule.To);

            m_selected.Clear();
            for (var index = 0; index < m_plan.Count; index++)
            {
                // Every applicable row starts ticked, overwrites included (2026-08-17 の指示で
                // 既定を全選択へ変更). The list still spells out which rows replace an existing
                // setting, and unticking is one press.
                if (m_plan[index].IsApplicable)
                {
                    m_selected.Add(index);
                }
            }

            m_selectAll.SetEnabled(true);
            m_selectNone.SetEnabled(true);
            m_rows.itemsSource = m_plan;
            m_rows.Rebuild();
            RefreshSummary();
        }

        /// <summary>
        /// Ticks every row that can be applied, the overwriting ones included.
        /// </summary>
        /// <remarks>
        /// Overwrites start unticked so they take a deliberate act; pressing this is exactly
        /// that act, spelled out in the button's tooltip, so they are included rather than
        /// leaving the button a synonym for the default state.
        /// </remarks>
        private void OnSelectAllClicked()
        {
            if (!m_hasRule)
            {
                return;
            }

            m_selected.Clear();
            for (var index = 0; index < m_plan.Count; index++)
            {
                if (m_plan[index].IsApplicable)
                {
                    m_selected.Add(index);
                }
            }

            m_rows.RefreshItems();
            RefreshSummary();
        }

        private void OnSelectNoneClicked()
        {
            if (!m_hasRule)
            {
                return;
            }

            m_selected.Clear();
            m_rows.RefreshItems();
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            m_summary.text = string.Format(
                MateriluneL10n.Get("materilune.ui.batch_swap.summary", "{0} of {1} rows selected"),
                m_selected.Count,
                m_plan.Count);
            m_apply.SetEnabled(m_selected.Count > 0);
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            m_rowTemplate.CloneTree(row);
            var preview = row.Q<Image>("img-row-preview");
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
        /// Builds and binds one row outside the list view, so tests can inspect a row without
        /// the layout pass that fills a virtualized list.
        /// </summary>
        /// <param name="index">The row index to bind.</param>
        /// <returns>The bound row.</returns>
        internal VisualElement BuildRowForTests(int index)
        {
            // A test can reach this before the editor has given the window its first layout
            // pass; the window's own build step is safe to run directly and starts over from
            // a cleared root, so running it twice costs nothing.
            if (m_rows == null)
            {
                CreateGUI();
            }

            // The build step can decline (no entries after a domain reload, assets missing);
            // an empty row then makes the caller's assertions fail plainly instead of this
            // helper throwing on the absent template.
            if (m_rowTemplate == null)
            {
                return new VisualElement();
            }

            var row = MakeRow();
            BindRow(row, index);
            return row;
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

        private void BindRow(VisualElement element, int index)
        {
            var toggle = element.Q<Toggle>("tgl-row");
            var label = element.Q<Label>("lbl-row");
            if (toggle == null || label == null)
            {
                return;
            }

            // Rebinding reuses rows, so a handler left from the previous binding would fire for
            // whatever row now sits here.
            toggle.UnregisterCallback<ChangeEvent<bool>, int>(OnRowToggled);

            if (!m_hasRule)
            {
                BindSourceRow(element, toggle, label, index);
                return;
            }

            BindPlanRow(element, toggle, label, index);
        }

        private void BindSourceRow(VisualElement element, Toggle toggle, Label label, int index)
        {
            var source = index >= 0 && index < m_sources.Count ? m_sources[index] : null;
            element.EnableInClassList(UnavailableRowClass, false);
            toggle.SetValueWithoutNotify(false);
            toggle.visible = false;
            label.text = source == null ? string.Empty : source.name;

            var preview = element.Q<Image>("img-row-preview");
            if (preview != null)
            {
                // The row is recycled, so the previous material's thumbnail has to go before
                // the new one is requested. The poll paused when the last thumbnail landed;
                // a new material means new waiting, and the refresh pauses it again at once
                // if there is nothing to wait for.
                preview.image = null;
                preview.userData = source;
                if (m_previewPolls.TryGetValue(preview, out var poll))
                {
                    poll.Resume();
                }

                PollPreview(preview);
            }

            // The whole row is the button in this step: clicking a name opens the candidates
            // for it, which is the only thing there is to do here.
            element.userData = index;
            element.RegisterCallback<ClickEvent>(OnSourceRowClicked);
        }

        private void BindPlanRow(VisualElement element, Toggle toggle, Label label, int index)
        {
            element.UnregisterCallback<ClickEvent>(OnSourceRowClicked);
            var item = index >= 0 && index < m_plan.Count ? m_plan[index] : null;
            if (item == null)
            {
                return;
            }

            element.EnableInClassList(UnavailableRowClass, !item.IsApplicable);
            toggle.visible = item.IsApplicable;
            toggle.SetEnabled(item.IsApplicable);
            toggle.SetValueWithoutNotify(m_selected.Contains(index));
            toggle.RegisterCallback<ChangeEvent<bool>, int>(OnRowToggled, index);
            label.text = DescribeItem(item);

            // The thumbnail shows what the row will amount to: the replacement while the row
            // is ticked, the material as it is while it is not (2026-08-18 の指示).
            UpdatePlanPreview(element.Q<Image>("img-row-preview"), index);
        }

        /// <summary>
        /// Points a plan row's thumbnail at the material the row currently stands for.
        /// </summary>
        /// <param name="preview">The image showing the preview.</param>
        /// <param name="index">The plan row index.</param>
        private void UpdatePlanPreview(Image preview, int index)
        {
            if (preview == null || index < 0 || index >= m_plan.Count)
            {
                return;
            }

            var item = m_plan[index];
            var material = m_selected.Contains(index) && item.To != null ? item.To : item.From;
            preview.image = null;
            preview.userData = material;
            if (m_previewPolls.TryGetValue(preview, out var poll))
            {
                poll.Resume();
            }

            PollPreview(preview);
        }

        private string DescribeItem(MateriluneBatchSwapPlanItem item)
        {
            // Two lines per row (2026-08-18 の指示): the source name above, what happens to it
            // below. The null checks read destroyed materials as absent, Unity's fake null
            // included, instead of throwing on a replacement deleted after planning.
            var fromName = item.From == null ? string.Empty : item.From.name;
            var toName = item.To == null ? string.Empty : item.To.name;
            switch (item.Status)
            {
                case MateriluneBatchSwapStatus.Ready:
                    return string.Format(
                        MateriluneL10n.Get(
                            "materilune.ui.batch_swap.row_ready",
                            "{0}\n->  {1}"),
                        fromName,
                        toName);
                case MateriluneBatchSwapStatus.Overwrite:
                    return string.Format(
                        MateriluneL10n.Get(
                            "materilune.ui.batch_swap.row_overwrite",
                            "{0}\n->  {1}   (replaces the current setting)"),
                        fromName,
                        toName);
                case MateriluneBatchSwapStatus.NoCandidate:
                    return string.Format(
                        MateriluneL10n.Get(
                            "materilune.ui.batch_swap.row_no_candidate",
                            "{0}\n(no candidate named {1})"),
                        fromName,
                        item.ExpectedName);
                default:
                    return string.Format(
                        MateriluneL10n.Get(
                            "materilune.ui.batch_swap.row_not_matched",
                            "{0}\n(the rule does not apply)"),
                        fromName);
            }
        }

        private void OnSourceRowClicked(ClickEvent clickEvent)
        {
            if (m_hasRule || !(clickEvent.currentTarget is VisualElement element)
                || !(element.userData is int index))
            {
                return;
            }

            var source = index >= 0 && index < m_sources.Count ? m_sources[index] : null;
            if (source == null)
            {
                return;
            }

            MateriluneCandidatePickerWindow.Open(
                element.worldBound,
                source,
                m_mode,
                replacement => OnExampleChosen(source, replacement));
        }

        private void OnExampleChosen(Material source, Material replacement)
        {
            m_exampleSource = source;
            m_exampleReplacement = replacement;
            m_rule = MateriluneBatchSwapRule.Learn(source, replacement);
            if (!m_rule.IsValid)
            {
                Debug.LogWarning(MateriluneL10n.Get(
                    "materilune.ui.batch_swap.rule_not_learned",
                    "No pattern could be found between the two material names."));
                return;
            }

            m_plan.Clear();
            m_plan.AddRange(MateriluneBatchSwap.Plan(m_entries, m_rule, m_mode));
            m_hasRule = true;
            ShowPlanStep();
        }

        private void OnRowToggled(ChangeEvent<bool> changeEvent, int index)
        {
            if (changeEvent.newValue)
            {
                m_selected.Add(index);
            }
            else
            {
                m_selected.Remove(index);
            }

            // The tick decides which material the row's thumbnail shows, so the flip lands
            // in the picture right away, without waiting for a list refresh.
            if (changeEvent.currentTarget is VisualElement toggleElement && toggleElement.parent != null)
            {
                UpdatePlanPreview(toggleElement.parent.Q<Image>("img-row-preview"), index);
            }

            RefreshSummary();
        }

        private void OnApplyClicked()
        {
            var approved = new List<MateriluneBatchSwapPlanItem>();
            foreach (var index in m_selected)
            {
                if (index >= 0 && index < m_plan.Count && m_plan[index].IsApplicable)
                {
                    approved.Add(m_plan[index]);
                }
            }

            var callback = m_onApply;
            Close();
            callback?.Invoke(approved);
        }

        internal void ChooseExampleForTests(Material source, Material replacement)
        {
            // Tests can arrive before the first layout pass; the plan step touches the built
            // controls, so the build step runs here the same way BuildRowForTests runs it.
            if (m_rows == null)
            {
                CreateGUI();
            }

            OnExampleChosen(source, replacement);
        }

        internal IReadOnlyList<MateriluneBatchSwapPlanItem> GetPlanForTests()
        {
            return m_plan;
        }

        internal bool IsRowSelectedForTests(int index)
        {
            return m_selected.Contains(index);
        }

        internal void ApplyForTests()
        {
            OnApplyClicked();
        }

        internal void SelectAllForTests()
        {
            OnSelectAllClicked();
        }

        internal void SelectNoneForTests()
        {
            OnSelectNoneClicked();
        }
    }
}
