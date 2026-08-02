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
    public class MateriluneSetupServiceTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();

        /// <summary>
        /// Destroys objects and materials created by the test.
        /// </summary>
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

            m_gameObjects.Clear();

            foreach (var material in m_materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            m_materials.Clear();
            Undo.ClearAll();
        }

        /// <summary>
        /// Verifies target hierarchy branches are recreated for renderers.
        /// </summary>
        [Test]
        public void SetupRecreatesRendererHierarchy()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var branchA = CreateGameObject("A", target.transform);
            var rendererB = CreateRenderer("B", branchA.transform, CreateMaterial(shader));
            var rendererC = CreateRenderer("C", target.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target);
            var overrideB = FindOverride(root, rendererB);
            var overrideC = FindOverride(root, rendererC);

            Assert.That(overrideB, Is.Not.Null);
            Assert.That(overrideC, Is.Not.Null);
            Assert.That(overrideB.transform.parent, Is.Not.EqualTo(root.transform));
            Assert.That(overrideB.transform.parent.parent, Is.EqualTo(root.transform));
            Assert.That(overrideB.transform.parent.GetComponent<MateriluneSwapOverride>(), Is.Null);
            Assert.That(overrideC.transform.parent, Is.EqualTo(root.transform));
        }

        /// <summary>
        /// Verifies branches with no renderers are not recreated.
        /// </summary>
        [Test]
        public void SetupDoesNotCreateBranchesWithoutRenderers()
        {
            GetShader();
            var target = CreateTarget();
            CreateGameObject("D", target.transform);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
            var root = MateriluneSetupService.Setup(target);

            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(1));
        }

        /// <summary>
        /// Verifies duplicate names do not affect renderer associations.
        /// </summary>
        [Test]
        public void SetupDoesNotConfuseSameNamedObjects()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var firstRenderer = CreateRenderer("Same", target.transform, CreateMaterial(shader));
            var secondRenderer = CreateRenderer("Same", target.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target);
            var firstOverride = FindOverride(root, firstRenderer);
            var secondOverride = FindOverride(root, secondRenderer);

            Assert.That(firstOverride, Is.Not.Null);
            Assert.That(secondOverride, Is.Not.Null);
            Assert.That(firstOverride, Is.Not.EqualTo(secondOverride));
            Assert.That(firstOverride.TargetRenderer, Is.EqualTo(firstRenderer));
            Assert.That(secondOverride.TargetRenderer, Is.EqualTo(secondRenderer));
        }

        /// <summary>
        /// Verifies each Material Swap root points to its corresponding renderer object.
        /// </summary>
        [Test]
        public void SetupSetsMaterialSwapRoots()
        {
            var shader = GetShader();
            var target = CreateTarget();
            CreateRenderer("A", target.transform, CreateMaterial(shader));
            CreateRenderer("B", target.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target);
            var operationOverrides = root.GetComponentsInChildren<MateriluneSwapOverride>(true);

            Assert.That(operationOverrides, Is.Not.Empty);
            foreach (var operationOverride in operationOverrides)
            {
                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();
                Assert.That(materialSwap, Is.Not.Null);
                Assert.That(materialSwap.Root, Is.Not.Null);
                Assert.That(materialSwap.Root.Get(materialSwap), Is.EqualTo(operationOverride.TargetRenderer.gameObject));
            }
        }

        /// <summary>
        /// Verifies EditorOnly branches are excluded from setup.
        /// </summary>
        [Test]
        public void SetupExcludesEditorOnlyRenderers()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var editorOnly = CreateGameObject("E", target.transform);
            editorOnly.tag = "EditorOnly";
            var excludedRenderer = CreateRenderer("F", editorOnly.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target);

            Assert.That(FindOverride(root, excludedRenderer), Is.Null);
        }

        /// <summary>
        /// Verifies existing operation objects are not scanned as new renderers.
        /// </summary>
        [Test]
        public void SetupDoesNotRescanMateriluneHierarchy()
        {
            var shader = GetShader();
            var target = CreateTarget();
            CreateRenderer("A", target.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target);
            var firstCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;
            MateriluneSetupService.Setup(target);
            var secondCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;

            Assert.That(secondCount, Is.EqualTo(firstCount));
        }

        /// <summary>
        /// Verifies existing override swap settings are retained during setup.
        /// </summary>
        [Test]
        public void SetupRetainsExistingOverrideSwaps()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("A", target.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target);
            var operationOverride = FindOverride(root, renderer);
            var replacement = CreateMaterial(shader);
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(renderer.sharedMaterial, replacement));

            MateriluneSetupService.Setup(target);

            Assert.That(operationOverride.Swaps, Has.Count.EqualTo(1));
            Assert.That(operationOverride.Swaps[0].From, Is.EqualTo(renderer.sharedMaterial));
            Assert.That(operationOverride.Swaps[0].To, Is.EqualTo(replacement));
        }

        /// <summary>
        /// Verifies root materials contain all renderer materials without duplicates.
        /// </summary>
        [Test]
        public void SetupSetsUniqueRootAvailableMaterials()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var first = CreateMaterial(shader);
            var second = CreateMaterial(shader);
            var third = CreateMaterial(shader);
            CreateRenderer("A", target.transform, first, second);
            CreateRenderer("B", target.transform, second, third);

            var root = MateriluneSetupService.Setup(target);

            CollectionAssert.AreEqual(new[] { first, second, third }, root.AvailableMaterials);
        }

        /// <summary>
        /// Verifies null targets are rejected.
        /// </summary>
        [Test]
        public void SetupThrowsForNullTarget()
        {
            Assert.Throws<ArgumentNullException>(() => MateriluneSetupService.Setup(null));
        }

        /// <summary>
        /// Verifies a renderer on the target itself is configured on the Materilune object.
        /// </summary>
        [Test]
        public void SetupPlacesRendererOnTargetOnMateriluneObject()
        {
            var shader = GetShader();
            var outerParent = CreateGameObject("OuterParent", null);
            var target = CreateGameObject("Target", outerParent.transform);
            target.AddComponent<NDMFAvatarRoot>();
            var targetRenderer = target.AddComponent<MeshRenderer>();
            targetRenderer.sharedMaterials = new[] { CreateMaterial(shader) };

            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var targetOverride = FindOverride(root, targetRenderer);

            Assert.That(targetOverride, Is.Not.Null);
            Assert.That(targetOverride.gameObject, Is.EqualTo(root.gameObject));
            Assert.That(root.GetComponent<ModularAvatarMaterialSwap>(), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies orphaned operation objects are removed with their objects.
        /// </summary>
        [Test]
        public void SetupRemovesOrphanedOperationObject()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("A", target.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var operationObject = FindOverride(root, renderer).gameObject;

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(operationObject == null, Is.True);
        }

        /// <summary>
        /// Verifies orphaned operation objects remain when requested.
        /// </summary>
        [Test]
        public void SetupKeepsOrphanedOperationObject()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("A", target.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var operationObject = FindOverride(root, renderer).gameObject;

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(operationObject, Is.Not.Null);
            Assert.That(operationObject.GetComponent<MateriluneSwapOverride>(), Is.Not.Null);
        }

        /// <summary>
        /// Verifies orphaned components are removed while the Materilune object remains.
        /// </summary>
        [Test]
        public void SetupRemovesOrphanedComponentsOnMateriluneObject()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CreateMaterial(shader) };
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Object.DestroyImmediate(renderer);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.gameObject, Is.Not.Null);
            Assert.That(root.GetComponent<MateriluneSwapRoot>(), Is.SameAs(root));
            Assert.That(root.GetComponent<MateriluneSwapOverride>(), Is.Null);
            Assert.That(root.GetComponent<ModularAvatarMaterialSwap>(), Is.Null);
        }

        /// <summary>
        /// Verifies empty intermediate operation objects are removed with an orphan.
        /// </summary>
        [Test]
        public void SetupRemovesEmptyIntermediateObjects()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var branchA = CreateGameObject("A", target.transform);
            var renderer = CreateRenderer("B", branchA.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(root.transform.childCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies nested renderers reuse operation objects as their parent hierarchy.
        /// </summary>
        [Test]
        public void SetupReusesNestedRendererOperationObject()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var hatRenderer = CreateRenderer("Hat", target.transform, CreateMaterial(shader));
            var featherRenderer = CreateRenderer("Feather", hatRenderer.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var hatOverride = FindOverride(root, hatRenderer);
            var featherOverride = FindOverride(root, featherRenderer);

            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(3));
            Assert.That(hatOverride, Is.Not.Null);
            Assert.That(featherOverride, Is.Not.Null);
            Assert.That(featherOverride.transform.parent, Is.EqualTo(hatOverride.transform));
        }

        /// <summary>
        /// Verifies repeated setup does not add nested renderer operation objects.
        /// </summary>
        [Test]
        public void SetupDoesNotAddNestedRendererOperationObjectsOnRepeat()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var hatRenderer = CreateRenderer("Hat", target.transform, CreateMaterial(shader));
            CreateRenderer("Feather", hatRenderer.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var firstTransformCount = root.GetComponentsInChildren<Transform>(true).Length;
            var firstOverrideCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(firstTransformCount));
            Assert.That(root.GetComponentsInChildren<MateriluneSwapOverride>(true), Has.Length.EqualTo(firstOverrideCount));
        }

        /// <summary>
        /// Verifies an orphaned parent operation object remains as an intermediate object for a valid child.
        /// </summary>
        [Test]
        public void SetupKeepsOrphanedParentOperationObjectForNestedRenderer()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var hatRenderer = CreateRenderer("Hat", target.transform, CreateMaterial(shader));
            var featherRenderer = CreateRenderer("Feather", hatRenderer.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var hatOperationObject = FindOverride(root, hatRenderer).gameObject;

            Object.DestroyImmediate(hatRenderer);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            var featherOverride = FindOverride(root, featherRenderer);
            Assert.That(hatOperationObject, Is.Not.Null);
            Assert.That(hatOperationObject.GetComponent<MateriluneSwapOverride>(), Is.Null);
            Assert.That(hatOperationObject.GetComponent<ModularAvatarMaterialSwap>(), Is.Null);
            Assert.That(featherOverride, Is.Not.Null);
            Assert.That(featherOverride.TargetRenderer, Is.EqualTo(featherRenderer));
        }

        /// <summary>
        /// Verifies undo removes a newly created Materilune hierarchy.
        /// </summary>
        [Test]
        public void SetupUndoRemovesNewHierarchy()
        {
            var shader = GetShader();
            var target = CreateTarget();
            CreateRenderer("A", target.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(root == null, Is.True);
        }

        /// <summary>
        /// Verifies redo restores a newly created Materilune hierarchy.
        /// </summary>
        [Test]
        public void SetupRedoRestoresNewHierarchy()
        {
            var shader = GetShader();
            var target = CreateTarget();
            CreateRenderer("A", target.transform, CreateMaterial(shader));

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Undo.PerformRedo();

            Assert.That(FindRoot(target), Is.Not.Null);
            Assert.That(FindRoot(target).GetComponentsInChildren<MateriluneSwapOverride>(true), Is.Not.Empty);
        }

        /// <summary>
        /// Verifies undo restores an operation object removed as an orphan.
        /// </summary>
        [Test]
        public void SetupUndoRestoresRemovedOrphan()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("A", target.transform, CreateMaterial(shader));
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            var operationObject = FindOverride(root, renderer).gameObject;

            Object.DestroyImmediate(renderer.gameObject);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(operationObject, Is.Not.Null);
            Assert.That(operationObject.GetComponent<MateriluneSwapOverride>(), Is.Not.Null);
        }

        /// <summary>
        /// Verifies nested orphans are removed without touching an already destroyed descendant.
        /// Destroying the ancestor also destroys the descendant, so the removal loop must tolerate
        /// entries that Unity already reports as null.
        /// </summary>
        [Test]
        public void SetupRemovesNestedOrphansWithoutError()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var outerRenderer = CreateRenderer("Hat", target.transform, CreateMaterial(shader));
            var innerRenderer = CreateRenderer("Feather", outerRenderer.transform, CreateMaterial(shader));

            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Object.DestroyImmediate(innerRenderer);
            Object.DestroyImmediate(outerRenderer);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<MateriluneSwapOverride>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(1));
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
            var gameObject = CreateGameObject(name, parent);
            var renderer = gameObject.AddComponent<MeshRenderer>();
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

        private static MateriluneSwapRoot FindRoot(GameObject target)
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
    }
}
