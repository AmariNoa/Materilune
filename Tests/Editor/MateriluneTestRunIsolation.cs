using NUnit.Framework;
using UnityEditor;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Clears the editor selection for the whole test run and restores it afterwards.
    /// </summary>
    /// <remarks>
    /// An undo restores the selection that was current when the undone operation was recorded,
    /// and the inspector rebuilds its editors during that restore. With the user's selection
    /// still active while a test records its operations, every undo brings the user's editors
    /// back mid-call; the VRChat expressions menu editor then subscribes to the undo callback
    /// and throws, because its UI was never built. No intervention before the undo can reach an
    /// editor that is created inside it, so the selection is cleared before anything is
    /// recorded instead. Being a set-up fixture, this covers every test in this namespace,
    /// including ones added later.
    /// </remarks>
    [SetUpFixture]
    public sealed class MateriluneTestRunIsolation
    {
        private Object[] m_savedSelection;

        /// <summary>
        /// Saves and clears the selection before any test runs.
        /// </summary>
        [OneTimeSetUp]
        public void SaveAndClearSelection()
        {
            m_savedSelection = Selection.objects;
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        /// <summary>
        /// Restores the user's selection after the last test finished.
        /// </summary>
        [OneTimeTearDown]
        public void RestoreSelection()
        {
            Selection.objects = m_savedSelection ?? new Object[0];
            m_savedSelection = null;
        }
    }
}
