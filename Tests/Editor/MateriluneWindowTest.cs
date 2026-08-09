using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the Materilune editor window's instance and target resolution behavior.
    /// </summary>
    public sealed class MateriluneWindowTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();
        private MateriluneWindow m_window;

        [TearDown]
        public void TearDown()
        {
            if (m_window != null)
            {
                m_window.Close();
                m_window = null;
            }

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
        public void OpenForTestsReusesOneWindowInstance()
        {
            var first = MateriluneWindow.OpenForTests();
            var second = MateriluneWindow.OpenForTests();
            m_window = second;

            Assert.That(first, Is.SameAs(second));
            Assert.That(Resources.FindObjectsOfTypeAll<MateriluneWindow>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void SetTargetResolvesSetupManagerFromTarget()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(target);

            Assert.That(m_window.ResolvedManager, Is.SameAs(manager));
        }

        [Test]
        public void SetTargetResolvesManagerFromRendererChild()
        {
            var target = CreateTarget();
            var child = CreateGameObject("Renderer", target.transform);
            child.AddComponent<MeshRenderer>();
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(child);

            Assert.That(m_window.ResolvedManager, Is.SameAs(manager));
        }

        [Test]
        public void SetTargetResolutionDoesNotDependOnObjectNames()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            foreach (Transform child in target.transform)
            {
                child.gameObject.name = "Renamed";
            }

            target.name = "RenamedTarget";
            manager.gameObject.name = "RenamedManager";
            manager.GetPresets()[0].gameObject.name = "RenamedPreset";
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(target);

            Assert.That(m_window.ResolvedManager, Is.SameAs(manager));
        }

        [Test]
        public void UnsetupTargetClearsAllDataViews()
        {
            var target = CreateTarget();
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(target);

            Assert.That(GetItemCount("lv-preset-list"), Is.EqualTo(0));
            Assert.That(GetItemCount("lv-swap-root-entries"), Is.EqualTo(0));
            Assert.That(GetItemCount("lv-swap-override-entries"), Is.EqualTo(0));
            Assert.That(m_window.rootVisualElement.Q<TreeView>("tv-swap-override-components").GetRootIds(),
                Is.Empty);
            Assert.That(m_window.ResolvedManager, Is.Null);
        }

        /// <summary>
        /// Verifies a rebuild keeps the mesh the user selected, so editing does not clear the
        /// override pane.
        /// </summary>
        [Test]
        public void RebuildKeepsTheSelectedRenderer()
        {
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            var tree = m_window.rootVisualElement.Q<TreeView>("tv-swap-override-components");
            Assert.That(tree, Is.Not.Null);
            tree.SetSelectionById(renderer.transform.GetInstanceID());
            Assert.That(m_window.SelectedRenderer, Is.SameAs(renderer));

            m_window.SetTargetForTests(target);

            Assert.That(m_window.SelectedRenderer, Is.SameAs(renderer));
        }

        /// <summary>
        /// Verifies a rebuild keeps showing the preset the user switched to, even when it is
        /// not the active one.
        /// </summary>
        [Test]
        public void RebuildKeepsTheDisplayedPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            m_window.SetDisplayedPresetForTests(secondPreset);

            m_window.SetTargetForTests(target);

            Assert.That(m_window.DisplayedPreset, Is.SameAs(secondPreset));
        }

        /// <summary>
        /// Verifies selecting another preset switches the displayed preset.
        /// </summary>
        [Test]
        public void ActivatingAnotherPresetSwitchesTheDisplayedPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            Assert.That(m_window.DisplayedPreset, Is.SameAs(manager.GetPresets()[0]));

            var presetList = m_window.rootVisualElement.Q<ListView>("lv-preset-list");
            Assert.That(presetList, Is.Not.Null);
            presetList.SetSelection(1);

            Assert.That(m_window.DisplayedPreset, Is.SameAs(secondPreset));
        }

        /// <summary>
        /// Verifies adding a preset leaves the preset the user is looking at on screen, since
        /// a new preset is created inactive and does not change which preset is active.
        /// </summary>
        [Test]
        public void AddingAPresetKeepsTheDisplayedPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            m_window.SetDisplayedPresetForTests(secondPreset);

            MateriluneSetupService.AddPreset(manager);
            m_window.SetTargetForTests(target);

            Assert.That(m_window.DisplayedPreset, Is.SameAs(secondPreset));
            Assert.That(manager.GetPresets(), Has.Count.EqualTo(3));
        }

        /// <summary>
        /// Verifies activating a preset while every preset is inactive switches the display,
        /// since an all-inactive manager must stay distinguishable from a fallback.
        /// </summary>
        [Test]
        public void ActivatingFromAllInactiveSwitchesTheDisplayedPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var firstPreset = manager.GetPresets()[0];
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            firstPreset.gameObject.SetActive(false);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            m_window.SetDisplayedPresetForTests(secondPreset);

            m_window.rootVisualElement.Q<ListView>("lv-preset-list").SetSelection(0);

            Assert.That(m_window.DisplayedPreset, Is.SameAs(firstPreset));
        }

        /// <summary>
        /// Verifies undoing a preset switch also moves the display back, so the window never
        /// edits a preset that is no longer the active one.
        /// </summary>
        [Test]
        public void UndoingAPresetSwitchMovesTheDisplayBack()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var firstPreset = manager.GetPresets()[0];
            var secondPreset = MateriluneSetupService.AddPreset(manager);

            // Clearing the selection raises selectionChanged, which drops the test target, so
            // the window is opened and pointed at the target only after the isolation is done.
            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            m_window.rootVisualElement.Q<ListView>("lv-preset-list").SetSelection(1);
            Assert.That(m_window.DisplayedPreset, Is.SameAs(secondPreset));

            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(firstPreset.gameObject.activeSelf, Is.True);
            Assert.That(m_window.DisplayedPreset, Is.SameAs(firstPreset));

            MateriluneInspectorIsolation.PerformRedo();

            Assert.That(secondPreset.gameObject.activeSelf, Is.True);
            Assert.That(m_window.DisplayedPreset, Is.SameAs(secondPreset));
        }

        /// <summary>
        /// Verifies destroying the manager clears all bound data while the target object remains.
        /// </summary>
        [Test]
        public void DestroyedManagerClearsAllDataViews()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            Object.DestroyImmediate(manager.gameObject);
            m_window.SetTargetForTests(target);

            Assert.That(GetItemCount("lv-preset-list"), Is.EqualTo(0));
            Assert.That(GetItemCount("lv-swap-root-entries"), Is.EqualTo(0));
            Assert.That(GetItemCount("lv-swap-override-entries"), Is.EqualTo(0));
            Assert.That(m_window.rootVisualElement.Q<TreeView>("tv-swap-override-components").GetRootIds(),
                Is.Empty);
            Assert.That(m_window.ResolvedManager, Is.Null);
        }

        /// <summary>
        /// Verifies destroying the displayed preset falls back to a remaining preset instead of
        /// clearing the whole window.
        /// </summary>
        [Test]
        public void DestroyedPresetFallsBackToRemainingPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var secondPreset = MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            m_window.SetDisplayedPresetForTests(secondPreset);

            Object.DestroyImmediate(secondPreset.gameObject);
            m_window.SetTargetForTests(target);

            Assert.That(GetItemCount("lv-preset-list"), Is.EqualTo(1));
            Assert.That(GetItemCount("lv-swap-root-entries"),
                Is.EqualTo(manager.GetPresets()[0].Swaps.Count));
            Assert.That(m_window.DisplayedPreset, Is.SameAs(manager.GetPresets()[0]));
        }

        [Test]
        public void PresetAddButtonAddsPresetAndListItem()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            var initialCount = manager.GetPresets().Count;

            Assert.That(m_window.rootVisualElement.Q<Button>("btn-preset-add").enabledSelf, Is.True);
            AssertButtonIsClickable("btn-preset-add");
            m_window.AddPresetForTests();

            Assert.That(manager.GetPresets(), Has.Count.EqualTo(initialCount + 1));
            Assert.That(GetItemCount("lv-preset-list"), Is.EqualTo(initialCount + 1));
        }

        /// <summary>
        /// Verifies the preset list shows one row per material assigned to the target meshes.
        /// Entries come from those materials rather than from a manual addition.
        /// </summary>
        [Test]
        public void RootEntryListShowsOneRowPerTargetMaterial()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform);
            var first = CreateMaterial(shader);
            var second = CreateMaterial(shader);
            renderer.sharedMaterials = new[] { first, second };
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var preset = manager.GetPresets()[0];
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            Assert.That(preset.Swaps, Has.Count.EqualTo(2));
            Assert.That(preset.Swaps[0].From, Is.EqualTo(first));
            Assert.That(preset.Swaps[0].To, Is.Null);
            Assert.That(preset.Swaps[1].From, Is.EqualTo(second));
            Assert.That(GetItemCount("lv-swap-root-entries"), Is.EqualTo(2));
        }

        [Test]
        public void PresetAddButtonIsDisabledWhenManagerIsUnresolved()
        {
            var target = CreateTarget();
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            Assert.That(m_window.rootVisualElement.Q<Button>("btn-preset-add").enabledSelf, Is.False);
        }

        [Test]
        public void PresetRemoveButtonIsDisabledWhenOnlyOnePresetExists()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            // The list virtualizes its rows and no layout pass runs in edit mode tests, so the
            // row is built directly instead of being searched for in the visual tree.
            var row = m_window.BuildPresetRowForTests(0);
            Assert.That(row, Is.Not.Null);
            var removeButton = row.Q<Button>("btn-preset-remove");
            Assert.That(removeButton, Is.Not.Null);
            Assert.That(removeButton.enabledSelf, Is.False);
        }

        /// <summary>
        /// Verifies a preset row can be removed once a second preset exists, so the rule only
        /// blocks emptying the manager.
        /// </summary>
        [Test]
        public void PresetRemoveButtonIsEnabledWithSeveralPresets()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            var row = m_window.BuildPresetRowForTests(0);
            Assert.That(row, Is.Not.Null);
            var removeButton = row.Q<Button>("btn-preset-remove");
            Assert.That(removeButton, Is.Not.Null);
            Assert.That(removeButton.enabledSelf, Is.True);
            Assert.That(row.Q<Label>("lbl-preset-name").text,
                Is.EqualTo(manager.GetPresets()[0].gameObject.name));
        }

        [Test]
        public void ClosingWindowUnsubscribesBeforeUndoAndRedo()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneInspectorIsolation.DeselectAll();
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            Undo.FlushUndoRecordObjects();
            m_window.Close();
            m_window = null;

            Assert.DoesNotThrow(() =>
            {
                MateriluneInspectorIsolation.PerformUndo();
                MateriluneInspectorIsolation.PerformRedo();
            });
        }

        private GameObject CreateTarget()
        {
            var target = CreateGameObject("Target", null);
            target.AddComponent<NDMFAvatarRoot>();
            return target;
        }

        private static Shader GetShader()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            return shader;
        }

        private Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
        }

        private MeshRenderer CreateRenderer(string name, Transform parent)
        {
            return CreateGameObject(name, parent).AddComponent<MeshRenderer>();
        }

        private static MateriluneSwapOverride FindOverride(MateriluneSwapRoot preset, Renderer renderer)
        {
            foreach (var operationOverride in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (operationOverride != null && operationOverride.TargetRenderer == renderer)
                {
                    return operationOverride;
                }
            }

            return null;
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        /// <summary>
        /// Checks that a button exists, carries a click manipulator and is usable. Sending a
        /// synthetic click is not available on this Unity version, so tests assert the button
        /// is ready here and then invoke the same handler it is wired to.
        /// </summary>
        /// <param name="buttonName">The name of the button to check.</param>
        private void AssertButtonIsClickable(string buttonName)
        {
            var button = m_window.rootVisualElement.Q<Button>(buttonName);
            Assert.That(button, Is.Not.Null, "Missing button: " + buttonName);
            Assert.That(button.clickable, Is.Not.Null, "Button has no clickable: " + buttonName);
            Assert.That(button.enabledSelf, Is.True, "Button is disabled: " + buttonName);
        }

        private int GetItemCount(string listName)
        {
            var list = m_window.rootVisualElement.Q<ListView>(listName);
            Assert.That(list, Is.Not.Null);
            return list.itemsSource == null ? 0 : list.itemsSource.Count;
        }
    }
}
