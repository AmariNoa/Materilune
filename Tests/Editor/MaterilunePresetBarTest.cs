using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the Materilune preset bar user interface.
    /// </summary>
    public sealed class MaterilunePresetBarTest
    {
        private const string ActiveClass = "materilune-preset-bar__item--active";
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<MaterilunePresetBar> m_bars = new List<MaterilunePresetBar>();

        [TearDown]
        public void TearDown()
        {
            foreach (var bar in m_bars)
            {
                bar.Unbind();
            }

            m_bars.Clear();
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
        public void BindBuildsOneButtonPerPresetAndMarksOnlyActivePreset()
        {
            var manager = CreateManager();
            var activePreset = CreatePreset(manager, "Active", true);
            var inactivePreset = CreatePreset(manager, "Inactive", false);
            var bar = CreateBar();

            bar.Bind(manager);

            var presets = bar.Q<VisualElement>("presets");
            Assert.That(presets, Is.Not.Null);
            Assert.That(presets.childCount, Is.EqualTo(2));
            Assert.That(GetButton(presets, 0).ClassListContains(ActiveClass), Is.True);
            Assert.That(GetButton(presets, 1).ClassListContains(ActiveClass), Is.False);
            Assert.That(GetButton(presets, 0).text, Is.EqualTo(activePreset.gameObject.name));
            Assert.That(GetButton(presets, 1).text, Is.EqualTo(inactivePreset.gameObject.name));
        }

        [Test]
        public void ActivatePresetChangesStateRefreshesButtonsAndRaisesChanged()
        {
            var manager = CreateManager();
            var firstPreset = CreatePreset(manager, "First", true);
            var secondPreset = CreatePreset(manager, "Second", false);
            var bar = CreateBar();
            var changedCount = 0;
            bar.Changed += () => changedCount++;
            bar.Bind(manager);

            bar.ActivatePreset(secondPreset);

            Assert.That(firstPreset.gameObject.activeSelf, Is.False);
            Assert.That(secondPreset.gameObject.activeSelf, Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
            var presets = bar.Q<VisualElement>("presets");
            Assert.That(GetButton(presets, 0).ClassListContains(ActiveClass), Is.False);
            Assert.That(GetButton(presets, 1).ClassListContains(ActiveClass), Is.True);
        }

        [Test]
        public void ActivatePresetCanBeUndoneAsOneOperationAndRefreshesAfterUndo()
        {
            var manager = CreateManager();
            var firstPreset = CreatePreset(manager, "First", true);
            var secondPreset = CreatePreset(manager, "Second", false);
            var bar = CreateBar();
            bar.Bind(manager);
            Undo.ClearAll();

            bar.ActivatePreset(secondPreset);
            Assert.That(firstPreset.gameObject.activeSelf, Is.False);
            Assert.That(secondPreset.gameObject.activeSelf, Is.True);

            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(firstPreset.gameObject.activeSelf, Is.True);
            Assert.That(secondPreset.gameObject.activeSelf, Is.False);
            var presets = bar.Q<VisualElement>("presets");
            Assert.That(GetButton(presets, 0).ClassListContains(ActiveClass), Is.True);
            Assert.That(GetButton(presets, 1).ClassListContains(ActiveClass), Is.False);
        }

        [Test]
        public void AddPresetEntryAddsPresetButtonAndRaisesChanged()
        {
            var manager = CreateManager();
            CreatePreset(manager, "Existing", false);
            var bar = CreateBar();
            var changedCount = 0;
            bar.Changed += () => changedCount++;
            bar.Bind(manager);

            bar.AddPresetEntry();

            Assert.That(manager.GetPresets(), Has.Count.EqualTo(2));
            Assert.That(bar.Q<VisualElement>("presets").childCount, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void OperationsAfterManagerDestructionAreIgnored()
        {
            var manager = CreateManager();
            var preset = CreatePreset(manager, "Preset", true);
            var bar = CreateBar();
            bar.Bind(manager);

            Object.DestroyImmediate(manager.gameObject);

            Assert.DoesNotThrow(() => bar.ActivatePreset(preset));
            Assert.DoesNotThrow(() => bar.AddPresetEntry());
            Assert.DoesNotThrow(() => bar.Refresh());
            Assert.That(bar.Q<VisualElement>("presets").childCount, Is.EqualTo(0));
        }

        [Test]
        public void RefreshFollowsExternallyAddedPreset()
        {
            var manager = CreateManager();
            var bar = CreateBar();
            bar.Bind(manager);
            Assert.That(bar.Q<VisualElement>("presets").childCount, Is.EqualTo(0));

            CreatePreset(manager, "AddedExternally", false);
            bar.Refresh();

            Assert.That(bar.Q<VisualElement>("presets").childCount, Is.EqualTo(1));
        }

        private MateriluneSwap CreateManager()
        {
            // Mirrors the hierarchy setup builds: target > marker > manager. AddPreset resolves
            // the target through it, so a bare manager object is not enough.
            var target = CreateGameObject("Target", null);
            var markerObject = CreateGameObject("Materilune", target.transform);
            markerObject.AddComponent<Materilune>();
            var managerObject = CreateGameObject("Material Swap", markerObject.transform);
            return managerObject.AddComponent<MateriluneSwap>();
        }

        private MaterilunePresetBar CreateBar()
        {
            var bar = new MaterilunePresetBar();
            m_bars.Add(bar);
            return bar;
        }

        private MateriluneSwapRoot CreatePreset(MateriluneSwap manager, string name, bool active)
        {
            var presetObject = CreateGameObject(name, manager.transform);
            presetObject.SetActive(active);
            return presetObject.AddComponent<MateriluneSwapRoot>();
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private static Button GetButton(VisualElement presets, int index)
        {
            return presets[index] as Button;
        }
    }
}
