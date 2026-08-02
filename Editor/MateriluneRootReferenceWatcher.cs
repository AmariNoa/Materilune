using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Repairs material swap references after an object is moved into an avatar hierarchy.
    /// </summary>
    [InitializeOnLoad]
    internal static class MateriluneRootReferenceWatcher
    {
        private static bool s_repairQueued;

        static MateriluneRootReferenceWatcher()
        {
            EditorApplication.hierarchyChanged += QueueRepair;
        }

        private static void QueueRepair()
        {
            if (s_repairQueued)
            {
                return;
            }

            s_repairQueued = true;
            EditorApplication.delayCall += RepairQueuedReferences;
        }

        private static void RepairQueuedReferences()
        {
            s_repairQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            RepairBrokenReferences();
        }

        internal static int RepairBrokenReferences()
        {
            var repairedCount = 0;
            foreach (var operationOverride in Object.FindObjectsOfType<MateriluneSwapOverride>(true))
            {
                var targetRenderer = operationOverride.TargetRenderer;
                if (targetRenderer == null)
                {
                    continue;
                }

                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                if (materialSwap == null)
                {
                    continue;
                }

                if (materialSwap.Root != null &&
                    materialSwap.Root.Get(materialSwap) == targetRenderer.gameObject)
                {
                    continue;
                }

                // Outside an avatar hierarchy the reference cannot be resolved at all, so repairing
                // would dirty the scene and log on every hierarchy change without ever succeeding.
                if (nadena.dev.ndmf.runtime.RuntimeUtil.FindAvatarInParents(targetRenderer.transform) == null)
                {
                    continue;
                }

                Undo.RecordObject(materialSwap, "Repair Materilune Root Reference");
                var rootReference = materialSwap.Root;
                if (rootReference == null)
                {
                    rootReference = new AvatarObjectReference();
                    materialSwap.Root = rootReference;
                }

                rootReference.Set(targetRenderer.gameObject);
                EditorUtility.SetDirty(materialSwap);
                PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwap);
                repairedCount++;
            }

            if (repairedCount > 0)
            {
                Debug.Log(string.Format(
                    MateriluneL10n.Get(
                        "materilune.watcher.repaired",
                        "Materilune repaired {0} material swap reference(s)."),
                    repairedCount));
            }

            return repairedCount;
        }
    }
}
