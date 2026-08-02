using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides the Materilune setup context menu command.
    /// </summary>
    internal static class MateriluneSetupMenu
    {
        [MenuItem("GameObject/Materilune/Setup Materilune", false, 20)]
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
