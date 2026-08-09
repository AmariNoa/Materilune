using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Adds a non-blocking warning to Renderer inspector headers managed by Materilune.
    /// </summary>
    [InitializeOnLoad]
    internal static class MateriluneRendererInspectorWarning
    {
        private static readonly HashSet<Renderer> s_managedRenderers = new HashSet<Renderer>();
        private static bool s_cacheBuilt;

        static MateriluneRendererInspectorWarning()
        {
            Editor.finishedDefaultHeaderGUI += DrawWarning;
            EditorApplication.hierarchyChanged += InvalidateCache;
            Undo.undoRedoPerformed += InvalidateCache;
        }

        /// <summary>
        /// Determines whether a renderer is managed by a Materilune override.
        /// </summary>
        /// <param name="renderer">The renderer to inspect.</param>
        /// <returns><see langword="true" /> when an override references the renderer.</returns>
        internal static bool IsManaged(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            EnsureCache();
            return s_managedRenderers.Contains(renderer);
        }

        /// <summary>
        /// Invalidates the cached renderer references after a hierarchy or undo change.
        /// </summary>
        internal static void InvalidateCache()
        {
            s_cacheBuilt = false;
            s_managedRenderers.Clear();
        }

        private static void DrawWarning(Editor inspector)
        {
            if (inspector == null || inspector.targets == null)
            {
                return;
            }

            foreach (var inspectedTarget in inspector.targets)
            {
                if (ShouldWarnFor(inspectedTarget))
                {
                    EditorGUILayout.HelpBox(
                        MateriluneL10n.Get(
                            "materilune.warning.managed_by_materilune",
                            "The materials of this renderer are managed by Materilune."),
                        MessageType.Warning);
                    return;
                }
            }
        }

        /// <summary>
        /// Determines whether the object whose header is being drawn carries a managed renderer.
        /// </summary>
        /// <param name="inspectedTarget">The object the header belongs to.</param>
        /// <returns><see langword="true" /> when the warning applies.</returns>
        /// <remarks>
        /// The header event carries the object the inspector header belongs to, which for a
        /// scene selection is the game object rather than each component drawn below it. Testing
        /// only for a Renderer target would therefore never match, so the renderers the game
        /// object holds are tested as well.
        /// </remarks>
        internal static bool ShouldWarnFor(Object inspectedTarget)
        {
            if (inspectedTarget is Renderer renderer)
            {
                return IsManaged(renderer);
            }

            if (!(inspectedTarget is GameObject gameObject))
            {
                return false;
            }

            foreach (var candidate in gameObject.GetComponents<Renderer>())
            {
                if (IsManaged(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureCache()
        {
            if (s_cacheBuilt)
            {
                return;
            }

            s_managedRenderers.Clear();
            foreach (var operationOverride in Object.FindObjectsOfType<MateriluneSwapOverride>(true))
            {
                if (operationOverride != null && operationOverride.TargetRenderer != null)
                {
                    s_managedRenderers.Add(operationOverride.TargetRenderer);
                }
            }

            s_cacheBuilt = true;
        }
    }
}
