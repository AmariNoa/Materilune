using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests single active preset enforcement.
    /// </summary>
    public sealed class MaterilunePresetActivationWatcherTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();

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
            Undo.ClearAll();
            MateriluneInspectorIsolation.RestoreSelection();
        }

        [Test]
        public void EnforceSingleActivePresetDeactivatesAllButOne()
        {
            var manager = CreateManager();
            var first = CreatePreset(manager, true);
            var second = CreatePreset(manager, true);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*"));
            var deactivatedCount = MaterilunePresetActivationWatcher.EnforceSingleActivePreset();

            Assert.That(deactivatedCount, Is.EqualTo(1));
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(second.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void EnforceSingleActivePresetAllowsEveryPresetToBeInactive()
        {
            var manager = CreateManager();
            var first = CreatePreset(manager, false);
            var second = CreatePreset(manager, false);

            var deactivatedCount = MaterilunePresetActivationWatcher.EnforceSingleActivePreset();

            Assert.That(deactivatedCount, Is.EqualTo(0));
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(second.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void EnforceSingleActivePresetKeepsNewlyActivatedPreset()
        {
            var manager = CreateManager();
            var first = CreatePreset(manager, true);
            var second = CreatePreset(manager, false);
            MaterilunePresetActivationWatcher.EnforceSingleActivePreset();
            second.gameObject.SetActive(true);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*"));
            var deactivatedCount = MaterilunePresetActivationWatcher.EnforceSingleActivePreset();

            Assert.That(deactivatedCount, Is.EqualTo(1));
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(second.gameObject.activeSelf, Is.True);
        }

        private MateriluneSwap CreateManager()
        {
            var managerObject = CreateGameObject("Manager", null);
            managerObject.AddComponent<NDMFAvatarRoot>();
            return managerObject.AddComponent<MateriluneSwap>();
        }

        private MateriluneSwapRoot CreatePreset(MateriluneSwap manager, bool active)
        {
            var presetObject = CreateGameObject("Preset", manager.transform);
            var preset = presetObject.AddComponent<MateriluneSwapRoot>();
            presetObject.SetActive(active);
            return preset;
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
