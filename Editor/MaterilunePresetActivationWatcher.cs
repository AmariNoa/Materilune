using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Ensures that each Materilune manager has no more than one active preset.
    /// </summary>
    [InitializeOnLoad]
    internal static class MaterilunePresetActivationWatcher
    {
        private static readonly Dictionary<MateriluneSwap, HashSet<MateriluneSwapRoot>> s_previousActivePresets =
            new Dictionary<MateriluneSwap, HashSet<MateriluneSwapRoot>>();
        private static bool s_enforcementQueued;

        static MaterilunePresetActivationWatcher()
        {
            EditorApplication.hierarchyChanged += QueueEnforcement;

            // Without a baseline, the first pass after a domain reload has no history and would
            // keep the first active preset instead of the one the user just activated. Record the
            // current state once, before the user can change anything.
            EditorApplication.delayCall += CaptureInitialState;
        }

        private static void CaptureInitialState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            RemoveDestroyedManagers();
            foreach (var manager in Object.FindObjectsOfType<MateriluneSwap>(true))
            {
                if (!s_previousActivePresets.ContainsKey(manager))
                {
                    RecordActivePresets(manager, manager.GetPresets());
                }
            }
        }

        private static void QueueEnforcement()
        {
            if (s_enforcementQueued)
            {
                return;
            }

            s_enforcementQueued = true;
            EditorApplication.delayCall += EnforceQueuedPresets;
        }

        private static void EnforceQueuedPresets()
        {
            s_enforcementQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            EnforceSingleActivePreset();
        }

        internal static int EnforceSingleActivePreset()
        {
            var deactivatedCount = 0;
            RemoveDestroyedManagers();
            foreach (var manager in Object.FindObjectsOfType<MateriluneSwap>(true))
            {
                var presets = manager.GetPresets();
                var activePresets = new List<MateriluneSwapRoot>();
                foreach (var preset in presets)
                {
                    if (preset.gameObject.activeSelf)
                    {
                        activePresets.Add(preset);
                    }
                }

                if (activePresets.Count > 1)
                {
                    var presetToKeep = ChoosePresetToKeep(manager, activePresets);
                    foreach (var preset in activePresets)
                    {
                        if (preset == presetToKeep)
                        {
                            continue;
                        }

                        Undo.RecordObject(
                            preset.gameObject,
                            MateriluneL10n.Get("materilune.undo.deactivate_preset", "Deactivate Materilune Preset"));
                        preset.gameObject.SetActive(false);
                        EditorUtility.SetDirty(preset.gameObject);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(preset.gameObject);
                        deactivatedCount++;
                    }
                }

                RecordActivePresets(manager, presets);
            }

            if (deactivatedCount > 0)
            {
                Debug.Log(string.Format(
                    MateriluneL10n.Get(
                        "materilune.preset.deactivated",
                        "Materilune deactivated {0} preset(s) to keep a single active preset."),
                    deactivatedCount));
            }

            return deactivatedCount;
        }

        private static MateriluneSwapRoot ChoosePresetToKeep(
            MateriluneSwap manager,
            IList<MateriluneSwapRoot> activePresets)
        {
            if (s_previousActivePresets.TryGetValue(manager, out var previousActivePresets))
            {
                MateriluneSwapRoot newlyActivePreset = null;
                foreach (var preset in activePresets)
                {
                    if (previousActivePresets.Contains(preset))
                    {
                        continue;
                    }

                    if (newlyActivePreset != null)
                    {
                        return activePresets[0];
                    }

                    newlyActivePreset = preset;
                }

                if (newlyActivePreset != null)
                {
                    return newlyActivePreset;
                }
            }

            return activePresets[0];
        }

        private static void RecordActivePresets(MateriluneSwap manager, IEnumerable<MateriluneSwapRoot> presets)
        {
            var activePresets = new HashSet<MateriluneSwapRoot>();
            foreach (var preset in presets)
            {
                if (preset.gameObject.activeSelf)
                {
                    activePresets.Add(preset);
                }
            }

            s_previousActivePresets[manager] = activePresets;
        }

        private static void RemoveDestroyedManagers()
        {
            var destroyedManagers = new List<MateriluneSwap>();
            foreach (var manager in s_previousActivePresets.Keys)
            {
                if (manager == null)
                {
                    destroyedManagers.Add(manager);
                }
            }

            foreach (var manager in destroyedManagers)
            {
                s_previousActivePresets.Remove(manager);
            }
        }
    }
}
