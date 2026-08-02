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
            MateriluneInspectorIsolation.RestoreSelection();
        }

        [Test]
        public void SyncExpandsRootSettingsAndGivesOverridesPrecedence()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var rootReplacement = CreateMaterial(shader);
            var overrideReplacement = CreateMaterial(shader);
            var firstRenderer = CreateRenderer("First", target.transform, from);
            var secondRenderer = CreateRenderer("Second", target.transform, from);
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, rootReplacement));
            FindOverride(root, firstRenderer).Swaps.Add(new MateriluneMaterialSwapEntry(from, overrideReplacement));

            MateriluneSwapSynchronizer.Sync(root);

            AssertSwap(FindMaterialSwap(root, firstRenderer), from, overrideReplacement);
            AssertSwap(FindMaterialSwap(root, secondRenderer), from, rootReplacement);
        }

        [Test]
        public void SyncHandlesRendererOnSetupTargetAndIsIdempotent()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { from };
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            var firstChanged = MateriluneSwapSynchronizer.Sync(root);
            var secondChanged = MateriluneSwapSynchronizer.Sync(root);

            Assert.That(FindOverride(root, renderer).gameObject, Is.EqualTo(root.gameObject));
            Assert.That(firstChanged, Is.GreaterThanOrEqualTo(1));
            Assert.That(secondChanged, Is.EqualTo(0));
        }

        [Test]
        public void SyncUsesOverrideForRendererOnSetupTarget()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var rootReplacement = CreateMaterial(shader);
            var overrideReplacement = CreateMaterial(shader);
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { from };
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, rootReplacement));
            root.GetComponent<MateriluneSwapOverride>().Swaps.Add(
                new MateriluneMaterialSwapEntry(from, overrideReplacement));

            MateriluneSwapSynchronizer.Sync(root);

            AssertSwap(root.GetComponent<ModularAvatarMaterialSwap>(), from, overrideReplacement);
        }

        [Test]
        public void SyncCanBeUndone()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var renderer = CreateRenderer("Renderer", target.transform, from);
            var root = GetOnlyPreset(MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep));
            Undo.ClearAll();
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSwapSynchronizer.Sync(root);
            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(FindMaterialSwap(root, renderer).Swaps, Is.Empty);
        }

        [Test]
        public void SyncManagerUpdatesInactivePresets()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var renderer = CreateRenderer("Renderer", target.transform, from);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var inactiveRoot = MateriluneSetupService.AddPreset(manager);
            inactiveRoot.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            var changed = MateriluneSwapSynchronizer.Sync(manager);

            Assert.That(inactiveRoot.gameObject.activeSelf, Is.False);
            Assert.That(changed, Is.GreaterThanOrEqualTo(1));
            AssertSwap(FindMaterialSwap(inactiveRoot, renderer), from, to);
        }

        [Test]
        public void SyncManagerCanBeUndoneInOneStep()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var from = CreateMaterial(shader);
            var firstReplacement = CreateMaterial(shader);
            var secondReplacement = CreateMaterial(shader);
            var renderer = CreateRenderer("Renderer", target.transform, from);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var firstRoot = GetOnlyPreset(manager);
            var secondRoot = MateriluneSetupService.AddPreset(manager);
            Undo.ClearAll();
            firstRoot.Swaps.Add(new MateriluneMaterialSwapEntry(from, firstReplacement));
            secondRoot.Swaps.Add(new MateriluneMaterialSwapEntry(from, secondReplacement));

            MateriluneSwapSynchronizer.Sync(manager);
            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(FindMaterialSwap(firstRoot, renderer).Swaps, Is.Empty);
            Assert.That(FindMaterialSwap(secondRoot, renderer).Swaps, Is.Empty);
        }

        [Test]
        public void SyncThrowsForNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => MateriluneSwapSynchronizer.Sync((MateriluneSwapRoot)null));
            Assert.Throws<ArgumentNullException>(() => MateriluneSwapSynchronizer.Sync((MateriluneSwap)null));
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
