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
    /// Tests preset manager behavior.
    /// </summary>
    public sealed class MateriluneSwapTest
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
        public void SetupCreatesManagerAndActiveDefaultPreset()
        {
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform, CreateMaterial());

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var preset = GetOnlyPreset(manager);
            var marker = manager.transform.parent;

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.GetComponent<Materilune>(), Is.Not.Null);
            Assert.That(marker.parent, Is.EqualTo(target.transform));
            Assert.That(manager.transform.parent, Is.EqualTo(marker));
            Assert.That(preset.transform.parent, Is.EqualTo(manager.transform));
            Assert.That(preset.gameObject.activeSelf, Is.True);
            Assert.That(FindOverride(preset, renderer), Is.Not.Null);
            Assert.That(preset.GetComponent<ModularAvatarMaterialSwap>(), Is.Not.Null);
            Assert.That(FindIntermediate(preset), Is.Not.Null);
        }

        [Test]
        public void GetPresetsIncludesInactiveChildrenInSiblingOrderWithoutUsingNames()
        {
            var target = CreateTarget();
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var first = GetOnlyPreset(manager);
            var second = MateriluneSetupService.AddPreset(manager);
            first.gameObject.name = "Renamed First";
            second.gameObject.name = "Renamed Second";

            CollectionAssert.AreEqual(new[] { first, second }, manager.GetPresets());
        }

        [Test]
        public void AddPresetCreatesInactiveMirroredHierarchyAndCanBeUndone()
        {
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform, CreateMaterial());
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            Undo.ClearAll();

            var added = MateriluneSetupService.AddPreset(manager);
            Undo.FlushUndoRecordObjects();

            Assert.That(added.gameObject.activeSelf, Is.False);
            Assert.That(added.SetupTarget, Is.EqualTo(target));
            Assert.That(added.Swaps, Is.Empty);
            Assert.That(FindOverride(added, renderer), Is.Not.Null);
            Assert.That(added.GetComponent<ModularAvatarMaterialSwap>(), Is.Not.Null);
            Assert.That(FindIntermediate(added), Is.Not.Null);
            Assert.That(added.TargetOverride, Is.EqualTo(FindIntermediate(added)));
            Assert.That(
                added.GetComponent<ModularAvatarMaterialSwap>().Root.Get(added.GetComponent<ModularAvatarMaterialSwap>()),
                Is.EqualTo(target));

            MateriluneInspectorIsolation.DeselectAll();
            MateriluneInspectorIsolation.PerformUndo();
            Assert.That(manager.GetPresets(), Has.Count.EqualTo(1));
            MateriluneInspectorIsolation.PerformRedo();
            Assert.That(manager.GetPresets(), Has.Count.EqualTo(2));

            var restoredPreset = manager.GetPresets()[1];
            Assert.That(restoredPreset.TargetOverride, Is.EqualTo(FindIntermediate(restoredPreset)));
        }

        [Test]
        public void AddPresetThrowsForNullManager()
        {
            Assert.Throws<ArgumentNullException>(() => MateriluneSetupService.AddPreset(null));
        }

        [Test]
        public void SetupUpdatesEveryPreset()
        {
            var target = CreateTarget();
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            var renderer = CreateRenderer("Renderer", target.transform, CreateMaterial());

            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            foreach (var preset in manager.GetPresets())
            {
                Assert.That(FindOverride(preset, renderer), Is.Not.Null);
            }

            Assert.That(secondPreset, Is.Not.Null);
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

        private MeshRenderer CreateRenderer(string name, Transform parent, Material material)
        {
            var renderer = CreateGameObject(name, parent).AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
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
    }
}
