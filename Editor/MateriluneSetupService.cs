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
        /// <summary>Removes orphaned operation objects.</summary>
        Remove,

        /// <summary>Retains orphaned operation objects.</summary>
        Keep,
    }

    /// <summary>
    /// Creates and updates Materilune operation objects for a target hierarchy.
    /// </summary>
    public static class MateriluneSetupService
    {
        private static string UndoGroupName => MateriluneL10n.Get("materilune.undo.setup", "Setup Materilune");

        /// <summary>
        /// Creates or updates the Materilune operation hierarchy for a target object.
        /// </summary>
        /// <param name="target">The object to configure.</param>
        /// <returns>The resolved or created Materilune preset manager.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public static MateriluneSwap Setup(GameObject target)
        {
            var setupState = PrepareSetup(target);
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
                    return setupState.Manager;
                }
            }

            return ApplySetup(setupState, MateriluneOrphanAction.Remove);
        }

        /// <summary>
        /// Creates or updates the Materilune operation hierarchy without displaying dialogs.
        /// </summary>
        /// <param name="target">The object to configure.</param>
        /// <param name="orphanAction">The action to take for orphaned operation objects.</param>
        /// <returns>The resolved or created Materilune preset manager.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public static MateriluneSwap Setup(GameObject target, MateriluneOrphanAction orphanAction)
        {
            return ApplySetup(PrepareSetup(target), orphanAction);
        }

        /// <summary>
        /// Adds an inactive, empty preset that mirrors the manager's setup target.
        /// </summary>
        /// <param name="manager">The manager that owns the new preset.</param>
        /// <returns>The newly created preset root.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is null.</exception>
        public static MateriluneSwapRoot AddPreset(MateriluneSwap manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            // Resolve before the undo group opens: a manager outside a complete Materilune
            // hierarchy has no target, and continuing would build the preset against nothing.
            var target = GetTargetObject(manager);
            if (target == null)
            {
                throw new ArgumentException(
                    "The manager is not placed under a Materilune marker inside a target object.",
                    nameof(manager));
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            var undoLabel = MateriluneL10n.Get("materilune.undo.add_preset", "Add Materilune Preset");
            Undo.SetCurrentGroupName(undoLabel);
            try
            {
                var renderers = CollectTargetRenderers(target);
                var preset = CreatePreset(manager, "Swap" + (manager.GetPresets().Count + 1));
                var presetState = new PresetState(
                    preset,
                    new Dictionary<Renderer, MateriluneSwapOverride>(),
                    new Dictionary<Transform, Transform>(),
                    new List<MateriluneSwapOverride>());
                ApplyPreset(target, renderers, presetState, MateriluneOrphanAction.Keep);
                Undo.RecordObject(preset.gameObject, undoLabel);
                preset.gameObject.SetActive(false);
                return preset;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static SetupState PrepareSetup(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var manager = FindExistingManager(target);
            var renderers = CollectTargetRenderers(target);
            var rendererSet = new HashSet<Renderer>(renderers);
            var presetStates = new List<PresetState>();
            var allOrphans = new List<MateriluneSwapOverride>();
            if (manager != null)
            {
                foreach (var preset in manager.GetPresets())
                {
                    var overridesByRenderer = new Dictionary<Renderer, MateriluneSwapOverride>();
                    var operationTransformsBySource = new Dictionary<Transform, Transform>();
                    RebuildExistingMappings(
                        target,
                        preset,
                        rendererSet,
                        overridesByRenderer,
                        operationTransformsBySource);
                    var orphans = FindOrphans(target, preset, rendererSet);
                    presetStates.Add(new PresetState(
                        preset,
                        overridesByRenderer,
                        operationTransformsBySource,
                        orphans));
                    allOrphans.AddRange(orphans);
                }
            }

            return new SetupState(target, manager, renderers, presetStates, allOrphans);
        }

        private static MateriluneSwap ApplySetup(SetupState setupState, MateriluneOrphanAction orphanAction)
        {
            if (orphanAction != MateriluneOrphanAction.Remove && orphanAction != MateriluneOrphanAction.Keep)
            {
                throw new ArgumentOutOfRangeException(nameof(orphanAction), orphanAction, "Unknown orphan action.");
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroupName);
            try
            {
                var manager = setupState.Manager ?? CreateManager(setupState.Target);
                if (setupState.Renderers.Count == 0)
                {
                    Debug.LogWarning(MateriluneL10n.Get(
                        "materilune.setup.error.no_renderer",
                        "No renderer was found under the target object."));
                }

                if (setupState.Presets.Count == 0)
                {
                    var preset = CreatePreset(manager, "Swap1");
                    setupState.Presets.Add(new PresetState(
                        preset,
                        new Dictionary<Renderer, MateriluneSwapOverride>(),
                        new Dictionary<Transform, Transform>(),
                        new List<MateriluneSwapOverride>()));
                }

                foreach (var presetState in setupState.Presets)
                {
                    ApplyPreset(setupState.Target, setupState.Renderers, presetState, orphanAction);
                }

                MateriluneSwapSynchronizer.Sync(manager);
                return manager;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void ApplyPreset(
            GameObject target,
            IList<Renderer> renderers,
            PresetState presetState,
            MateriluneOrphanAction orphanAction)
        {
            var preset = presetState.Root;
            var intermediateOverride = EnsureIntermediate(target, preset);

            presetState.OverridesByRenderer.Clear();
            presetState.OperationTransformsBySource.Clear();
            RebuildExistingMappings(
                target,
                preset,
                renderers.Count == 0 ? new HashSet<Renderer>() : new HashSet<Renderer>(renderers),
                presetState.OverridesByRenderer,
                presetState.OperationTransformsBySource);

            var presetMaterialSwap = preset.GetComponent<ModularAvatarMaterialSwap>();
            if (presetMaterialSwap == null)
            {
                presetMaterialSwap = Undo.AddComponent<ModularAvatarMaterialSwap>(preset.gameObject);
            }
            else
            {
                Undo.RecordObject(presetMaterialSwap, UndoGroupName);
            }

            SetMaterialSwapRoot(presetMaterialSwap, target);
            EditorUtility.SetDirty(presetMaterialSwap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(presetMaterialSwap);

            Undo.RecordObject(preset, UndoGroupName);
            preset.SetupTarget = target;
            EditorUtility.SetDirty(preset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(preset);
            foreach (var renderer in renderers)
            {
                var operationOverride = GetOrCreateOverride(
                    target,
                    preset,
                    renderer,
                    presetState.OverridesByRenderer,
                    presetState.OperationTransformsBySource);
                presetState.OverridesByRenderer[renderer] = operationOverride;
            }

            SetAvailableMaterials(preset, renderers, presetState.OverridesByRenderer);
            if (orphanAction == MateriluneOrphanAction.Remove && presetState.Orphans.Count > 0)
            {
                RemoveOrphans(preset, presetState.Orphans, new HashSet<Renderer>(renderers));
            }
        }

        private static MateriluneSwapOverride EnsureIntermediate(
            GameObject target,
            MateriluneSwapRoot preset)
        {
            var intermediateOverride = FindIntermediateOverride(preset);
            if (intermediateOverride == null)
            {
                var intermediateObject = new GameObject(target.name);
                intermediateObject.transform.SetParent(preset.transform, false);
                Undo.RegisterCreatedObjectUndo(intermediateObject, UndoGroupName);
                intermediateOverride = Undo.AddComponent<MateriluneSwapOverride>(intermediateObject);
            }
            else
            {
                Undo.RecordObject(intermediateOverride, UndoGroupName);
            }

            if (preset.TargetOverride != intermediateOverride)
            {
                Undo.RecordObject(preset, UndoGroupName);
                preset.TargetOverride = intermediateOverride;
                EditorUtility.SetDirty(preset);
                PrefabUtility.RecordPrefabInstancePropertyModifications(preset);
            }

            intermediateOverride.TargetRenderer = target.GetComponent<Renderer>();
            EditorUtility.SetDirty(intermediateOverride);
            PrefabUtility.RecordPrefabInstancePropertyModifications(intermediateOverride);

            var materialSwap = intermediateOverride.GetComponent<ModularAvatarMaterialSwap>();
            if (materialSwap == null)
            {
                materialSwap = Undo.AddComponent<ModularAvatarMaterialSwap>(intermediateOverride.gameObject);
            }
            else
            {
                Undo.RecordObject(materialSwap, UndoGroupName);
            }

            SetMaterialSwapRoot(materialSwap, target);
            EditorUtility.SetDirty(materialSwap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwap);
            return intermediateOverride;
        }

        /// <summary>
        /// Returns the override that stands for the setup target itself.
        /// </summary>
        /// <param name="preset">The preset to inspect.</param>
        /// <returns>The stored override, or <see langword="null" /> when the preset has none.</returns>
        private static MateriluneSwapOverride FindIntermediateOverride(MateriluneSwapRoot preset)
        {
            // Held by reference on the preset. Locating it by position or by the presence of a
            // renderer cannot tell it apart from an operation object left by an older layout,
            // and a wrong match would move a mesh's settings onto another mesh.
            var storedOverride = preset.TargetOverride;
            if (storedOverride == null || storedOverride.transform.parent != preset.transform)
            {
                return null;
            }

            return storedOverride;
        }

        private static MateriluneSwap FindExistingManager(GameObject target)
        {
            // Every marker is inspected, not just the first one. Stopping at the first marker
            // would miss a manager held by a later one and build a duplicate hierarchy.
            foreach (Transform markerChild in target.transform)
            {
                if (markerChild.GetComponent<Materilune>() == null)
                {
                    continue;
                }

                foreach (Transform child in markerChild)
                {
                    var manager = child.GetComponent<MateriluneSwap>();
                    if (manager != null)
                    {
                        return manager;
                    }
                }
            }

            return null;
        }

        private static Materilune FindExistingMarker(GameObject target)
        {
            foreach (Transform child in target.transform)
            {
                var marker = child.GetComponent<Materilune>();
                if (marker != null)
                {
                    return marker;
                }
            }

            return null;
        }

        private static MateriluneSwap CreateManager(GameObject target)
        {
            var marker = FindExistingMarker(target);
            if (marker == null)
            {
                var markerObject = new GameObject("Materilune");
                markerObject.transform.SetParent(target.transform, false);
                Undo.RegisterCreatedObjectUndo(markerObject, UndoGroupName);
                marker = Undo.AddComponent<Materilune>(markerObject);
            }

            foreach (Transform child in marker.transform)
            {
                var existingManager = child.GetComponent<MateriluneSwap>();
                if (existingManager != null)
                {
                    return existingManager;
                }
            }

            var managerObject = new GameObject("Material Swap");
            managerObject.transform.SetParent(marker.transform, false);
            Undo.RegisterCreatedObjectUndo(managerObject, UndoGroupName);
            return Undo.AddComponent<MateriluneSwap>(managerObject);
        }

        private static MateriluneSwapRoot CreatePreset(MateriluneSwap manager, string displayName)
        {
            var presetObject = new GameObject(displayName);
            presetObject.transform.SetParent(manager.transform, false);
            Undo.RegisterCreatedObjectUndo(presetObject, UndoGroupName);
            return Undo.AddComponent<MateriluneSwapRoot>(presetObject);
        }

        internal static List<Renderer> CollectTargetRenderers(GameObject target)
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

        /// <summary>
        /// Determines whether an object marks the start of an area excluded from the mesh scan.
        /// </summary>
        /// <param name="transform">The object to test.</param>
        /// <returns><see langword="true" /> when the object and its children are excluded.</returns>
        internal static bool IsExcludedObject(Transform transform)
        {
            return transform.GetComponent<Materilune>() != null ||
                transform.GetComponent<MateriluneSwap>() != null ||
                transform.GetComponent<MateriluneSwapRoot>() != null ||
                transform.gameObject.tag == "EditorOnly";
        }

        private static bool HasExcludedAncestor(Transform transform, Transform targetTransform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (IsExcludedObject(current))
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

        private static GameObject GetTargetObject(MateriluneSwap manager)
        {
            if (manager == null)
            {
                return null;
            }

            // The target is only well defined when the manager sits under a marker that in turn
            // sits under the target. Falling back to the manager or the marker itself when the
            // hierarchy is incomplete would point the swaps at the wrong object, and a marker
            // nested inside another marker has no target of its own.
            var marker = manager.transform.parent;
            if (marker == null || marker.GetComponent<Materilune>() == null || marker.parent == null)
            {
                return null;
            }

            return marker.parent.GetComponent<Materilune>() != null
                ? null
                : marker.parent.gameObject;
        }

        private static void RebuildExistingMappings(
            GameObject target,
            MateriluneSwapRoot preset,
            ISet<Renderer> renderers,
            IDictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            var intermediateOverride = FindIntermediateOverride(preset);
            var operationRoot = intermediateOverride != null
                ? intermediateOverride.transform
                : preset.transform;
            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride == null)
                {
                    continue;
                }

                var renderer = operationOverride.TargetRenderer;
                if (renderer == null || !renderer.transform.IsChildOf(target.transform) || !renderers.Contains(renderer))
                {
                    continue;
                }

                if (!overridesByRenderer.ContainsKey(renderer))
                {
                    overridesByRenderer.Add(renderer, operationOverride);
                }

                if (intermediateOverride != null && operationOverride == intermediateOverride)
                {
                    continue;
                }

                if (!operationTransformsBySource.ContainsKey(renderer.transform))
                {
                    operationTransformsBySource.Add(renderer.transform, operationOverride.transform);
                }

                var operationTransform = operationOverride.transform.parent;
                var sourceTransform = renderer.transform.parent;
                while (operationTransform != null && sourceTransform != null &&
                       operationTransform != operationRoot && sourceTransform != target.transform)
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
            GameObject target,
            MateriluneSwapRoot preset,
            ISet<Renderer> renderers)
        {
            var orphans = new List<MateriluneSwapOverride>();
            var intermediateOverride = FindIntermediateOverride(preset);
            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride == null || operationOverride == intermediateOverride)
                {
                    continue;
                }

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
            MateriluneSwapRoot preset,
            Renderer renderer,
            IDictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            var intermediateOverride = FindIntermediateOverride(preset);
            var operationRoot = intermediateOverride != null
                ? intermediateOverride.transform
                : preset.transform;
            if (overridesByRenderer.TryGetValue(renderer, out var operationOverride))
            {
                Undo.RecordObject(operationOverride, UndoGroupName);
                operationOverride.TargetRenderer = renderer;
                EditorUtility.SetDirty(operationOverride);
                PrefabUtility.RecordPrefabInstancePropertyModifications(operationOverride);
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
                EditorUtility.SetDirty(materialSwap);
                PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwap);
                if (renderer.transform != target.transform && !operationTransformsBySource.ContainsKey(renderer.transform))
                {
                    operationTransformsBySource.Add(renderer.transform, operationOverride.transform);
                }

                return operationOverride;
            }

            GameObject operationObject;
            if (renderer.transform == target.transform)
            {
                operationObject = operationRoot.gameObject;
            }
            else if (operationTransformsBySource.TryGetValue(renderer.transform, out var operationTransform))
            {
                operationObject = operationTransform.gameObject;
            }
            else
            {
                var parent = ResolveOperationParent(
                    renderer.transform.parent,
                    target.transform,
                    operationRoot,
                    operationTransformsBySource);
                operationObject = new GameObject(renderer.name);
                operationObject.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(operationObject, UndoGroupName);
                operationTransformsBySource.Add(renderer.transform, operationObject.transform);
            }

            var existingOverride = operationObject.GetComponent<MateriluneSwapOverride>();
            if (existingOverride != null)
            {
                Undo.RecordObject(existingOverride, UndoGroupName);
                existingOverride.TargetRenderer = renderer;
                EditorUtility.SetDirty(existingOverride);
                PrefabUtility.RecordPrefabInstancePropertyModifications(existingOverride);
                operationOverride = existingOverride;
            }
            else
            {
                operationOverride = Undo.AddComponent<MateriluneSwapOverride>(operationObject);
                operationOverride.TargetRenderer = renderer;
                EditorUtility.SetDirty(operationOverride);
                PrefabUtility.RecordPrefabInstancePropertyModifications(operationOverride);
            }

            var materialSwapComponent = operationObject.GetComponent<ModularAvatarMaterialSwap>();
            if (materialSwapComponent == null)
            {
                materialSwapComponent = Undo.AddComponent<ModularAvatarMaterialSwap>(operationObject);
            }
            else
            {
                Undo.RecordObject(materialSwapComponent, UndoGroupName);
            }

            SetMaterialSwapRoot(materialSwapComponent, renderer.gameObject);
            if (materialSwapComponent != null)
            {
                EditorUtility.SetDirty(materialSwapComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(materialSwapComponent);
            }

            return operationOverride;
        }

        private static Transform ResolveOperationParent(
            Transform sourceTransform,
            Transform targetTransform,
            Transform presetTransform,
            IDictionary<Transform, Transform> operationTransformsBySource)
        {
            if (sourceTransform == null || sourceTransform == targetTransform || !sourceTransform.IsChildOf(targetTransform))
            {
                return presetTransform;
            }

            if (operationTransformsBySource.TryGetValue(sourceTransform, out var operationTransform))
            {
                return operationTransform;
            }

            var parent = ResolveOperationParent(
                sourceTransform.parent,
                targetTransform,
                presetTransform,
                operationTransformsBySource);
            var operationObject = new GameObject(sourceTransform.name);
            operationObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(operationObject, UndoGroupName);
            operationTransformsBySource.Add(sourceTransform, operationObject.transform);
            return operationObject.transform;
        }

        private static void RemoveOrphans(
            MateriluneSwapRoot preset,
            IEnumerable<MateriluneSwapOverride> orphans,
            ISet<Renderer> renderers)
        {
            var orphanObjects = new HashSet<GameObject>();
            var orphanAncestors = new HashSet<Transform>();
            var rootOverrides = new List<MateriluneSwapOverride>();
            var componentOnlyOverrides = new List<MateriluneSwapOverride>();
            var orphanSet = new HashSet<MateriluneSwapOverride>();
            foreach (var operationOverride in orphans)
            {
                if (operationOverride == null)
                {
                    continue;
                }

                var targetRenderer = operationOverride.TargetRenderer;
                if (targetRenderer != null && renderers.Contains(targetRenderer))
                {
                    continue;
                }

                orphanSet.Add(operationOverride);
            }

            foreach (var operationOverride in orphanSet)
            {
                if (operationOverride.transform == preset.transform)
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
                    for (var ancestor = operationOverride.transform.parent;
                         ancestor != null && ancestor != preset.transform;
                         ancestor = ancestor.parent)
                    {
                        orphanAncestors.Add(ancestor);
                    }
                }
            }

            foreach (var orphanObject in orphanObjects)
            {
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

            RemoveOrphanAncestors(orphanAncestors);
        }

        private static bool HasNonOrphanDescendant(Transform operationTransform, ISet<MateriluneSwapOverride> orphanSet)
        {
            foreach (var descendantOverride in operationTransform.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (descendantOverride != null && descendantOverride.transform != operationTransform &&
                    !orphanSet.Contains(descendantOverride))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveOrphanAncestors(IEnumerable<Transform> ancestors)
        {
            // Ancestors of a nested orphan include other orphans that were just destroyed, and
            // the sort comparer would walk their parents. Drop destroyed entries before sorting.
            var transforms = new List<Transform>();
            foreach (var ancestor in ancestors)
            {
                if (ancestor != null)
                {
                    transforms.Add(ancestor);
                }
            }

            transforms.Sort((left, right) => GetDepth(right).CompareTo(GetDepth(left)));
            foreach (var transform in transforms)
            {
                if (transform == null || transform.childCount != 0 ||
                    transform.GetComponent<MateriluneSwapOverride>() != null ||
                    transform.GetComponents<Component>().Length != 1)
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
            MateriluneSwapRoot preset,
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
                EditorUtility.SetDirty(operationOverride);
                PrefabUtility.RecordPrefabInstancePropertyModifications(operationOverride);
                foreach (var material in materials)
                {
                    if (rootMaterialSet.Add(material))
                    {
                        rootMaterials.Add(material);
                    }
                }
            }

            Undo.RecordObject(preset, UndoGroupName);
            preset.AvailableMaterials.Clear();
            preset.AvailableMaterials.AddRange(rootMaterials);
            EditorUtility.SetDirty(preset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(preset);
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
                MateriluneSwap manager,
                List<Renderer> renderers,
                List<PresetState> presets,
                List<MateriluneSwapOverride> orphans)
            {
                Target = target;
                Manager = manager;
                Renderers = renderers;
                Presets = presets;
                Orphans = orphans;
            }

            internal GameObject Target { get; }
            internal MateriluneSwap Manager { get; }
            internal List<Renderer> Renderers { get; }
            internal List<PresetState> Presets { get; }
            internal List<MateriluneSwapOverride> Orphans { get; }
        }

        private sealed class PresetState
        {
            internal PresetState(
                MateriluneSwapRoot root,
                Dictionary<Renderer, MateriluneSwapOverride> overridesByRenderer,
                Dictionary<Transform, Transform> operationTransformsBySource,
                List<MateriluneSwapOverride> orphans)
            {
                Root = root;
                OverridesByRenderer = overridesByRenderer;
                OperationTransformsBySource = operationTransformsBySource;
                Orphans = orphans;
            }

            internal MateriluneSwapRoot Root { get; }
            internal Dictionary<Renderer, MateriluneSwapOverride> OverridesByRenderer { get; }
            internal Dictionary<Transform, Transform> OperationTransformsBySource { get; }
            internal List<MateriluneSwapOverride> Orphans { get; }
        }
    }
}
