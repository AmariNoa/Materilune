using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Finds material candidates for material replacement controls.
    /// </summary>
    public static class MateriluneMaterialCandidates
    {
        /// <summary>
        /// Gets material candidates for the specified material and discovery mode.
        /// </summary>
        /// <param name="current">The material currently selected for replacement.</param>
        /// <param name="mode">The candidate discovery mode.</param>
        /// <returns>A sorted list of distinct, non-null material candidates.</returns>
        public static List<Material> GetCandidates(Material current, MateriluneCandidateMode mode)
        {
            var candidates = new List<Material>();
            switch (mode)
            {
                case MateriluneCandidateMode.None:
                    return candidates;
                case MateriluneCandidateMode.SameDirectory:
                case MateriluneCandidateMode.SiblingDirectory:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown candidate mode.");
            }

            if (current == null)
            {
                return candidates;
            }

            var currentPath = AssetDatabase.GetAssetPath(current);
            if (string.IsNullOrEmpty(currentPath))
            {
                return candidates;
            }

            if (mode == MateriluneCandidateMode.SameDirectory)
            {
                return GetSameDirectoryCandidates(current, currentPath);
            }

            return GetSiblingDirectoryCandidates(current, currentPath);
        }

        // AssetDatabase paths use forward slashes, but Path.GetDirectoryName returns the platform
        // separator, which AssetDatabase.FindAssets is not guaranteed to accept in folder filters.
        private static string GetAssetDirectory(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(directory) ? directory : directory.Replace('\\', '/');
        }

        private static List<Material> GetSameDirectoryCandidates(Material current, string currentPath)
        {
            var directory = GetAssetDirectory(currentPath);
            if (string.IsNullOrEmpty(directory))
            {
                return new List<Material>();
            }

            var paths = AssetDatabase.FindAssets("t:Material", new[] { directory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Append(currentPath)
                .Where(path => string.Equals(GetAssetDirectory(path), directory, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal);
            return LoadDistinctMaterials(paths, currentPath, current);
        }

        private static List<Material> GetSiblingDirectoryCandidates(Material current, string currentPath)
        {
            var currentDirectory = GetAssetDirectory(currentPath);
            var parentDirectory = string.IsNullOrEmpty(currentDirectory)
                ? null
                : GetAssetDirectory(currentDirectory);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                return new List<Material>();
            }

            var currentFileName = Path.GetFileNameWithoutExtension(currentPath);
            var paths = AssetDatabase.FindAssets("t:Material", new[] { parentDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Append(currentPath)
                .GroupBy(GetAssetDirectory, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrEmpty(group.Key)
                    && string.Equals(GetAssetDirectory(group.Key), parentDirectory, StringComparison.Ordinal))
                .Select(group => group
                    .OrderBy(path => LevenshteinDistance(Path.GetFileNameWithoutExtension(path), currentFileName))
                    .ThenBy(path => path, StringComparer.Ordinal)
                    .First())
                .OrderBy(path => path, StringComparer.Ordinal);
            return LoadDistinctMaterials(paths, currentPath, current);
        }

        private static List<Material> LoadDistinctMaterials(IEnumerable<string> paths, string currentPath, Material current)
        {
            var candidates = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (var path in paths)
            {
                var material = string.Equals(path, currentPath, StringComparison.Ordinal)
                    ? current
                    : AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && seen.Add(material))
                {
                    candidates.Add(material);
                }
            }

            return candidates;
        }

        private static int LevenshteinDistance(string first, string second)
        {
            first = (first ?? string.Empty).ToLowerInvariant();
            second = (second ?? string.Empty).ToLowerInvariant();
            var previous = new int[second.Length + 1];
            var current = new int[second.Length + 1];

            for (var index = 0; index <= second.Length; index++)
            {
                previous[index] = index;
            }

            for (var firstIndex = 1; firstIndex <= first.Length; firstIndex++)
            {
                current[0] = firstIndex;
                for (var secondIndex = 1; secondIndex <= second.Length; secondIndex++)
                {
                    var substitutionCost = first[firstIndex - 1] == second[secondIndex - 1] ? 0 : 1;
                    current[secondIndex] = Math.Min(
                        Math.Min(previous[secondIndex] + 1, current[secondIndex - 1] + 1),
                        previous[secondIndex - 1] + substitutionCost);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[second.Length];
        }
    }
}
