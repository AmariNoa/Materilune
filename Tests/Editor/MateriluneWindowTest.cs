using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
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

            m_gameObjects.Clear();
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
        public void UnsetupTargetShowsSetupContainer()
        {
            var target = CreateTarget();
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(target);

            var setupContainer = m_window.rootVisualElement.Q<VisualElement>("setup-container");
            Assert.That(setupContainer, Is.Not.Null);
            Assert.That(setupContainer.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
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

            var tree = m_window.rootVisualElement.Q<MateriluneTargetTreeView>();
            Assert.That(tree, Is.Not.Null);
            tree.SelectRenderer(renderer);
            Assert.That(tree.SelectedRenderer, Is.SameAs(renderer));

            m_window.SetTargetForTests(target);

            Assert.That(m_window.rootVisualElement.Q<MateriluneTargetTreeView>().SelectedRenderer,
                Is.SameAs(renderer));
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
        /// Verifies activating another preset through the preset bar switches the displayed
        /// preset, instead of keeping the previously shown one.
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

            var presetBar = m_window.rootVisualElement.Q<MaterilunePresetBar>();
            Assert.That(presetBar, Is.Not.Null);
            presetBar.ActivatePreset(secondPreset);

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

            m_window.rootVisualElement.Q<MaterilunePresetBar>().AddPresetEntry();

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

            m_window.rootVisualElement.Q<MaterilunePresetBar>().ActivatePreset(firstPreset);

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

            m_window.rootVisualElement.Q<MaterilunePresetBar>().ActivatePreset(secondPreset);
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
        /// Verifies destroying the manager falls back to the setup prompt instead of hiding
        /// everything while the target object is still present.
        /// </summary>
        [Test]
        public void DestroyedManagerShowsSetupContainerAgain()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            Object.DestroyImmediate(manager.gameObject);
            m_window.SetTargetForTests(target);

            var setupContainer = m_window.rootVisualElement.Q<VisualElement>("setup-container");
            Assert.That(setupContainer.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
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

            var contentContainer = m_window.rootVisualElement.Q<VisualElement>("content-container");
            Assert.That(contentContainer.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(m_window.DisplayedPreset, Is.SameAs(manager.GetPresets()[0]));
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

        private MeshRenderer CreateRenderer(string name, Transform parent)
        {
            return CreateGameObject(name, parent).AddComponent<MeshRenderer>();
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
