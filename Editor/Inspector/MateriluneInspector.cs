using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides the shared layout and behavior for the Materilune summary inspectors.
    /// </summary>
    internal abstract class MateriluneInspector : Editor
    {
        private const string UxmlPath =
            "Packages/com.amari-noa.materilune/Editor/Inspector/MateriluneInspector.uxml";

        /// <summary>
        /// Creates the shared summary-only inspector UI.
        /// </summary>
        /// <returns>The shared inspector UI.</returns>
        protected VisualElement CreateSharedInspectorGUI()
        {
            var root = new VisualElement();
            // The stylesheet is referenced by the uxml itself, so what the UI Builder
            // previews is exactly what runs; the code attaches nothing.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.inspector.load_error",
                    "Materilune could not load the inspector UI assets."));
                return root;
            }

            visualTree.CloneTree(root);

            var summary = root.Q<Label>("lbl-summary");
            var openWindowButton = root.Q<Button>("btn-open-window");
            if (summary == null || openWindowButton == null)
            {
                Debug.LogError(MateriluneL10n.Get(
                    "materilune.inspector.missing_element_error",
                    "Materilune inspector UI is missing a required element."));
                return root;
            }

            summary.text = BuildSummary();
            openWindowButton.text = MateriluneL10n.Get(
                "materilune.inspector.open_window",
                "Open in Materilune Window");
            openWindowButton.clicked += MateriluneWindow.ShowWindow;
            return root;
        }

        /// <summary>
        /// Builds the localized summary for the inspected component.
        /// </summary>
        /// <returns>The summary text.</returns>
        protected abstract string BuildSummary();

        /// <summary>
        /// Gets the first live target of the requested type.
        /// </summary>
        /// <typeparam name="T">The target component type.</typeparam>
        /// <returns>The first live target, or <see langword="null" />.</returns>
        protected T GetFirstTarget<T>() where T : UnityEngine.Object
        {
            var inspectedTargets = targets;
            if (inspectedTargets == null)
            {
                return null;
            }

            foreach (var inspectedTarget in inspectedTargets)
            {
                if (inspectedTarget is T typedTarget && typedTarget != null)
                {
                    return typedTarget;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets whether this inspector is showing more than one target.
        /// </summary>
        protected bool HasMultipleTargets => targets != null && targets.Length > 1;

        /// <summary>
        /// Gets the localized text used for an unavailable reference.
        /// </summary>
        protected static string NoneText => MateriluneL10n.Get(
            "materilune.inspector.none",
            "None");

        /// <summary>
        /// Gets the localized text used for a multi-object selection.
        /// </summary>
        protected static string MultipleTargetsText => MateriluneL10n.Get(
            "materilune.inspector.multiple_objects",
            "Multiple objects selected");

        /// <summary>
        /// Gets a display-only object name without using it for object resolution.
        /// </summary>
        /// <param name="objectReference">The object to display.</param>
        /// <returns>The object name or the localized unavailable text.</returns>
        protected static string GetDisplayName(UnityEngine.Object objectReference)
        {
            return objectReference == null ? NoneText : objectReference.name;
        }
    }
}
