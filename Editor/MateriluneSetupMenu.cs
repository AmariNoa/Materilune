using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides the Materilune setup context menu command.
    /// </summary>
    internal static class MateriluneSetupMenu
    {
        // Modular Avatar assigns -1000..-997 to its GameObject items; staying within 10 of that
        // block keeps the Materilune submenu directly below it, with no separator in between.
        [MenuItem("GameObject/Materilune/Setup Materilune", false, -990)]
        private static void SetupMaterilune(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null)
            {
                target = Selection.activeGameObject;
            }

            if (target == null)
            {
                Debug.LogWarning(MateriluneL10n.Get(
                    "materilune.setup.error.no_target",
                    "No target object is selected."));
                return;
            }

            var root = MateriluneSetupService.Setup(target);
            Selection.activeObject = root.gameObject;
        }

        [MenuItem("GameObject/Materilune/Setup Materilune", true)]
        private static bool ValidateSetupMaterilune()
        {
            return Selection.activeGameObject != null &&
                   !EditorUtility.IsPersistent(Selection.activeGameObject);
        }
    }
}
