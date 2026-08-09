using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Displays a summary for a Materilune Swap manager.
    /// </summary>
    [CustomEditor(typeof(MateriluneSwap))]
    [CanEditMultipleObjects]
    internal sealed class MateriluneSwapInspector : MateriluneInspector
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

            var manager = GetFirstTarget<MateriluneSwap>();
            var presetCount = 0;
            var activePresetName = NoneText;
            if (manager != null)
            {
                var presets = manager.GetPresets();
                presetCount = presets == null ? 0 : presets.Count;
                activePresetName = GetActivePresetName(presets);
            }

            return string.Format(
                MateriluneL10n.Get(
                    "materilune.inspector.swap_summary",
                    "Presets: {0}\nActive preset: {1}"),
                presetCount,
                activePresetName);
        }

        private static string GetActivePresetName(IList<MateriluneSwapRoot> presets)
        {
            if (presets == null)
            {
                return NoneText;
            }

            foreach (var preset in presets)
            {
                if (preset != null && preset.gameObject.activeSelf)
                {
                    return GetDisplayName(preset.gameObject);
                }
            }

            return NoneText;
        }
    }
}
