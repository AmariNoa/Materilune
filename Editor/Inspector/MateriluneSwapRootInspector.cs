using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Displays a summary for a Materilune Swap Root preset.
    /// </summary>
    [CustomEditor(typeof(MateriluneSwapRoot))]
    [CanEditMultipleObjects]
    internal sealed class MateriluneSwapRootInspector : MateriluneInspector
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            return CreateSharedInspectorGUI();
        }

        /// <inheritdoc />
        protected override string BuildSummary()
        {
            if (HasMultipleTargets)
            {
                return MultipleTargetsText;
            }

            var preset = GetFirstTarget<MateriluneSwapRoot>();
            var targetName = preset == null ? NoneText : GetDisplayName(preset.SetupTarget);
            var total = 0;
            var assigned = 0;
            var orphaned = 0;
            MateriluneSwapEntries.CountEntries(preset, out total, out assigned, out orphaned);
            var orphanedText = orphaned > 0
                ? string.Format(
                    MateriluneL10n.Get(
                        "materilune.inspector.orphaned_suffix",
                        " (orphaned: {0})"),
                    orphaned)
                : string.Empty;

            return string.Format(
                MateriluneL10n.Get(
                    "materilune.inspector.root_summary",
                    "Target: {0}\nReplacements set: {1} / {2}{3}"),
                targetName,
                assigned,
                total,
                orphanedText);
        }
    }
}
