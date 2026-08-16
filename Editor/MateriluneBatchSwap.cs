using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Why a row of a batch replacement can or cannot be applied.
    /// </summary>
    public enum MateriluneBatchSwapStatus
    {
        /// <summary>The row has a replacement waiting and nothing of its own to lose.</summary>
        Ready,

        /// <summary>The row has a replacement waiting, over a value already chosen for it.</summary>
        Overwrite,

        /// <summary>The source material's name does not contain what the rule replaces.</summary>
        NotMatched,

        /// <summary>The name the rule produces belongs to no material this row can offer.</summary>
        NoCandidate,
    }

    /// <summary>
    /// A name substitution learned from one example replacement.
    /// </summary>
    public readonly struct MateriluneBatchSwapRule
    {
        private MateriluneBatchSwapRule(string from, string to)
        {
            From = from;
            To = to;
        }

        /// <summary>Gets the part of a name the rule replaces.</summary>
        public string From { get; }

        /// <summary>Gets what the rule puts in its place.</summary>
        public string To { get; }

        /// <summary>Gets a value indicating whether the rule can be applied.</summary>
        public bool IsValid => !string.IsNullOrEmpty(From) && To != null && From != To;

        /// <summary>
        /// Works out what one example replacement changes about a name.
        /// </summary>
        /// <param name="sample">The material the example replaces.</param>
        /// <param name="replacement">The material the example replaces it with.</param>
        /// <returns>The rule, invalid when the two names give nothing to go on.</returns>
        /// <remarks>
        /// Colour variants of one material are usually named alike apart from the colour, so
        /// the difference between the two names is the part worth carrying to the other rows.
        /// Trimming the shared beginning and the shared end leaves exactly that difference.
        /// When nothing is removed, only added ("Pink" to "PinkPastel"), that trimming leaves
        /// nothing to look for in the other names, so the whole sample name becomes the rule
        /// instead. It matches fewer rows than a difference would, but names like these are
        /// common in real data, and refusing them would fail exactly where the feature helps.
        /// </remarks>
        public static MateriluneBatchSwapRule Learn(Material sample, Material replacement)
        {
            if (sample == null || replacement == null)
            {
                return default;
            }

            var before = sample.name;
            var after = replacement.name;
            if (string.IsNullOrEmpty(before) || string.IsNullOrEmpty(after) || before == after)
            {
                return default;
            }

            var prefix = 0;
            while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < before.Length - prefix
                   && suffix < after.Length - prefix
                   && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
            {
                suffix++;
            }

            var from = before.Substring(prefix, before.Length - prefix - suffix);
            if (from.Length == 0)
            {
                return new MateriluneBatchSwapRule(before, after);
            }

            return new MateriluneBatchSwapRule(
                from,
                after.Substring(prefix, after.Length - prefix - suffix));
        }

        /// <summary>
        /// Applies the rule to a name.
        /// </summary>
        /// <param name="name">The name to transform.</param>
        /// <returns>The transformed name, or <see langword="null" /> when the rule does not apply.</returns>
        /// <remarks>
        /// Every occurrence is replaced. A name that repeats the colour, once in the body and
        /// once in a suffix, is the case this is for; replacing only the first would produce a
        /// name that names no material.
        /// </remarks>
        public string Apply(string name)
        {
            if (!IsValid || string.IsNullOrEmpty(name) || !name.Contains(From))
            {
                return null;
            }

            return name.Replace(From, To);
        }
    }

    /// <summary>
    /// One row of a batch replacement, as it would be applied.
    /// </summary>
    public sealed class MateriluneBatchSwapPlanItem
    {
        internal MateriluneBatchSwapPlanItem(
            int index,
            Material from,
            Material to,
            string expectedName,
            MateriluneBatchSwapStatus status)
        {
            Index = index;
            From = from;
            To = to;
            ExpectedName = expectedName;
            Status = status;
        }

        /// <summary>Gets the position of the row among the component's entries.</summary>
        public int Index { get; }

        /// <summary>Gets the material the row replaces.</summary>
        public Material From { get; }

        /// <summary>Gets the material the row would be given, or null when there is none.</summary>
        public Material To { get; }

        /// <summary>Gets the name the rule produced, or null when the rule did not apply.</summary>
        public string ExpectedName { get; }

        /// <summary>Gets why this row can or cannot be applied.</summary>
        public MateriluneBatchSwapStatus Status { get; }

        /// <summary>Gets a value indicating whether this row can be applied at all.</summary>
        public bool IsApplicable =>
            Status == MateriluneBatchSwapStatus.Ready || Status == MateriluneBatchSwapStatus.Overwrite;
    }

    /// <summary>
    /// Works out what a batch replacement would do, without doing any of it.
    /// </summary>
    /// <remarks>
    /// Nothing here touches the scene. The window shows the result and applies only the rows
    /// that are ticked, so the planning stays separate from the change.
    /// </remarks>
    public static class MateriluneBatchSwap
    {
        /// <summary>
        /// Works out what a rule would do to each entry of a component.
        /// </summary>
        /// <param name="entries">The entries of the component being edited.</param>
        /// <param name="rule">The rule learned from the example.</param>
        /// <param name="preferredMode">The component's candidate mode, used as an ordering.</param>
        /// <returns>One item per entry, in the same order.</returns>
        /// <remarks>
        /// A replacement is looked for only among the candidates that row already offers, so a
        /// batch can never reach a material the row's own picker would not show. Searching the
        /// whole project would be easier and is deliberately not done. What the picker offers
        /// is both of its tabs, not one: the component's stored mode only decides which tab it
        /// opens on, and its default is None, which finds nothing at all if taken as the range.
        /// So both real modes are searched, the preferred one first.
        /// </remarks>
        public static List<MateriluneBatchSwapPlanItem> Plan(
            IReadOnlyList<MateriluneMaterialSwapEntry> entries,
            MateriluneBatchSwapRule rule,
            MateriluneCandidateMode preferredMode)
        {
            var items = new List<MateriluneBatchSwapPlanItem>();
            if (entries == null || !rule.IsValid)
            {
                return items;
            }

            // One candidate list per distinct source material: rows of one component often
            // share a source, and the search reads the asset database every time.
            var candidatesBySource = new Dictionary<Material, List<Material>>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var from = entry.From;
                var expectedName = from == null ? null : rule.Apply(from.name);
                if (expectedName == null)
                {
                    items.Add(new MateriluneBatchSwapPlanItem(
                        index, from, null, null, MateriluneBatchSwapStatus.NotMatched));
                    continue;
                }

                if (!candidatesBySource.TryGetValue(from, out var candidates))
                {
                    candidates = new List<Material>();
                    foreach (var searchMode in OrderModes(preferredMode))
                    {
                        candidates.AddRange(MateriluneMaterialCandidates.GetCandidates(from, searchMode));
                    }

                    candidatesBySource[from] = candidates;
                }

                var match = FindByName(candidates, expectedName);
                if (match == null)
                {
                    items.Add(new MateriluneBatchSwapPlanItem(
                        index, from, null, expectedName, MateriluneBatchSwapStatus.NoCandidate));
                    continue;
                }

                items.Add(new MateriluneBatchSwapPlanItem(
                    index,
                    from,
                    match,
                    expectedName,
                    entry.To == null
                        ? MateriluneBatchSwapStatus.Ready
                        : MateriluneBatchSwapStatus.Overwrite));
            }

            return items;
        }

        /// <summary>
        /// Puts the component's preferred mode first among the modes that actually search.
        /// </summary>
        /// <param name="preferredMode">The component's candidate mode.</param>
        /// <returns>The search order.</returns>
        /// <remarks>
        /// The order matters when both tabs hold a material of the wanted name: the first list
        /// searched supplies the match, so the preferred tab's material wins, which is also the
        /// one the picker would have shown first.
        /// </remarks>
        private static MateriluneCandidateMode[] OrderModes(MateriluneCandidateMode preferredMode)
        {
            if (preferredMode == MateriluneCandidateMode.SiblingDirectory)
            {
                return new[]
                {
                    MateriluneCandidateMode.SiblingDirectory,
                    MateriluneCandidateMode.SameDirectory,
                };
            }

            return new[]
            {
                MateriluneCandidateMode.SameDirectory,
                MateriluneCandidateMode.SiblingDirectory,
            };
        }

        private static Material FindByName(IReadOnlyList<Material> candidates, string name)
        {
            if (candidates == null)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                if (candidate != null && candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
