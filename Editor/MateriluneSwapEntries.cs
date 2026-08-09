using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Rebuilds material replacement entries from the materials available to each component.
    /// </summary>
    public static class MateriluneSwapEntries
    {
        private static string UndoGroupName => MateriluneL10n.Get(
            "materilune.undo.rebuild_entries",
            "Rebuild Materilune Swap Entries");

        /// <summary>
        /// Rebuilds the replacement entries for a preset.
        /// </summary>
        /// <param name="preset">The preset whose entries are rebuilt.</param>
        /// <returns><see langword="true" /> when the entries changed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="preset" /> is null.</exception>
        public static bool Rebuild(MateriluneSwapRoot preset)
        {
            if (ReferenceEquals(preset, null))
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (preset == null)
            {
                return false;
            }

            return RebuildEntries(preset, preset.AvailableMaterials, preset.Swaps);
        }

        /// <summary>
        /// Rebuilds the replacement entries for an operation override.
        /// </summary>
        /// <param name="operationOverride">The override whose entries are rebuilt.</param>
        /// <returns><see langword="true" /> when the entries changed.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="operationOverride" /> is null.
        /// </exception>
        public static bool Rebuild(MateriluneSwapOverride operationOverride)
        {
            if (ReferenceEquals(operationOverride, null))
            {
                throw new ArgumentNullException(nameof(operationOverride));
            }

            if (operationOverride == null)
            {
                return false;
            }

            return RebuildEntries(
                operationOverride,
                operationOverride.AvailableMaterials,
                operationOverride.Swaps);
        }

        /// <summary>
        /// Determines whether the presets have fallen behind the materials currently assigned to
        /// the target meshes.
        /// </summary>
        /// <param name="manager">The manager whose presets are inspected.</param>
        /// <returns><see langword="true" /> when at least one mesh or material is missing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager" /> is null.</exception>
        /// <remarks>
        /// The comparison runs against the renderers as they stand now, not against the material
        /// list each component recorded at setup time. That recorded list is itself what falls
        /// behind, so comparing with it could never report an added mesh or material.
        /// Orphaned entries are not reported, because updating does not remove them.
        /// </remarks>
        public static bool NeedsUpdate(MateriluneSwap manager)
        {
            if (ReferenceEquals(manager, null))
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (manager == null)
            {
                return false;
            }

            foreach (var preset in manager.GetPresets())
            {
                if (preset == null || preset.SetupTarget == null)
                {
                    continue;
                }

                if (PresetNeedsUpdate(preset))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PresetNeedsUpdate(MateriluneSwapRoot preset)
        {
            var renderers = MateriluneSetupService.CollectTargetRenderers(preset.SetupTarget);
            var targetRenderers = new HashSet<Renderer>(renderers);
            var targetMaterials = new List<Material>();
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && !ContainsMaterial(targetMaterials, material))
                    {
                        targetMaterials.Add(material);
                    }
                }
            }

            if (HasMissingMaterial(targetMaterials, preset.Swaps))
            {
                return true;
            }

            var coveredRenderers = new HashSet<Renderer>();
            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride == null)
                {
                    continue;
                }

                // An override whose mesh left the target is orphaned. Updating cannot give it
                // entries, so reporting it would leave the update prompt on for good.
                var renderer = operationOverride.TargetRenderer;
                if (renderer == null || !targetRenderers.Contains(renderer))
                {
                    continue;
                }

                coveredRenderers.Add(renderer);
                if (HasMissingMaterial(
                        GetDistinctMaterials(renderer.sharedMaterials),
                        operationOverride.Swaps))
                {
                    return true;
                }
            }

            // A mesh added after setup has no operation object at all, which the material
            // comparison above cannot see.
            foreach (var renderer in renderers)
            {
                if (renderer != null && !coveredRenderers.Contains(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts the replacement entries a preset and its overrides hold.
        /// </summary>
        /// <param name="preset">The preset to inspect.</param>
        /// <param name="total">Receives the number of entries.</param>
        /// <param name="assigned">Receives the number of entries that name a replacement.</param>
        /// <param name="orphaned">
        /// Receives the number of entries whose source material is no longer offered by the
        /// component that holds them. The test matches the one the synchronizer applies, so the
        /// count says exactly how many entries are held back from the Material Swap components.
        /// </param>
        internal static void CountEntries(
            MateriluneSwapRoot preset,
            out int total,
            out int assigned,
            out int orphaned)
        {
            total = 0;
            assigned = 0;
            orphaned = 0;
            if (preset == null)
            {
                return;
            }

            CountEntries(preset.AvailableMaterials, preset.Swaps, ref total, ref assigned, ref orphaned);
            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride != null)
                {
                    CountEntries(
                        operationOverride.AvailableMaterials,
                        operationOverride.Swaps,
                        ref total,
                        ref assigned,
                        ref orphaned);
                }
            }
        }

        private static void CountEntries(
            IList<Material> availableMaterials,
            IList<MateriluneMaterialSwapEntry> swaps,
            ref int total,
            ref int assigned,
            ref int orphaned)
        {
            if (swaps == null)
            {
                return;
            }

            // An empty material list means the component never recorded what it offers, so the
            // synchronizer skips the orphan test there and this count does the same.
            var canJudgeOrphans = availableMaterials != null && availableMaterials.Count > 0;
            foreach (var swap in swaps)
            {
                total++;
                if (swap.To != null)
                {
                    assigned++;
                }

                if (canJudgeOrphans && !ContainsMaterial(availableMaterials, swap.From))
                {
                    orphaned++;
                }
            }
        }

        private static bool RebuildEntries(
            UnityEngine.Object owner,
            IList<Material> availableMaterials,
            IList<MateriluneMaterialSwapEntry> existingSwaps)
        {
            var desiredMaterials = GetDistinctMaterials(availableMaterials);
            var rebuiltSwaps = new List<MateriluneMaterialSwapEntry>();
            foreach (var material in desiredMaterials)
            {
                rebuiltSwaps.Add(new MateriluneMaterialSwapEntry(
                    material,
                    FindReplacement(existingSwaps, material)));
            }

            if (existingSwaps != null)
            {
                foreach (var existingSwap in existingSwaps)
                {
                    // Only an entry that never held a material is dropped. A reference to a
                    // deleted asset reports itself as null through the Unity operator, and
                    // dropping it would lose the replacement the user chose even though the
                    // asset can come back and resolve the reference again.
                    if (ReferenceEquals(existingSwap.From, null)
                        || ContainsMaterial(desiredMaterials, existingSwap.From))
                    {
                        continue;
                    }

                    rebuiltSwaps.Add(existingSwap);
                }
            }

            if (HasSameEntries(existingSwaps, rebuiltSwaps))
            {
                return false;
            }

            Undo.RecordObject(owner, UndoGroupName);
            existingSwaps.Clear();
            foreach (var rebuiltSwap in rebuiltSwaps)
            {
                existingSwaps.Add(rebuiltSwap);
            }

            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            return true;
        }

        private static List<Material> GetDistinctMaterials(IList<Material> availableMaterials)
        {
            var distinctMaterials = new List<Material>();
            if (availableMaterials == null)
            {
                return distinctMaterials;
            }

            foreach (var material in availableMaterials)
            {
                if (material != null && !ContainsMaterial(distinctMaterials, material))
                {
                    distinctMaterials.Add(material);
                }
            }

            return distinctMaterials;
        }

        private static Material FindReplacement(
            IList<MateriluneMaterialSwapEntry> existingSwaps,
            Material from)
        {
            if (existingSwaps == null)
            {
                return null;
            }

            foreach (var existingSwap in existingSwaps)
            {
                if (existingSwap.From != null && existingSwap.From == from)
                {
                    return existingSwap.To;
                }
            }

            return null;
        }

        private static bool HasMissingMaterial(
            IList<Material> availableMaterials,
            IList<MateriluneMaterialSwapEntry> swaps)
        {
            if (availableMaterials == null)
            {
                return false;
            }

            foreach (var material in availableMaterials)
            {
                if (material != null && !HasFromMaterial(swaps, material))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFromMaterial(
            IList<MateriluneMaterialSwapEntry> swaps,
            Material material)
        {
            if (swaps == null)
            {
                return false;
            }

            foreach (var swap in swaps)
            {
                if (swap.From != null && swap.From == material)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMaterial(IList<Material> materials, Material target)
        {
            if (materials == null || target == null)
            {
                return false;
            }

            foreach (var material in materials)
            {
                if (material != null && material == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSameEntries(
            IList<MateriluneMaterialSwapEntry> current,
            IList<MateriluneMaterialSwapEntry> expected)
        {
            if (current == null || expected == null || current.Count != expected.Count)
            {
                return false;
            }

            for (var index = 0; index < current.Count; index++)
            {
                if (current[index].From != expected[index].From ||
                    current[index].To != expected[index].To)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
