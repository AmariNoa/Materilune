using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using UnityEditor;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Synchronizes Materilune material replacement settings with Modular Avatar components.
    /// </summary>
    public static class MateriluneSwapSynchronizer
    {
        private const string UndoGroupName = "Sync Materilune";

        /// <summary>
        /// Synchronizes composed root and override settings to all operation objects.
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
                var changedCount = 0;
                foreach (var operationOverride in root.GetComponentsInChildren<MateriluneSwapOverride>(true))
                {
                    if (operationOverride.TargetRenderer == null)
                    {
                        continue;
                    }

                    var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                    if (materialSwap == null)
                    {
                        continue;
                    }

                    var composedSwaps = MateriluneSwapComposer.Compose(root.Swaps, operationOverride.Swaps);
                    var swaps = new List<MatSwap>(composedSwaps.Count);
                    foreach (var composedSwap in composedSwaps)
                    {
                        swaps.Add(new MatSwap
                        {
                            From = composedSwap.From,
                            To = composedSwap.To,
                        });
                    }

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
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
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
