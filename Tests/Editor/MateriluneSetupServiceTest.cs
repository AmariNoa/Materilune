using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests Materilune setup behavior.
    /// </summary>
    public sealed class MateriluneSetupServiceTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_gameObjects.Count - 1; index >= 0; index--)
            {
                if (m_gameObjects[index] != null)
                {
                    Object.DestroyImmediate(m_gameObjects[index]);
                }
            }

            foreach (var material in m_materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            m_gameObjects.Clear();
            m_materials.Clear();
            Undo.ClearAll();
            MateriluneInspectorIsolation.RestoreSelection();
        }

        [Test]
        public void SetupRecreatesRendererHierarchyWithoutUsingNames()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var branch = CreateGameObject("A", target.transform);
            var firstRenderer = CreateRenderer("Same", branch.transform, CreateMaterial(shader));
            var secondRenderer = CreateRenderer("Same", target.transform, CreateMaterial(shader));

            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var firstOverride = FindOverride(root, firstRenderer);
            var secondOverride = FindOverride(root, secondRenderer);

            Assert.That(firstOverride, Is.Not.Null);
            Assert.That(secondOverride, Is.Not.Null);
            Assert.That(firstOverride, Is.Not.EqualTo(secondOverride));
            var intermediate = FindIntermediate(root);
            Assert.That(firstOverride.transform.parent.parent, Is.EqualTo(intermediate.transform));
            Assert.That(secondOverride.transform.parent, Is.EqualTo(intermediate.transform));
            Assert.That(intermediate.transform.parent, Is.EqualTo(root.transform));
        }

        [Test]
        public void SetupExcludesEditorOnlyAndMateriluneHierarchies()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var editorOnly = CreateGameObject("EditorOnly", target.transform);
            editorOnly.tag = "EditorOnly";
            var excludedRenderer = CreateRenderer("Excluded", editorOnly.transform, CreateMaterial(shader));
            CreateRenderer("Included", target.transform, CreateMaterial(shader));

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var root = GetOnlyPreset(manager);
            var markerRenderer = CreateRenderer("MarkerChild", manager.transform.parent, CreateMaterial(shader));
            var userRenderer = CreateRenderer("User", manager.transform, CreateMaterial(shader));
            var originalCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(FindOverride(root, excludedRenderer), Is.Null);
            Assert.That(FindOverride(root, markerRenderer), Is.Null);
            Assert.That(FindOverride(root, userRenderer), Is.Null);
            Assert.That(root.GetComponentsInChildren<MateriluneSwapOverride>(true), Has.Length.EqualTo(originalCount));
        }

        [Test]
        public void SetupDoesNotExcludeObjectNamedMateriluneWithoutMarkerComponent()
        {
            var target = CreateTarget();
            var nameOnlyObject = CreateGameObject("Materilune", target.transform);
            var renderer = CreateRenderer("Renderer", nameOnlyObject.transform, CreateMaterial(GetShader()));

            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            Assert.That(FindOverride(root, renderer), Is.Not.Null);
        }

        [Test]
        public void SetupReusesExistingMarkerAndDoesNotDuplicateManager()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var marker = manager.transform.parent;
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(target.GetComponentsInChildren<Materilune>(true), Has.Length.EqualTo(1));
            Assert.That(target.GetComponentsInChildren<MateriluneSwap>(true), Has.Length.EqualTo(1));
            Assert.That(manager.transform.parent, Is.SameAs(marker));
        }

        [Test]
        public void SetupCompletesMarkerThatHasNoManager()
        {
            var target = CreateTarget();
            var markerObject = CreateGameObject("CustomMarker", target.transform);
            var marker = markerObject.AddComponent<Materilune>();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(manager.transform.parent, Is.EqualTo(marker.transform));
            Assert.That(target.GetComponentsInChildren<Materilune>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void SetupIncludesInactiveAndSkinnedMeshRenderersAndSetsMaterials()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var first = CreateMaterial(shader);
            var second = CreateMaterial(shader);
            var inactive = CreateRenderer("Inactive", target.transform, first, second);
            inactive.gameObject.SetActive(false);
            var skinnedObject = CreateGameObject("Skinned", target.transform);
            var skinned = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMaterials = new[] { second };

            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            Assert.That(FindOverride(root, inactive), Is.Not.Null);
            Assert.That(FindOverride(root, skinned), Is.Not.Null);
            CollectionAssert.AreEqual(new[] { first, second }, root.AvailableMaterials);
        }

        [Test]
        public void SetupPlacesTargetRendererOnPresetObject()
        {
            var target = CreateTarget();
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CreateMaterial(GetShader()) };

            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var operationOverride = FindOverride(root, renderer);

            var intermediate = FindIntermediate(root);
            Assert.That(operationOverride.gameObject, Is.EqualTo(intermediate.gameObject));
            Assert.That(root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(), Is.Not.Null);
            Assert.That(
                root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>().Root.Get(root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>()),
                Is.EqualTo(target));
        }

        [Test]
        public void SetupRetainsSwapsAndPreservesUserObjects()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var replacement = CreateMaterial(shader);
            var operationOverride = FindOverride(root, renderer);
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(renderer.sharedMaterial, replacement));
            var userObject = CreateGameObject("User", root.transform);

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(operationOverride.Swaps, Has.Count.EqualTo(1));
            Assert.That(userObject, Is.Not.Null);
        }

        [Test]
        public void SetupRemovesOrphansAndKeepsRequiredIntermediateObjects()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var parentRenderer = CreateRenderer("Parent", target.transform, CreateMaterial(shader));
            var childRenderer = CreateRenderer("Child", parentRenderer.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var parentOperationObject = FindOverride(root, parentRenderer).gameObject;

            Object.DestroyImmediate(parentRenderer);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(parentOperationObject, Is.Not.Null);
            Assert.That(parentOperationObject.GetComponent<MateriluneSwapOverride>(), Is.Null);
            Assert.That(FindOverride(root, childRenderer), Is.Not.Null);
        }

        [Test]
        public void SetupKeepsOrphansWhenRequested()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var operationObject = FindOverride(root, renderer).gameObject;

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(operationObject, Is.Not.Null);
        }

        /// <summary>
        /// Verifies the marker is placed ahead of the target's other children.
        /// </summary>
        /// <remarks>
        /// Material Swap gives a contested material to the component it reaches last, and it
        /// reaches a parent's children in order, so a setup nested under a later child has to
        /// come after this one to win.
        /// </remarks>
        [Test]
        public void SetupPutsTheMarkerAheadOfTheOtherChildren()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(manager.transform.parent.GetSiblingIndex(), Is.Zero);
        }

        /// <summary>
        /// Verifies a marker left behind at the end by an earlier version is moved forward.
        /// </summary>
        [Test]
        public void SetupMovesAnExistingMarkerToTheFront()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var marker = manager.transform.parent;
            marker.SetAsLastSibling();
            Assert.That(marker.GetSiblingIndex(), Is.Not.Zero, "the marker should start out of place");

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(marker.GetSiblingIndex(), Is.Zero);
        }

        /// <summary>
        /// Verifies undoing a setup also puts the sibling order back.
        /// </summary>
        /// <remarks>
        /// Moving the marker rearranges the target's children, which is a change to the scene
        /// like any other and has to come back with the rest of the setup.
        /// </remarks>
        [Test]
        public void SetupUndoRestoresTheOriginalSiblingOrder()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));
            var marker = CreateGameObject("Materilune", target.transform);
            marker.AddComponent<Materilune>();
            marker.transform.SetAsLastSibling();
            var originalIndex = marker.transform.GetSiblingIndex();

            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            Undo.FlushUndoRecordObjects();
            Assert.That(marker.transform.GetSiblingIndex(), Is.Zero, "the setup should move the marker");

            MateriluneInspectorIsolation.PerformUndo();
            Assert.That(marker.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));

            MateriluneInspectorIsolation.PerformRedo();

            Assert.That(marker.transform.GetSiblingIndex(), Is.Zero);
        }

        /// <summary>
        /// Verifies the order is called good when nothing ahead of the marker holds a setup.
        /// </summary>
        /// <remarks>
        /// Being at the front is sufficient but not necessary, so a marker that sits after
        /// ordinary objects must not be reported as a problem.
        /// </remarks>
        [Test]
        public void OrderIsGuaranteedWhenNothingAheadHoldsASetup()
        {
            var target = CreateTarget();
            var marker = CreateGameObject("Materilune", target.transform);
            var component = marker.AddComponent<Materilune>();
            CreateGameObject("Plain", target.transform).transform.SetAsFirstSibling();

            Assert.That(MateriluneMarkerOrdering.IsOrderGuaranteed(component), Is.True);
        }

        /// <summary>
        /// Verifies a nested setup ahead of the marker is reported.
        /// </summary>
        [Test]
        public void OrderIsNotGuaranteedWhenANestedSetupComesFirst()
        {
            var target = CreateTarget();
            var marker = CreateGameObject("Materilune", target.transform);
            var component = marker.AddComponent<Materilune>();
            var outfit = CreateGameObject("Outfit", target.transform);
            CreateGameObject("Materilune", outfit.transform).AddComponent<Materilune>();
            outfit.transform.SetAsFirstSibling();

            Assert.That(MateriluneMarkerOrdering.IsOrderGuaranteed(component), Is.False);
        }

        /// <summary>
        /// Verifies a new preset never takes a name an existing preset already wears.
        /// </summary>
        /// <remarks>
        /// The name used to come from the preset count, so one preset renamed to Swap2 counted
        /// straight to a second Swap2. Names decide nothing, but identical rows cannot be told
        /// apart by the person reading them.
        /// </remarks>
        [Test]
        public void AddPresetSkipsNamesAlreadyInUse()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            manager.GetPresets()[0].gameObject.name = "Swap2";

            var added = MateriluneSetupService.AddPreset(manager);

            Assert.That(added.gameObject.name, Is.Not.EqualTo("Swap2"));

            var names = new HashSet<string>();
            foreach (var preset in manager.GetPresets())
            {
                Assert.That(names.Add(preset.gameObject.name), Is.True, "duplicate preset name");
            }
        }

        [Test]
        public void SetupUndoAndRedoRestoreManagerAndPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));

            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();
            Assert.That(manager == null, Is.True);

            MateriluneInspectorIsolation.PerformRedo();
            var restoredManager = FindManager(target);
            Assert.That(restoredManager, Is.Not.Null);
            Assert.That(restoredManager.GetPresets(), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Verifies undoing an orphan removal restores the removed operation object.
        /// </summary>
        [Test]
        public void SetupUndoRestoresRemovedOrphan()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("A", target.transform, CreateMaterial(shader));
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var root = GetOnlyPreset(manager);
            var operationObject = FindOverride(root, renderer).gameObject;

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(operationObject, Is.Not.Null);
            Assert.That(operationObject.GetComponent<MateriluneSwapOverride>(), Is.Not.Null);
        }

        [Test]
        public void SetupThrowsForNullTarget()
        {
            Assert.Throws<ArgumentNullException>(() => MateriluneSetupService.Setup(null));
        }

        [Test]
        public void SetupDoesNotCreateOperationBranchesWithoutRenderers()
        {
            var target = CreateTarget();
            CreateGameObject("Empty", target.transform);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(2));
            Assert.That(FindIntermediate(root), Is.Not.Null);
        }

        [Test]
        public void SetupSetsEveryMaterialSwapRootToTheTargetRenderer()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var first = CreateRenderer("First", target.transform, CreateMaterial(shader));
            var second = CreateRenderer("Second", target.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            foreach (var renderer in new Renderer[] { first, second })
            {
                var operationOverride = FindOverride(root, renderer);
                var materialSwap = operationOverride.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>();
                Assert.That(materialSwap.Root.Get(materialSwap), Is.EqualTo(renderer.gameObject));
            }
        }

        [Test]
        public void SetupRemovesComponentsFromPresetWhenTargetRendererIsRemoved()
        {
            var target = CreateTarget();
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(GetShader());
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            Object.DestroyImmediate(renderer);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(root.GetComponent<MateriluneSwapOverride>(), Is.Null);
            Assert.That(root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(), Is.Not.Null);
            Assert.That(FindIntermediate(root).TargetRenderer, Is.Null);
        }

        [Test]
        public void SetupReusesPresetComponentsWhenTargetRendererIsReplaced()
        {
            var target = CreateTarget();
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(GetShader());
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));

            Object.DestroyImmediate(renderer);
            var replacementRenderer = target.AddComponent<MeshRenderer>();
            replacementRenderer.sharedMaterial = CreateMaterial(GetShader());
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            var intermediate = FindIntermediate(root);
            Assert.That(root.GetComponents<MateriluneSwapOverride>(), Is.Empty);
            Assert.That(intermediate, Is.Not.Null);
            Assert.That(
                root.GetComponents<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(),
                Has.Length.EqualTo(1));
            var operationOverride = intermediate;
            var materialSwap = root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>();
            Assert.That(operationOverride.TargetRenderer, Is.EqualTo(replacementRenderer));
            Assert.That(materialSwap.Root.Get(materialSwap), Is.EqualTo(target));
        }

        [Test]
        public void SetupRemovesEmptyBranchesAndDoesNotDuplicateNestedOperations()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var branch = CreateGameObject("Branch", target.transform);
            var parentRenderer = CreateRenderer("Parent", branch.transform, CreateMaterial(shader));
            CreateRenderer("Child", parentRenderer.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var transformCount = root.GetComponentsInChildren<Transform>(true).Length;
            var overrideCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(transformCount));
            Assert.That(root.GetComponentsInChildren<MateriluneSwapOverride>(true), Has.Length.EqualTo(overrideCount));

            Object.DestroyImmediate(parentRenderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(2));
            Assert.That(FindIntermediate(root), Is.Not.Null);
        }

        [Test]
        public void SetupRetainsIntermediateOperationObjectWithUserComponent()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var sourceIntermediate = CreateGameObject("A", target.transform);
            var renderer = CreateRenderer("B", sourceIntermediate.transform, CreateMaterial(shader));
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            var operationObject = FindOverride(root, renderer).gameObject;
            var operationIntermediate = operationObject.transform.parent.gameObject;
            operationIntermediate.AddComponent<BoxCollider>();

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(operationIntermediate, Is.Not.Null);
            Assert.That(operationIntermediate.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(operationObject == null, Is.True);
        }

        private GameObject CreateTarget()
        {
            var target = CreateGameObject("Target", null);
            target.AddComponent<NDMFAvatarRoot>();
            return target;
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private MeshRenderer CreateRenderer(string name, Transform parent, params Material[] materials)
        {
            var renderer = CreateGameObject(name, parent).AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
        }

        private static Shader GetShader()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            return shader;
        }

        private static MateriluneSwapRoot GetOnlyPreset(MateriluneSwap manager)
        {
            Assert.That(manager.GetPresets(), Has.Count.EqualTo(1));
            return manager.GetPresets()[0];
        }

        private static MateriluneSwapOverride FindOverride(MateriluneSwapRoot root, Renderer renderer)
        {
            foreach (var operationOverride in root.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride.TargetRenderer == renderer)
                {
                    return operationOverride;
                }
            }

            return null;
        }

        private static MateriluneSwapOverride FindIntermediate(MateriluneSwapRoot root)
        {
            MateriluneSwapOverride result = null;
            foreach (Transform child in root.transform)
            {
                var candidate = child.GetComponent<MateriluneSwapOverride>();
                if (candidate == null)
                {
                    continue;
                }

                Assert.That(result, Is.Null);
                result = candidate;
            }

            return result;
        }

        private static MateriluneSwap FindManager(GameObject target)
        {
            foreach (Transform markerChild in target.transform)
            {
                if (markerChild.GetComponent<Materilune>() == null)
                {
                    continue;
                }

                foreach (Transform managerChild in markerChild)
                {
                    var manager = managerChild.GetComponent<MateriluneSwap>();
                    if (manager != null)
                    {
                        return manager;
                    }
                }
            }

            return null;
        }
    }
}
