using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Synchronizes Materilune material replacement settings with Modular Avatar components.
    /// </summary>
    public static class MateriluneSwapSynchronizer
    {
        private static string UndoGroupName => MateriluneL10n.Get("materilune.undo.sync", "Sync Materilune");

        /// <summary>
        /// Synchronizes root and override settings to their co-located material swap objects.
        /// </summary>
        /// <param name="root">The Materilune root containing the settings to synchronize.</param>
        /// <returns>The number of material swap components that changed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
        public static int Sync(MateriluneSwapRoot root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroupName);

            try
            {
                return SyncWithoutUndoGroup(root);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        /// <summary>
        /// Synchronizes every preset owned by a Materilune manager.
        /// </summary>
        /// <param name="manager">The manager whose presets are synchronized.</param>
        /// <returns>The total number of material swap components that changed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is null.</exception>
        public static int Sync(MateriluneSwap manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroupName);
            try
            {
                var changedCount = 0;
                foreach (var preset in manager.GetPresets())
                {
                    changedCount += SyncWithoutUndoGroup(preset);
                }

                return changedCount;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static int SyncWithoutUndoGroup(MateriluneSwapRoot root)
        {
            var changedCount = 0;
            var rootMaterialSwap = root.GetComponent<ModularAvatarMaterialSwap>();
            if (rootMaterialSwap != null)
            {
                var rootSwaps = CreateSwaps(root.Swaps, root.AvailableMaterials);
                if (!HasSameSwaps(rootMaterialSwap.Swaps, rootSwaps))
                {
                    Undo.RecordObject(rootMaterialSwap, UndoGroupName);
                    rootMaterialSwap.Swaps = rootSwaps;
                    EditorUtility.SetDirty(rootMaterialSwap);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rootMaterialSwap);
                    changedCount++;
                }
            }

            foreach (var operationOverride in root.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride == null)
                {
                    continue;
                }

                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                if (materialSwap == null)
                {
                    continue;
                }

                var swaps = CreateSwaps(operationOverride.Swaps, operationOverride.AvailableMaterials);

                if (HasSameSwaps(materialSwap.Swaps, swaps))
                {
                    continue;
                }

                Undo.RecordObject(materialSwap, UndoGroupName);
                materialSwap.Swaps = swaps;
                EditorUtility.SetDirty(materialSwap);
                PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwap);
                changedCount++;
            }

            return changedCount;
        }

        private static List<MatSwap> CreateSwaps(
            IList<MateriluneMaterialSwapEntry> source,
            IList<Material> availableMaterials)
        {
            var swaps = new List<MatSwap>();
            if (source == null)
            {
                return swaps;
            }

            foreach (var sourceSwap in source)
            {
                if (sourceSwap.From == null || sourceSwap.To == null ||
                    (availableMaterials != null && availableMaterials.Count > 0 &&
                     !availableMaterials.Contains(sourceSwap.From)))
                {
                    continue;
                }

                swaps.Add(new MatSwap
                {
                    From = sourceSwap.From,
                    To = sourceSwap.To,
                });
            }

            return swaps;
        }

        private static bool HasSameSwaps(IList<MatSwap> current, IList<MatSwap> expected)
        {
            if (current == null || expected == null)
            {
                return current == expected;
            }

            if (current.Count != expected.Count)
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
