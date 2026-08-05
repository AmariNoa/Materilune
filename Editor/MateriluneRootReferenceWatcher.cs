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
                var expected = FindExpectedRoot(operationOverride);
                if (expected == null)
                {
                    continue;
                }

                if (RepairReference(operationOverride.GetComponent<ModularAvatarMaterialSwap>(), expected))
                {
                    repairedCount++;
                }
            }

            foreach (var presetRoot in Object.FindObjectsOfType<MateriluneSwapRoot>(true))
            {
                if (RepairReference(presetRoot.GetComponent<ModularAvatarMaterialSwap>(), presetRoot.SetupTarget))
                {
                    repairedCount++;
                }
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

        /// <summary>
        /// Determines which object an override's material swap has to point at.
        /// </summary>
        /// <param name="operationOverride">The override to inspect.</param>
        /// <returns>The expected root object, or <see langword="null" /> when unknown.</returns>
        private static GameObject FindExpectedRoot(MateriluneSwapOverride operationOverride)
        {
            var targetRenderer = operationOverride.TargetRenderer;
            if (targetRenderer != null)
            {
                return targetRenderer.gameObject;
            }

            // The intermediate override stands for the setup target itself and holds no
            // renderer when the target has none, so the target comes from the owning preset.
            var presetRoot = operationOverride.GetComponentInParent<MateriluneSwapRoot>(true);
            if (presetRoot != null && presetRoot.TargetOverride == operationOverride)
            {
                return presetRoot.SetupTarget;
            }

            return null;
        }

        /// <summary>
        /// Points a material swap at the expected object when its reference does not resolve.
        /// </summary>
        /// <param name="materialSwap">The material swap to repair.</param>
        /// <param name="expected">The object the swap has to point at.</param>
        /// <returns><see langword="true" /> when a repair was made; otherwise, <see langword="false" />.</returns>
        private static bool RepairReference(ModularAvatarMaterialSwap materialSwap, GameObject expected)
        {
            if (materialSwap == null || expected == null)
            {
                return false;
            }

            if (materialSwap.Root != null && materialSwap.Root.Get(materialSwap) == expected)
            {
                return false;
            }

            // The reference resolves against the avatar that holds the material swap, so both
            // sides have to sit under the same avatar. Repairing when they do not would dirty
            // the scene and log on every hierarchy change without the reference ever resolving.
            var expectedAvatar = nadena.dev.ndmf.runtime.RuntimeUtil.FindAvatarInParents(expected.transform);
            if (expectedAvatar == null ||
                expectedAvatar != nadena.dev.ndmf.runtime.RuntimeUtil.FindAvatarInParents(materialSwap.transform))
            {
                return false;
            }

            Undo.RecordObject(
                materialSwap,
                MateriluneL10n.Get("materilune.undo.repair_reference", "Repair Materilune Root Reference"));
            var rootReference = materialSwap.Root;
            if (rootReference == null)
            {
                rootReference = new AvatarObjectReference();
                materialSwap.Root = rootReference;
            }

            rootReference.Set(expected);
            EditorUtility.SetDirty(materialSwap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwap);
            return true;
        }
    }
}
