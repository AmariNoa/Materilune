using System.Collections.Generic;
using UnityEngine;

namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Combines root and renderer-specific material replacement mappings.
    /// </summary>
    public static class MateriluneSwapComposer
    {
        /// <summary>
        /// Combines root mappings with override mappings, giving overrides precedence.
        /// </summary>
        /// <param name="rootSwaps">The root material replacement mappings.</param>
        /// <param name="overrideSwaps">The renderer-specific material replacement mappings.</param>
        /// <returns>The composed material replacement mappings.</returns>
        public static List<MateriluneMaterialSwapEntry> Compose(
            IEnumerable<MateriluneMaterialSwapEntry> rootSwaps,
            IEnumerable<MateriluneMaterialSwapEntry> overrideSwaps)
        {
            var rootEntries = GetLastEntries(rootSwaps);
            var overrideEntries = GetLastEntries(overrideSwaps);

            foreach (var overrideEntry in overrideEntries)
            {
                var rootIndex = FindByFrom(rootEntries, overrideEntry.From);
                if (rootIndex >= 0)
                {
                    rootEntries[rootIndex] = new MateriluneMaterialSwapEntry(rootEntries[rootIndex].From, overrideEntry.To);
                }
                else
                {
                    rootEntries.Add(overrideEntry);
                }
            }

            return rootEntries;
        }

        private static List<MateriluneMaterialSwapEntry> GetLastEntries(
            IEnumerable<MateriluneMaterialSwapEntry> swaps)
        {
            var entries = new List<MateriluneMaterialSwapEntry>();
            if (swaps == null)
            {
                return entries;
            }

            foreach (var entry in swaps)
            {
                if (entry.From == null)
                {
                    continue;
                }

                var existingIndex = FindByFrom(entries, entry.From);
                if (existingIndex >= 0)
                {
                    entries.RemoveAt(existingIndex);
                }

                entries.Add(entry);
            }

            return entries;
        }

        private static int FindByFrom(IList<MateriluneMaterialSwapEntry> entries, Material from)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].From == from)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
