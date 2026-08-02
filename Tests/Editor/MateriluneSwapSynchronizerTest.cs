using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests synchronization of Materilune settings to Modular Avatar material swaps.
    /// </summary>
    public sealed class MateriluneSwapSynchronizerTest
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
        }

        [Test]
        public void SyncExpandsRootSettingsToAllOperationObjects()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var firstRenderer = CreateRenderer("First", target.transform, from);
            var secondRenderer = CreateRenderer("Second", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSwapSynchronizer.Sync(root);

            AssertSwap(FindMaterialSwap(root, firstRenderer), from, to);
            AssertSwap(FindMaterialSwap(root, secondRenderer), from, to);
        }

        [Test]
        public void SyncGivesOverridePrecedenceOverRootSettings()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var rootReplacement = CreateMaterial(shader);
            var overrideReplacement = CreateMaterial(shader);
            var firstRenderer = CreateRenderer("First", target.transform, from);
            var secondRenderer = CreateRenderer("Second", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, rootReplacement));
            FindOverride(root, firstRenderer).Swaps.Add(
                new MateriluneMaterialSwapEntry(from, overrideReplacement));

            MateriluneSwapSynchronizer.Sync(root);

            AssertSwap(FindMaterialSwap(root, firstRenderer), from, overrideReplacement);
            AssertSwap(FindMaterialSwap(root, secondRenderer), from, rootReplacement);
        }

        [Test]
        public void SyncGivesOverridePrecedenceForRendererOnSetupTarget()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var rootReplacement = CreateMaterial(shader);
            var overrideReplacement = CreateMaterial(shader);
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { from };
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var operationOverride = FindOverride(root, renderer);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, rootReplacement));
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(from, overrideReplacement));

            MateriluneSwapSynchronizer.Sync(root);

            Assert.That(operationOverride.gameObject, Is.EqualTo(root.gameObject));
            AssertSwap(root.GetComponent<ModularAvatarMaterialSwap>(), from, overrideReplacement);
        }

        [Test]
        public void SyncDoesNotChangeAlreadySynchronizedSwaps()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            CreateRenderer("Renderer", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            var firstChangedCount = MateriluneSwapSynchronizer.Sync(root);
            var secondChangedCount = MateriluneSwapSynchronizer.Sync(root);

            Assert.That(firstChangedCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(secondChangedCount, Is.EqualTo(0));
        }

        [Test]
        public void SyncCanBeUndone()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var renderer = CreateRenderer("Renderer", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            Undo.ClearAll();
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSwapSynchronizer.Sync(root);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(FindMaterialSwap(root, renderer).Swaps, Is.Empty);
        }

        [Test]
        public void SyncAppliesOverrideSettingsWithoutRootSettings()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var firstRenderer = CreateRenderer("First", target.transform, from);
            var secondRenderer = CreateRenderer("Second", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            FindOverride(root, firstRenderer).Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSwapSynchronizer.Sync(root);

            AssertSwap(FindMaterialSwap(root, firstRenderer), from, to);
            Assert.That(FindMaterialSwap(root, secondRenderer).Swaps, Is.Empty);
        }

        [Test]
        public void SyncThrowsForNullRoot()
        {
            Assert.Throws<ArgumentNullException>(() => MateriluneSwapSynchronizer.Sync(null));
        }

        [Test]
        public void SetupSynchronizesRootSettings()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var renderer = CreateRenderer("Renderer", target.transform, from);
            var root = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            AssertSwap(FindMaterialSwap(root, renderer), from, to);
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

        private static ModularAvatarMaterialSwap FindMaterialSwap(MateriluneSwapRoot root, Renderer renderer)
        {
            return FindOverride(root, renderer).GetComponent<ModularAvatarMaterialSwap>();
        }

        private static void AssertSwap(ModularAvatarMaterialSwap materialSwap, Material from, Material to)
        {
            Assert.That(materialSwap, Is.Not.Null);
            Assert.That(materialSwap.Swaps, Has.Count.EqualTo(1));
            Assert.That(materialSwap.Swaps[0].From, Is.EqualTo(from));
            Assert.That(materialSwap.Swaps[0].To, Is.EqualTo(to));
        }
    }
}
