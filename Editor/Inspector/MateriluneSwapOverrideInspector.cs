using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Displays a summary for a Materilune Swap Override component.
    /// </summary>
    [CustomEditor(typeof(MateriluneSwapOverride))]
    [CanEditMultipleObjects]
    internal sealed class MateriluneSwapOverrideInspector : MateriluneInspector
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

            var operationOverride = GetFirstTarget<MateriluneSwapOverride>();
            var targetName = operationOverride == null
                ? NoneText
                : GetDisplayName(operationOverride.TargetRenderer);
            var total = 0;
            var assigned = 0;
            if (operationOverride != null && operationOverride.Swaps != null)
            {
                total = operationOverride.Swaps.Count;
                foreach (var swap in operationOverride.Swaps)
                {
                    if (swap.To != null)
                    {
                        assigned++;
                    }
                }
            }

            return string.Format(
                MateriluneL10n.Get(
                    "materilune.inspector.override_summary",
                    "Target mesh: {0}\nReplacements set: {1} / {2}"),
                targetName,
                assigned,
                total);
        }
    }
}
