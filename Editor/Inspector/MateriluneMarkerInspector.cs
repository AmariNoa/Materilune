using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Displays a summary for the Materilune marker component.
    /// </summary>
    [CustomEditor(typeof(Materilune))]
    [CanEditMultipleObjects]
    internal sealed class MateriluneMarkerInspector : MateriluneInspector
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

            // The marker sits on the Materilune object directly under the setup target,
            // so the parent is the object the whole setup operates on.
            var marker = GetFirstTarget<Materilune>();
            var setupTarget = marker == null || marker.transform.parent == null
                ? null
                : marker.transform.parent.gameObject;

            return string.Format(
                MateriluneL10n.Get(
                    "materilune.inspector.marker_summary",
                    "Target: {0}"),
                GetDisplayName(setupTarget));
        }
    }
}
