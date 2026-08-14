using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Runs undo and redo with third party inspectors detached. Editors such as the VRChat SDK
    /// expressions menu editor stay subscribed to Undo.undoRedoPerformed while enabled and throw
    /// when their user interface was never built, which would fail unrelated tests. Tests call
    /// <see cref="PerformUndo"/> and <see cref="PerformRedo"/> instead of the Undo methods, so
    /// the detaching always happens immediately before the undo runs.
    /// </summary>
    internal static class MateriluneInspectorIsolation
    {
        // Matched by name to avoid referencing the VRChat SDK editor assembly from tests. The
        // editor sits in the global namespace, so the assembly is checked as well to make sure
        // an unrelated type that happens to share the name is left alone.
        private const string ExpressionsMenuEditorTypeName = "VRCExpressionsMenuEditor";
        private const string VrchatAssemblyPrefix = "VRC.";

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
            DetachInspectorEditors();
        }

        /// <summary>
        /// Performs an undo with the inspector editors detached.
        /// </summary>
        internal static void PerformUndo()
        {
            RunWithInspectorsDetached(Undo.PerformUndo);
        }

        /// <summary>
        /// Performs a redo with the inspector editors detached.
        /// </summary>
        internal static void PerformRedo()
        {
            RunWithInspectorsDetached(Undo.PerformRedo);
        }

        /// <summary>
        /// Runs an undo or redo while the inspector cannot rebuild its editors.
        /// </summary>
        /// <param name="undoAction">The undo or redo call to run.</param>
        private static void RunWithInspectorsDetached(Action undoAction)
        {
            DetachInspectorEditors();

            // Undo restores the selection that was recorded with the undone operation, and the
            // inspector rebuilds its editors during that restore, before the undo callbacks
            // run. A rebuilt expressions menu editor subscribes in its OnEnable and then throws
            // from the same undo's callback, because its UI was never built. Destroying editors
            // beforehand cannot prevent that, so the tracker is locked across the call instead,
            // which stops the rebuild itself.
            var tracker = ActiveEditorTracker.sharedTracker;
            var wasLocked = tracker.isLocked;
            tracker.isLocked = true;

            // Destroying the editors and locking the tracker still leaves the failure reachable:
            // an inspector tab that is locked or hidden keeps its own tracker, and an editor
            // created while the undo restores the selection subscribes before the callbacks run.
            // Taking the offending subscriptions out of the callback list for the duration of the
            // call removes the failure at its source, and touches nothing else.
            var suspended = SuspendExpressionsMenuCallbacks();
            try
            {
                undoAction();
            }
            finally
            {
                RestoreCallbacks(suspended);
                tracker.isLocked = wasLocked;
            }
        }

        /// <summary>
        /// Removes the expressions menu editors' undo callbacks and returns them for restoring.
        /// </summary>
        /// <returns>The callbacks that were removed.</returns>
        private static List<Undo.UndoRedoCallback> SuspendExpressionsMenuCallbacks()
        {
            var suspended = new List<Undo.UndoRedoCallback>();

            // Undo.undoRedoPerformed is a plain delegate field rather than an event, so the
            // invocation list can be read and individual handlers removed.
            var current = Undo.undoRedoPerformed;
            if (current == null)
            {
                return suspended;
            }

            foreach (var handler in current.GetInvocationList())
            {
                var callback = (Undo.UndoRedoCallback)handler;
                if (callback.Target != null && IsExpressionsMenuEditor(callback.Target.GetType()))
                {
                    Undo.undoRedoPerformed -= callback;
                    suspended.Add(callback);
                }
            }

            return suspended;
        }

        /// <summary>
        /// Puts back the callbacks whose editor is still alive.
        /// </summary>
        /// <param name="suspended">The callbacks removed before the undo.</param>
        private static void RestoreCallbacks(List<Undo.UndoRedoCallback> suspended)
        {
            foreach (var callback in suspended)
            {
                // An editor destroyed during the undo already unsubscribed, which the removal
                // above turned into a no-op. Putting its callback back would leave a subscription
                // to a dead editor, which is the failure this helper exists to prevent.
                if (callback.Target is Object editor && editor != null)
                {
                    Undo.undoRedoPerformed += callback;
                }
            }
        }

        /// <summary>
        /// Destroys the third party editors that would react to the next undo or redo.
        /// </summary>
        private static void DetachInspectorEditors()
        {
            // Clearing the selection does not reach editors held by locked or hidden inspector
            // tabs, or nested editors another editor leaked. Destroying them runs OnDisable,
            // which removes their Undo.undoRedoPerformed subscription; any live inspector
            // recreates its editor on the next repaint, after the test finished its undo calls.
            // Anything that repaints in between, such as opening a window, brings the editor
            // back, so this runs again right before every undo rather than once per test.
            foreach (var editor in Resources.FindObjectsOfTypeAll<Editor>())
            {
                if (editor != null && IsExpressionsMenuEditor(editor.GetType()))
                {
                    Object.DestroyImmediate(editor);
                }
            }
        }

        private static bool IsExpressionsMenuEditor(Type type)
        {
            return type.FullName == ExpressionsMenuEditorTypeName
                && type.Assembly.GetName().Name.StartsWith(VrchatAssemblyPrefix, StringComparison.Ordinal);
        }
    }
}
