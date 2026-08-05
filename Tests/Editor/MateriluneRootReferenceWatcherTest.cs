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
    /// Tests repair of material swap references after hierarchy changes.
    /// </summary>
    public sealed class MateriluneRootReferenceWatcherTest
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
        public void RepairBrokenReferencesResolvesRootAfterMovingIntoAvatar()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var target = CreateGameObject("Target", null);
            var rendererObject = CreateGameObject("Renderer", target.transform);
            var renderer = rendererObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CreateMaterial(shader) };

            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var avatar = CreateGameObject("Avatar", null);
            avatar.AddComponent<NDMFAvatarRoot>();
            target.transform.SetParent(avatar.transform, false);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*"));
            var repairedCount = MateriluneRootReferenceWatcher.RepairBrokenReferences();

            // One repair each for the renderer's operation object, the intermediate override
            // and the preset object's material swap.
            Assert.That(repairedCount, Is.EqualTo(3));
            foreach (var operationOverride in manager.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                var materialSwap = operationOverride.GetComponent<ModularAvatarMaterialSwap>();

                // The intermediate override stands for the target itself and has no renderer of
                // its own here, so its material swap points at the target object.
                var expectedRoot = operationOverride.TargetRenderer == null
                    ? target
                    : operationOverride.TargetRenderer.gameObject;
                Assert.That(materialSwap.Root.Get(materialSwap), Is.EqualTo(expectedRoot));
            }

            foreach (var presetRoot in manager.GetPresets())
            {
                var presetMaterialSwap = presetRoot.GetComponent<ModularAvatarMaterialSwap>();
                Assert.That(presetMaterialSwap.Root.Get(presetMaterialSwap), Is.EqualTo(target));
            }
        }

        [Test]
        public void RepairBrokenReferencesDoesNothingForValidReferences()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var target = CreateGameObject("Target", null);
            target.AddComponent<NDMFAvatarRoot>();
            var rendererObject = CreateGameObject("Renderer", target.transform);
            var renderer = rendererObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CreateMaterial(shader) };
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(MateriluneRootReferenceWatcher.RepairBrokenReferences(), Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a setup that sits outside any avatar is left alone, so the repair does not
        /// dirty the scene and log on every hierarchy change without ever resolving.
        /// </summary>
        [Test]
        public void RepairBrokenReferencesDoesNothingOutsideAnAvatar()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var target = CreateGameObject("Target", null);
            var rendererObject = CreateGameObject("Renderer", target.transform);
            var renderer = rendererObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CreateMaterial(shader) };
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);

            Assert.That(MateriluneRootReferenceWatcher.RepairBrokenReferences(), Is.EqualTo(0));
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
        }
    }
}
