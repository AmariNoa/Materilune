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
            Assert.That(firstOverride.transform.parent.parent, Is.EqualTo(root.transform));
            Assert.That(secondOverride.transform.parent, Is.EqualTo(root.transform));
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
            var userRenderer = CreateRenderer("User", manager.transform, CreateMaterial(shader));
            var originalCount = root.GetComponentsInChildren<MateriluneSwapOverride>(true).Length;
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(FindOverride(root, excludedRenderer), Is.Null);
            Assert.That(FindOverride(root, userRenderer), Is.Null);
            Assert.That(root.GetComponentsInChildren<MateriluneSwapOverride>(true), Has.Length.EqualTo(originalCount));
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

            Assert.That(operationOverride.gameObject, Is.EqualTo(root.gameObject));
            Assert.That(root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(), Is.Not.Null);
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

        [Test]
        public void SetupUndoAndRedoRestoreManagerAndPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform, CreateMaterial(GetShader()));

            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Remove);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(manager == null, Is.True);

            Undo.PerformRedo();
            var restoredManager = FindManager(target);
            Assert.That(restoredManager, Is.Not.Null);
            Assert.That(restoredManager.GetPresets(), Has.Count.EqualTo(1));
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

            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(1));
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
            Assert.That(root.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(), Is.Null);
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

            Assert.That(root.GetComponents<MateriluneSwapOverride>(), Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponents<nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap>(),
                Has.Length.EqualTo(1));
            var operationOverride = root.GetComponent<MateriluneSwapOverride>();
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
            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(1));
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
            Undo.PerformUndo();

            Assert.That(operationObject, Is.Not.Null);
            Assert.That(operationObject.GetComponent<MateriluneSwapOverride>(), Is.Not.Null);
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

        private static MateriluneSwap FindManager(GameObject target)
        {
            foreach (Transform child in target.transform)
            {
                var manager = child.GetComponent<MateriluneSwap>();
                if (manager != null)
                {
                    return manager;
                }
            }

            return null;
        }
    }
}
