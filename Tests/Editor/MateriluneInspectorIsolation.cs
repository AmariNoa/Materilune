using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Detaches inspector editors before tests perform undo or redo. Third party inspectors
    /// (for example the VRChat SDK expressions menu editor) stay subscribed to
    /// Undo.undoRedoPerformed while enabled and can throw when their UI was never built,
    /// which would fail unrelated tests, so undo and redo run with nothing selected.
    /// </summary>
    internal static class MateriluneInspectorIsolation
    {
        // Matched by name to avoid referencing the VRChat SDK editor assembly from tests.
        private const string ExpressionsMenuEditorTypeName = "VRCExpressionsMenuEditor";

        private static Object[] s_savedSelection;

        /// <summary>
        /// Restores the selection captured by the last <see cref="DeselectAll"/> call.
        /// Call this from TearDown so running tests does not disturb the user's editing session.
        /// </summary>
        internal static void RestoreSelection()
        {
            if (s_savedSelection == null)
            {
                return;
            }

            Selection.objects = s_savedSelection;
            s_savedSelection = null;
        }

        internal static void DeselectAll()
        {
            s_savedSelection ??= Selection.objects;
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();

            // Clearing the selection does not reach editors held by locked or hidden inspector
            // tabs, or nested editors another editor leaked. Destroying them runs OnDisable,
            // which removes their Undo.undoRedoPerformed subscription; any live inspector
            // recreates its editor on the next repaint, after the test finished its undo calls.
            foreach (var editor in Resources.FindObjectsOfTypeAll<Editor>())
            {
                if (editor.GetType().Name == ExpressionsMenuEditorTypeName)
                {
                    Object.DestroyImmediate(editor);
                }
            }
        }
    }
}
