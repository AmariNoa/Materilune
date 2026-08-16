using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides the shared Hierarchy button offset preference.
    /// </summary>
    internal static class MateriluneHierarchyButtonSettings
    {
        private const string SettingsPath = "Preferences/AmariNoa/Hierarchy Buttons";

        /// <summary>
        /// Creates the Hierarchy Buttons preferences page.
        /// </summary>
        /// <returns>The settings provider for the shared button offset.</returns>
        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = MateriluneL10n.Get(
                    "materilune.ui.hierarchy.settings_page_label",
                    "Hierarchy Buttons"),
                guiHandler = OnGUI,
            };
        }

        private static void OnGUI(string searchContext)
        {
            EditorGUI.BeginChangeCheck();
            var extraOffset = EditorGUILayout.FloatField(
                MateriluneL10n.Get(
                    "materilune.ui.hierarchy.extra_offset_label",
                    "Extra offset"),
                EditorPrefs.GetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, 0f));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, extraOffset);
            }

            EditorGUILayout.HelpBox(
                MateriluneL10n.Get(
                    "materilune.ui.hierarchy.extra_offset_description",
                    "Adds horizontal space to the left of the shared Hierarchy buttons."),
                MessageType.Info);
        }
    }
}
