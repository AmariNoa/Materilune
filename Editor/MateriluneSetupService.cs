using System;
using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Specifies how setup handles operation objects without a corresponding renderer.
    /// </summary>
    public enum MateriluneOrphanAction
    {
        /// <summary>
        /// Removes orphaned operation objects.
        /// </summary>
        Remove,

        /// <summary>
        /// Retains orphaned operation objects.
        /// </summary>
        Keep,
    }

    /// <summary>
    /// Creates and updates Materilune operation objects for a target hierarchy.
    /// </summary>
    public static class MateriluneSetupService
    {
        private const string UndoGroupName = "Setup Materilune";

        /// <summary>
        /// Creates or updates the Materilune operation hierarchy for a target object.
        /// </summary>
        /// <param name="target">The object to configure.</param>
        /// <returns>The resolved or created Materilune root component.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public static MateriluneSwapRoot Setup(GameObject target)
        {
            var setupState = PrepareSetup(target);
            var orphanAction = MateriluneOrphanAction.Remove;
            if (setupState.Orphans.Count > 0)
            {
                var remove = EditorUtility.DisplayDialog(
                    MateriluneL10n.Get("materilune.setup.orphan.title", "Materilune Setup"),
                    string.Format(
                        MateriluneL10n.Get(
                            "materilune.setup.orphan.message",
                            "{0} operation object(s) no longer have a corresponding mesh. They will be removed. Continue?"),
                        setupState.Orphans.Count),
                    MateriluneL10n.Get("materilune.setup.orphan.remove", "Remove"),
                    MateriluneL10n.Get("materilune.setup.orphan.cancel", "Cancel"));
                if (!remove)
                {
                    return setupState.Root;
                }
            }

            return ApplySetup(setupState, orphanAction);
        }

        /// <summary>
        /// Creates or updates the Materilune operation hierarchy without displaying dialogs.
        /// </summary>
        /// <param name="target">The object to configure.</param>
        /// <param name="orphanAction">The action to take for orphaned operation objects.</param>
        /// <returns>The resolved or created Materilune root component.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public static MateriluneSwapRoot Setup(GameObject target, MateriluneOrphanAction orphanAction)
        {
            return ApplySetup(PrepareSetup(target), orphanAction);
        }

        private static SetupState PrepareSetup(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var root = FindExistingRoot(target);
            var renderers = CollectTargetRenderers(target);
            var overridesByRenderer = new Dictionary<Renderer, MateriluneSwapOverride>();
            var operationTransformsBySource = new Dictionary<Transform, Transform>();
            var rendererSet = new HashSet<Renderer>(renderers);
            if (root != null)
            {
                RebuildExistingMappings(
                    target,
                    root,
                    rendererSet,
                    overridesByRenderer,
                    operationTransformsBySource);
            }

            return new SetupState(
                target,
                root,
                renderers,
                overridesByRenderer,
                operationTransformsBySource,
                FindOrphans(root, rendererSet));
        }

        private static MateriluneSwapRoot ApplySetup(SetupState setupState, MateriluneOrphanAction orphanAction)
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroupName);

            try
            {
                var root = setupState.Root ?? CreateRoot(setupState.Target);
                if (setupState.Renderers.Count == 0)
                {
                    Debug.LogWarning(MateriluneL10n.Get(
                        "materilune.setup.error.no_renderer",
                        "No renderer was found under the target object."));
                }

                Undo.RecordObject(root, UndoGroupName);
                root.SetupTarget = setupState.Target;

                foreach (var renderer in setupState.Renderers)
                {
                    var operationOverride = GetOrCreateOverride(
                        setupState.Target,
                        root,
                        renderer,
                        setupState.OverridesByRenderer,
                        setupState.OperationTransformsBySource);
                    setupState.OverridesByRenderer[renderer] = operationOverride;
                }

                SetAvailableMaterials(root, setupState.Renderers, setupState.OverridesByRenderer);
                if (orphanAction == MateriluneOrphanAction.Remove)
                {
                    RemoveOrphans(root, setupState.Orphans);
                }

                return root;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static MateriluneSwapRoot FindExistingRoot(GameObject target)
        {
            foreach (Transform child in target.transform)
            {
                var root = child.GetComponent<MateriluneSwapRoot>();
                if (root != null)
                {
                    return root;
                }
            }

            return null;
        }

        private static MateriluneSwapRoot CreateRoot(GameObject target)
        {
            var rootObject = new GameObject("Materilune");
            rootObject.transform.SetParent(target.transform, false);
            Undo.RegisterCreatedObjectUndo(rootObject, UndoGroupName);
            return Undo.AddComponent<MateriluneSwapRoot>(rootObject);
        }

        private static List<Renderer> CollectTargetRenderers(GameObject target)
        {
            var renderers = new List<Renderer>();
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!HasExcludedAncestor(renderer.transform, target.transform))
                {
                    renderers.Add(renderer);
                }
            }

            return renderers;
        }

        private static bool HasExcludedAncestor(Transform transform, Transform targetTransform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.GetComponent<MateriluneSwapRoot>() != null ||
                    current.gameObject.tag == "EditorOnly")
                {
                    return true;
                }

                if (current == targetTransform)
                {
                    break;
                }
            }

            return false;
        }

        private static void RebuildExistingMappings(
            GameObject target,
            MateriluneSwapRoot root,
            ISet<Renderer> renderers,
            IDictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            foreach (var operationOverride in root.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                var renderer = operationOverride.TargetRenderer;
                if (renderer == null ||
                    !renderer.transform.IsChildOf(target.transform) ||
                    !renderers.Contains(renderer))
                {
                    continue;
                }

                if (!overridesByRenderer.ContainsKey(renderer))
                {
                    overridesByRenderer.Add(renderer, operationOverride);
                }

                if (operationOverride.transform == root.transform)
                {
                    continue;
                }

                if (!operationTransformsBySource.ContainsKey(renderer.transform))
                {
                    operationTransformsBySource.Add(renderer.transform, operationOverride.transform);
                }

                var operationTransform = operationOverride.transform.parent;
                var sourceTransform = renderer.transform.parent;
                while (operationTransform != null &&
                       sourceTransform != null &&
                       operationTransform != root.transform &&
                       sourceTransform != target.transform)
                {
                    if (!operationTransformsBySource.ContainsKey(sourceTransform))
                    {
                        operationTransformsBySource.Add(sourceTransform, operationTransform);
                    }

                    operationTransform = operationTransform.parent;
                    sourceTransform = sourceTransform.parent;
                }
            }
        }

        private static List<MateriluneSwapOverride> FindOrphans(
            MateriluneSwapRoot root,
            ISet<Renderer> renderers)
        {
            var orphans = new List<MateriluneSwapOverride>();
            if (root == null)
            {
                return orphans;
            }

            foreach (var operationOverride in root.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                var renderer = operationOverride.TargetRenderer;
                if (renderer == null || !renderers.Contains(renderer))
                {
                    orphans.Add(operationOverride);
                }
            }

            return orphans;
        }

        private static MateriluneSwapOverride GetOrCreateOverride(
            GameObject target,
            MateriluneSwapRoot root,
            Renderer renderer,
            IDictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            if (overridesByRenderer.TryGetValue(renderer, out var operationOverride))
            {
                Undo.RecordObject(operationOverride, UndoGroupName);
                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                if (materialSwap == null)
                {
                    materialSwap = Undo.AddComponent<ModularAvatarMaterialSwap>(operationOverride.gameObject);
                }
                else
                {
                    Undo.RecordObject(materialSwap, UndoGroupName);
                }

                SetMaterialSwapRoot(materialSwap, renderer.gameObject);
                if (renderer.transform != target.transform &&
                    !operationTransformsBySource.ContainsKey(renderer.transform))
                {
                    operationTransformsBySource.Add(renderer.transform, operationOverride.transform);
                }

                return operationOverride;
            }

            GameObject operationObject;
            if (renderer.transform == target.transform)
            {
                operationObject = root.gameObject;
            }
            else
            {
                if (operationTransformsBySource.TryGetValue(renderer.transform, out var operationTransform))
                {
                    operationObject = operationTransform.gameObject;
                }
                else
                {
                    var parent = ResolveOperationParent(
                        renderer.transform.parent,
                        target.transform,
                        root.transform,
                        operationTransformsBySource);
                    operationObject = new GameObject(renderer.name);
                    operationObject.transform.SetParent(parent, false);
                    Undo.RegisterCreatedObjectUndo(operationObject, UndoGroupName);
                    operationTransformsBySource.Add(renderer.transform, operationObject.transform);
                }
            }

            var materialSwapComponent = Undo.AddComponent<ModularAvatarMaterialSwap>(operationObject);
            SetMaterialSwapRoot(materialSwapComponent, renderer.gameObject);
            operationOverride = Undo.AddComponent<MateriluneSwapOverride>(operationObject);
            operationOverride.TargetRenderer = renderer;
            return operationOverride;
        }

        private static Transform ResolveOperationParent(
            Transform sourceTransform,
            Transform targetTransform,
            Transform rootTransform,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            if (sourceTransform == null ||
                sourceTransform == targetTransform ||
                !sourceTransform.IsChildOf(targetTransform))
            {
                return rootTransform;
            }

            if (operationTransformsBySource.TryGetValue(sourceTransform, out var operationTransform))
            {
                return operationTransform;
            }

            var parent = ResolveOperationParent(
                sourceTransform.parent,
                targetTransform,
                rootTransform,
                operationTransformsBySource);
            var operationObject = new GameObject(sourceTransform.name);
            operationObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(operationObject, UndoGroupName);
            operationTransformsBySource.Add(sourceTransform, operationObject.transform);
            return operationObject.transform;
        }

        private static void RemoveOrphans(
            MateriluneSwapRoot root,
            IEnumerable<MateriluneSwapOverride> orphans)
        {
            var orphanObjects = new HashSet<GameObject>();
            var rootOverrides = new List<MateriluneSwapOverride>();
            var componentOnlyOverrides = new List<MateriluneSwapOverride>();
            var orphanSet = new HashSet<MateriluneSwapOverride>(orphans);
            foreach (var operationOverride in orphans)
            {
                if (operationOverride.transform == root.transform)
                {
                    rootOverrides.Add(operationOverride);
                }
                else if (HasNonOrphanDescendant(operationOverride.transform, orphanSet))
                {
                    componentOnlyOverrides.Add(operationOverride);
                }
                else
                {
                    orphanObjects.Add(operationOverride.gameObject);
                }
            }

            foreach (var orphanObject in orphanObjects)
            {
                // Nested orphans are both listed here, and destroying the ancestor already destroyed
                // the descendant. Unity reports the destroyed reference as null.
                if (orphanObject != null)
                {
                    Undo.DestroyObjectImmediate(orphanObject);
                }
            }

            foreach (var operationOverride in componentOnlyOverrides)
            {
                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                if (materialSwap != null)
                {
                    Undo.DestroyObjectImmediate(materialSwap);
                }

                Undo.DestroyObjectImmediate(operationOverride);
            }

            foreach (var operationOverride in rootOverrides)
            {
                if (operationOverride != null)
                {
                    Undo.DestroyObjectImmediate(operationOverride);
                }
            }

            if (rootOverrides.Count > 0)
            {
                var materialSwap = root.GetComponent<ModularAvatarMaterialSwap>();
                if (materialSwap != null)
                {
                    Undo.DestroyObjectImmediate(materialSwap);
                }
            }

            RemoveEmptyIntermediateObjects(root);
        }

        private static bool HasNonOrphanDescendant(
            Transform operationTransform,
            ISet<MateriluneSwapOverride> orphanSet)
        {
            foreach (var descendantOverride in operationTransform.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (descendantOverride.transform != operationTransform &&
                    !orphanSet.Contains(descendantOverride))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveEmptyIntermediateObjects(MateriluneSwapRoot root)
        {
            var transforms = new List<Transform>(root.GetComponentsInChildren<Transform>(true));
            transforms.Sort((left, right) => GetDepth(right).CompareTo(GetDepth(left)));
            foreach (var transform in transforms)
            {
                if (transform == root.transform ||
                    transform.childCount != 0 ||
                    transform.GetComponentsInChildren<MateriluneSwapOverride>(true).Length != 0)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(transform.gameObject);
            }
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            for (var current = transform.parent; current != null; current = current.parent)
            {
                depth++;
            }

            return depth;
        }

        private static void SetMaterialSwapRoot(ModularAvatarMaterialSwap materialSwap, GameObject targetObject)
        {
            var rootReference = materialSwap.Root;
            if (rootReference == null)
            {
                rootReference = new AvatarObjectReference();
                materialSwap.Root = rootReference;
            }

            rootReference.Set(targetObject);
        }

        private static void SetAvailableMaterials(
            MateriluneSwapRoot root,
            IEnumerable<Renderer> renderers,
            IReadOnlyDictionary<Renderer, MateriluneSwapOverride> overridesByRenderer)
        {
            var rootMaterials = new List<Material>();
            var rootMaterialSet = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                var operationOverride = overridesByRenderer[renderer];
                var materials = GetUniqueMaterials(renderer.sharedMaterials);
                Undo.RecordObject(operationOverride, UndoGroupName);
                operationOverride.AvailableMaterials.Clear();
                operationOverride.AvailableMaterials.AddRange(materials);

                foreach (var material in materials)
                {
                    if (rootMaterialSet.Add(material))
                    {
                        rootMaterials.Add(material);
                    }
                }
            }

            Undo.RecordObject(root, UndoGroupName);
            root.AvailableMaterials.Clear();
            root.AvailableMaterials.AddRange(rootMaterials);
        }

        private static List<Material> GetUniqueMaterials(IEnumerable<Material> materials)
        {
            var uniqueMaterials = new List<Material>();
            var materialSet = new HashSet<Material>();
            if (materials == null)
            {
                return uniqueMaterials;
            }

            foreach (var material in materials)
            {
                if (material != null && materialSet.Add(material))
                {
                    uniqueMaterials.Add(material);
                }
            }

            return uniqueMaterials;
        }

        private sealed class SetupState
        {
            internal SetupState(
                GameObject target,
                MateriluneSwapRoot root,
                List<Renderer> renderers,
                Dictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
                Dictionary<Transform, Transform> operationTransformsBySource,
                List<MateriluneSwapOverride> orphans)
            {
                Target = target;
                Root = root;
                Renderers = renderers;
                OverridesByRenderer = overridesByRenderer;
                OperationTransformsBySource = operationTransformsBySource;
                Orphans = orphans;
            }

            internal GameObject Target { get; }

            internal MateriluneSwapRoot Root { get; }

            internal List<Renderer> Renderers { get; }

            internal Dictionary<Renderer, MateriluneSwapOverride> OverridesByRenderer { get; }

            internal Dictionary<Transform, Transform> OperationTransformsBySource { get; }

            internal List<MateriluneSwapOverride> Orphans { get; }
        }
    }
}
