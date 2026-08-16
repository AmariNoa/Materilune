using com.amari_noa.materilune.runtime;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Finds the replacement an enclosing Materilune setup already applies to a material.
    /// </summary>
    /// <remarks>
    /// Setups can be nested: one on the avatar root and another on an outfit below it, for
    /// instance. Material Swap lets the deeper one win, so a material the inner setup leaves
    /// alone still gets whatever the outer setup assigns to it. Without that shown, the window
    /// disagrees with the avatar. Everything here is read-only; the outer setup is edited by
    /// opening it in the window.
    /// </remarks>
    public static class MateriluneInheritedSwaps
    {
        /// <summary>
        /// Finds the replacement an enclosing setup applies to a material of one renderer.
        /// </summary>
        /// <param name="operationOverride">The per-mesh component being edited.</param>
        /// <param name="from">The material being replaced.</param>
        /// <returns>The inherited replacement, or <see langword="null" /> when there is none.</returns>
        /// <remarks>
        /// An enclosing setup can address this renderer in two ways: through its own per-mesh
        /// component for the same renderer, or through the whole-preset replacements that cover
        /// every mesh. The per-mesh one sits deeper and therefore wins, matching the order
        /// Material Swap itself applies.
        /// </remarks>
        public static Material ResolveForOverride(MateriluneSwapOverride operationOverride, Material from)
        {
            if (operationOverride == null || from == null)
            {
                return null;
            }

            var renderer = operationOverride.TargetRenderer;
            foreach (var preset in EnumerateEnclosingActivePresets(operationOverride.transform))
            {
                if (renderer != null)
                {
                    var perMesh = FindOverrideFor(preset, renderer);
                    var deeper = perMesh == null ? null : FindReplacement(perMesh.Swaps, from);
                    if (deeper != null)
                    {
                        return deeper;
                    }
                }

                var wholePreset = FindReplacement(preset.Swaps, from);
                if (wholePreset != null)
                {
                    return wholePreset;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the replacement an enclosing setup applies to a material of every mesh.
        /// </summary>
        /// <param name="preset">The preset whose whole-preset replacements are being edited.</param>
        /// <param name="from">The material being replaced.</param>
        /// <returns>The inherited replacement, or <see langword="null" /> when there is none.</returns>
        /// <remarks>
        /// Only the enclosing whole-preset replacements are consulted. An enclosing per-mesh
        /// component covers one renderer, so its value is not what this panel applies to, and
        /// showing it here would claim more than it does.
        /// </remarks>
        public static Material ResolveForRoot(MateriluneSwapRoot preset, Material from)
        {
            if (preset == null || from == null)
            {
                return null;
            }

            foreach (var enclosing in EnumerateEnclosingActivePresets(preset.transform))
            {
                var replacement = FindReplacement(enclosing.Swaps, from);
                if (replacement != null)
                {
                    return replacement;
                }
            }

            return null;
        }

        /// <summary>
        /// Walks outwards from a component, yielding the active preset of each setup above it.
        /// </summary>
        /// <param name="start">The transform of the component being edited.</param>
        /// <returns>The active presets, nearest setup first.</returns>
        /// <remarks>
        /// Nearest first is what the caller wants: among several enclosing setups the deepest
        /// one wins, so the first match is the answer. A setup whose preset is switched off
        /// contributes nothing, since only one preset of a setup is ever active.
        /// </remarks>
        private static System.Collections.Generic.IEnumerable<MateriluneSwapRoot>
            EnumerateEnclosingActivePresets(Transform start)
        {
            // The component being edited sits inside its own setup, so the walk begins above
            // the object that setup was applied to. Otherwise a setup would inherit from itself.
            var setupTarget = FindSetupTarget(start);
            var ancestor = setupTarget == null ? null : setupTarget.parent;
            for (; ancestor != null; ancestor = ancestor.parent)
            {
                var manager = FindManagerOn(ancestor);
                if (manager == null)
                {
                    continue;
                }

                foreach (var preset in manager.GetPresets())
                {
                    if (preset != null && preset.gameObject.activeSelf)
                    {
                        yield return preset;
                    }
                }
            }
        }

        /// <summary>
        /// Finds the object a setup was applied to, starting from a component inside it.
        /// </summary>
        /// <param name="start">The transform of the component being edited.</param>
        /// <returns>The setup target, or <see langword="null" /> when it cannot be found.</returns>
        private static Transform FindSetupTarget(Transform start)
        {
            for (var current = start; current != null; current = current.parent)
            {
                // The marker sits directly under the object the setup was applied to, so the
                // object holding it is the target. Matching is by component, never by name.
                if (current.GetComponent<Materilune>() != null)
                {
                    return current.parent;
                }
            }

            return null;
        }

        private static MateriluneSwap FindManagerOn(Transform candidate)
        {
            foreach (Transform child in candidate)
            {
                if (child == null || child.GetComponent<Materilune>() == null)
                {
                    continue;
                }

                foreach (Transform grandChild in child)
                {
                    var manager = grandChild == null ? null : grandChild.GetComponent<MateriluneSwap>();
                    if (manager != null)
                    {
                        return manager;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the component of a preset that addresses one renderer.
        /// </summary>
        /// <param name="preset">The preset to search.</param>
        /// <param name="renderer">The renderer being addressed.</param>
        /// <returns>The component, or <see langword="null" /> when the preset has none.</returns>
        /// <remarks>
        /// A preset can hold more than one component pointing at the same renderer: the layer
        /// standing for the target object itself addresses that object's own renderer, and so
        /// does the per-mesh object made for it. The deepest one is what Material Swap applies,
        /// so it is what gets reported here.
        /// </remarks>
        private static MateriluneSwapOverride FindOverrideFor(MateriluneSwapRoot preset, Renderer renderer)
        {
            MateriluneSwapOverride deepest = null;
            var deepestDepth = -1;
            foreach (var candidate in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                // Reference equality only. The hierarchy may hold several objects of one name,
                // so matching on names would pair the wrong ones up.
                if (candidate == null || candidate.TargetRenderer != renderer)
                {
                    continue;
                }

                var depth = DepthOf(candidate.transform);
                if (depth > deepestDepth)
                {
                    deepest = candidate;
                    deepestDepth = depth;
                }
            }

            return deepest;
        }

        private static int DepthOf(Transform transform)
        {
            var depth = 0;
            for (var current = transform; current != null; current = current.parent)
            {
                depth++;
            }

            return depth;
        }

        private static Material FindReplacement(
            System.Collections.Generic.List<MateriluneMaterialSwapEntry> swaps,
            Material from)
        {
            if (swaps == null)
            {
                return null;
            }

            foreach (var swap in swaps)
            {
                if (swap.From == from && swap.To != null)
                {
                    return swap.To;
                }
            }

            return null;
        }
    }
}
