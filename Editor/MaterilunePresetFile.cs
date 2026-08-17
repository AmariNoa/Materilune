using System;
using System.Collections.Generic;
using System.Text;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// The outcome of bringing a preset file into a setup.
    /// </summary>
    public sealed class MaterilunePresetImportResult
    {
        internal MaterilunePresetImportResult(
            MateriluneSwapRoot preset,
            int appliedCount,
            IReadOnlyList<string> missingMaterials,
            IReadOnlyList<string> unmatchedOverrides)
        {
            Preset = preset;
            AppliedCount = appliedCount;
            MissingMaterials = missingMaterials;
            UnmatchedOverrides = unmatchedOverrides;
        }

        /// <summary>Gets the preset that was created, or null when nothing could be.</summary>
        public MateriluneSwapRoot Preset { get; }

        /// <summary>Gets how many replacements were written.</summary>
        public int AppliedCount { get; }

        /// <summary>Gets the display names of materials whose assets were not found.</summary>
        public IReadOnlyList<string> MissingMaterials { get; }

        /// <summary>Gets the stored paths of overrides no renderer answered to.</summary>
        public IReadOnlyList<string> UnmatchedOverrides { get; }
    }

    /// <summary>
    /// Writes a preset to the .mlsp form and builds a preset back from one.
    /// </summary>
    /// <remarks>
    /// The file exists so a preset can travel: out of one project, into another, typically
    /// alongside sold avatar wear whose buyers hold the same material assets. Materials are
    /// identified by asset GUID alone; names ride along purely so a missing one can be named
    /// in the report instead of guessed at. Renderers are identified by their path relative
    /// to the setup target, the one deliberate exception to the no-name-matching rule: across
    /// projects there is no reference to hold on to.
    /// </remarks>
    public static class MaterilunePresetFile
    {
        /// <summary>The file extension, without the dot.</summary>
        public const string Extension = "mlsp";

        private const int CurrentSchema = 1;

        /// <summary>
        /// Writes one preset to the .mlsp JSON form.
        /// </summary>
        /// <param name="preset">The preset to write out.</param>
        /// <returns>The JSON text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="preset"/> is null.</exception>
        public static string ExportToJson(MateriluneSwapRoot preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            var file = new MlspFile
            {
                schema = CurrentSchema,
                presetName = preset.gameObject.name,
                candidateMode = preset.CandidateMode.ToString(),
                rootSwaps = WriteSwaps(preset.Swaps),
                overrides = WriteOverrides(preset),
            };

            return JsonUtility.ToJson(file, true);
        }

        /// <summary>
        /// Builds a new preset on a manager from the .mlsp JSON form.
        /// </summary>
        /// <param name="manager">The manager receiving the preset.</param>
        /// <param name="json">The file contents.</param>
        /// <returns>What was created and what could not be carried over.</returns>
        /// <remarks>
        /// The preset is added the same way a hand-made one is, entries generated from the
        /// meshes, and the file then fills in the replacements it can: a material is used only
        /// when its GUID resolves, and an override only when its stored path matches a
        /// renderer. Everything else is reported and skipped, since a partial import the user
        /// can read about beats a refused one. The whole of it lands in one undo group.
        /// </remarks>
        public static MaterilunePresetImportResult ImportFromJson(MateriluneSwap manager, string json)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("The file is empty.", nameof(json));
            }

            var file = JsonUtility.FromJson<MlspFile>(json);
            if (file == null || file.schema != CurrentSchema)
            {
                throw new ArgumentException(
                    "The file is not a Materilune swap preset this version can read.",
                    nameof(json));
            }

            var missingMaterials = new List<string>();
            var unmatchedOverrides = new List<string>();
            var applied = 0;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            var undoLabel = MateriluneL10n.Get(
                "materilune.undo.import_preset",
                "Import Materilune Preset");
            Undo.SetCurrentGroupName(undoLabel);
            MateriluneSwapRoot preset;
            try
            {
                preset = MateriluneSetupService.AddPreset(manager);
                if (!string.IsNullOrEmpty(file.presetName))
                {
                    Undo.RecordObject(preset.gameObject, undoLabel);
                    preset.gameObject.name = file.presetName;
                }

                MateriluneCandidateMode candidateMode;
                if (Enum.TryParse(file.candidateMode, out candidateMode))
                {
                    Undo.RecordObject(preset, undoLabel);
                    preset.CandidateMode = candidateMode;
                }

                applied += ApplySwaps(preset, preset.Swaps, file.rootSwaps, undoLabel, missingMaterials);

                var setupTarget = preset.SetupTarget;
                foreach (var storedOverride in file.overrides ?? Array.Empty<MlspOverride>())
                {
                    if (storedOverride == null)
                    {
                        continue;
                    }

                    var renderer = ResolveRenderer(setupTarget, storedOverride.rendererPath);
                    var operationOverride = renderer == null ? null : FindOverrideFor(preset, renderer);
                    if (operationOverride == null)
                    {
                        unmatchedOverrides.Add(DescribePath(storedOverride.rendererPath));
                        continue;
                    }

                    MateriluneCandidateMode overrideMode;
                    if (Enum.TryParse(storedOverride.candidateMode, out overrideMode))
                    {
                        Undo.RecordObject(operationOverride, undoLabel);
                        operationOverride.CandidateMode = overrideMode;
                    }

                    applied += ApplySwaps(
                        operationOverride,
                        operationOverride.Swaps,
                        storedOverride.swaps,
                        undoLabel,
                        missingMaterials);
                }

                MateriluneSwapSynchronizer.Sync(manager);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            return new MaterilunePresetImportResult(preset, applied, missingMaterials, unmatchedOverrides);
        }

        private static MlspSwap[] WriteSwaps(List<MateriluneMaterialSwapEntry> swaps)
        {
            var written = new List<MlspSwap>();
            foreach (var swap in swaps ?? new List<MateriluneMaterialSwapEntry>())
            {
                // Rows without a replacement are the resting state of every entry; carrying
                // them across would only pad the file with what the meshes already imply.
                if (swap.From == null || swap.To == null)
                {
                    continue;
                }

                string fromGuid;
                long fromLocalId;
                string toGuid;
                long toLocalId;
                IdentityOf(swap.From, out fromGuid, out fromLocalId);
                IdentityOf(swap.To, out toGuid, out toLocalId);
                written.Add(new MlspSwap
                {
                    fromGuid = fromGuid,
                    fromLocalId = fromLocalId,
                    fromName = swap.From.name,
                    fromPath = AssetDatabase.GetAssetPath(swap.From),
                    toGuid = toGuid,
                    toLocalId = toLocalId,
                    toName = swap.To.name,
                    toPath = AssetDatabase.GetAssetPath(swap.To),
                });
            }

            return written.ToArray();
        }

        private static MlspOverride[] WriteOverrides(MateriluneSwapRoot preset)
        {
            var written = new List<MlspOverride>();
            var setupTarget = preset.SetupTarget;
            if (setupTarget == null)
            {
                return written.ToArray();
            }

            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride == null || operationOverride.TargetRenderer == null)
                {
                    continue;
                }

                var swaps = WriteSwaps(operationOverride.Swaps);
                if (swaps.Length == 0)
                {
                    continue;
                }

                var path = BuildPath(setupTarget.transform, operationOverride.TargetRenderer.transform);
                if (path == null)
                {
                    continue;
                }

                written.Add(new MlspOverride
                {
                    rendererPath = path,
                    candidateMode = operationOverride.CandidateMode.ToString(),
                    swaps = swaps,
                });
            }

            return written.ToArray();
        }

        private static int ApplySwaps(
            UnityEngine.Object component,
            List<MateriluneMaterialSwapEntry> entries,
            MlspSwap[] stored,
            string undoLabel,
            List<string> missingMaterials)
        {
            if (stored == null || stored.Length == 0 || entries == null)
            {
                return 0;
            }

            var applied = 0;
            Undo.RecordObject(component, undoLabel);
            foreach (var storedSwap in stored)
            {
                // A hand-edited or truncated file can hold null entries; JsonUtility fills
                // most of them in as blank objects, and the file is outside input either way.
                // An entry with neither an identity nor a name carries nothing to apply and
                // nothing worth reporting.
                if (storedSwap == null
                    || (string.IsNullOrEmpty(storedSwap.fromGuid) && string.IsNullOrEmpty(storedSwap.fromName)))
                {
                    continue;
                }

                var from = LoadByIdentity(storedSwap.fromGuid, storedSwap.fromLocalId);
                var to = LoadByIdentity(storedSwap.toGuid, storedSwap.toLocalId);
                if (from == null)
                {
                    missingMaterials.Add(DescribeMaterial(storedSwap.fromName, storedSwap.fromPath));
                    continue;
                }

                if (to == null)
                {
                    missingMaterials.Add(DescribeMaterial(storedSwap.toName, storedSwap.toPath));
                    continue;
                }

                for (var index = 0; index < entries.Count; index++)
                {
                    if (entries[index].From == from)
                    {
                        entries[index] = new MateriluneMaterialSwapEntry(from, to);
                        applied++;
                    }
                }
            }

            EditorUtility.SetDirty(component);
            return applied;
        }

        private static MateriluneSwapOverride FindOverrideFor(MateriluneSwapRoot preset, Renderer renderer)
        {
            MateriluneSwapOverride last = null;
            foreach (var candidate in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (candidate != null && candidate.TargetRenderer == renderer)
                {
                    last = candidate;
                }
            }

            return last;
        }

        /// <summary>
        /// Builds the stored path of a renderer relative to the setup target.
        /// </summary>
        /// <returns>The path segments, or <see langword="null" /> when the renderer is outside.</returns>
        /// <remarks>
        /// Each segment carries the sibling index among same-named siblings: Unity allows
        /// twins, and the name alone would land on whichever twin came first.
        /// </remarks>
        private static MlspPathSegment[] BuildPath(Transform setupTarget, Transform rendererTransform)
        {
            var segments = new List<MlspPathSegment>();
            for (var current = rendererTransform; current != setupTarget; current = current.parent)
            {
                if (current == null || current.parent == null)
                {
                    return null;
                }

                var indexAmongSameName = 0;
                foreach (Transform sibling in current.parent)
                {
                    if (sibling == current)
                    {
                        break;
                    }

                    if (sibling.name == current.name)
                    {
                        indexAmongSameName++;
                    }
                }

                segments.Add(new MlspPathSegment { name = current.name, index = indexAmongSameName });
            }

            segments.Reverse();
            return segments.ToArray();
        }

        private static Renderer ResolveRenderer(GameObject setupTarget, MlspPathSegment[] path)
        {
            if (setupTarget == null || path == null)
            {
                return null;
            }

            // An empty path is the setup target itself: walking from the renderer up to the
            // target yields no segments when the two are one object.
            var current = setupTarget.transform;
            foreach (var segment in path)
            {
                Transform next = null;
                var seen = 0;
                foreach (Transform child in current)
                {
                    if (child.name != segment.name)
                    {
                        continue;
                    }

                    if (seen == segment.index)
                    {
                        next = child;
                        break;
                    }

                    seen++;
                }

                if (next == null)
                {
                    return null;
                }

                current = next;
            }

            return current.GetComponent<Renderer>();
        }

        private static string DescribePath(MlspPathSegment[] path)
        {
            if (path == null || path.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var segment in path)
            {
                if (builder.Length > 0)
                {
                    builder.Append('/');
                }

                builder.Append(segment.name);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reads the asset identity of a material: its file's GUID and its id inside the file.
        /// </summary>
        /// <remarks>
        /// The GUID alone names the file, and one file can hold several materials. The local
        /// id inside the file is what tells those apart; without it every sub-asset import
        /// would land on whichever material the file lists first.
        /// </remarks>
        private static void IdentityOf(Material material, out string guid, out long localId)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out guid, out localId))
            {
                guid = string.Empty;
                localId = 0L;
            }
        }

        private static Material LoadByIdentity(string guid, long localId)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Material fallback = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var material = asset as Material;
                if (material == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = material;
                }

                if (localId != 0L
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out _, out long candidateId)
                    && candidateId == localId)
                {
                    return material;
                }
            }

            // Files written before the local id was stored carry a zero, and those files only
            // ever named single-material assets, for which the first material is the answer.
            return localId == 0L ? fallback : fallback;
        }

        private static string DescribeMaterial(string name, string path)
        {
            return string.IsNullOrEmpty(path) ? name : name + " (" + path + ")";
        }

#pragma warning disable SA1307, SA1401
        [Serializable]
        private sealed class MlspFile
        {
            public int schema;
            public string presetName;
            public string candidateMode;
            public MlspSwap[] rootSwaps;
            public MlspOverride[] overrides;
        }

        [Serializable]
        private sealed class MlspSwap
        {
            public string fromGuid;
            public long fromLocalId;
            public string fromName;
            public string fromPath;
            public string toGuid;
            public long toLocalId;
            public string toName;
            public string toPath;
        }

        [Serializable]
        private sealed class MlspOverride
        {
            public MlspPathSegment[] rendererPath;
            public string candidateMode;
            public MlspSwap[] swaps;
        }

        [Serializable]
        private sealed class MlspPathSegment
        {
            public string name;
            public int index;
        }
#pragma warning restore SA1307, SA1401
    }
}
