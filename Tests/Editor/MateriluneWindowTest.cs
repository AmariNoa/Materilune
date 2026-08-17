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

        /// <summary>
        /// Verifies opening on an inner setup edits that setup rather than the one enclosing it.
        /// </summary>
        /// <remarks>
        /// The window reads the hierarchy selection, and a button drawn on a hierarchy row does
        /// not select that row when pressed, so opening without naming the object left the
        /// window on whatever was selected before.
        /// </remarks>
        [Test]
        public void ShowWindowOnNestedSetupResolvesTheInnerManager()
        {
            var outer = CreateTarget();
            CreateRenderer("OuterRenderer", outer.transform);
            var outerManager = MateriluneSetupService.Setup(outer, MateriluneOrphanAction.Keep);
            var inner = CreateGameObject("Inner", outer.transform);
            CreateRenderer("InnerRenderer", inner.transform);
            var innerManager = MateriluneSetupService.Setup(inner, MateriluneOrphanAction.Keep);
            Selection.activeGameObject = outer;
            m_window = MateriluneWindow.OpenForTests();

            MateriluneWindow.ShowWindow(inner);

            Assert.That(m_window.ResolvedManager, Is.SameAs(innerManager));
            Assert.That(m_window.ResolvedManager, Is.Not.SameAs(outerManager));
        }

        /// <summary>
        /// Verifies the window offers to fix the order only while the order is actually wrong.
        /// </summary>
        /// <remarks>
        /// While a nested setup is reached before this one, the settings shown are not the ones
        /// the avatar wears, so the window has to say so and offer a way out.
        /// </remarks>
        [Test]
        public void TheWindowOffersToFixAMarkerThatSitsBehindANestedSetup()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var outfit = CreateGameObject("Outfit", target.transform);
            CreateRenderer("OutfitRenderer", outfit.transform);
            MateriluneSetupService.Setup(outfit, MateriluneOrphanAction.Keep);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            Assert.That(m_window.IsOrderFixOfferedForTests(), Is.False, "the setup should leave the order right");

            manager.transform.parent.SetAsLastSibling();
            m_window.SetTargetForTests(target);

            Assert.That(m_window.IsOrderFixOfferedForTests(), Is.True);
        }

        /// <summary>
        /// Verifies the offer to fix the order goes away once the order has been fixed.
        /// </summary>
        [Test]
        public void FixingTheOrderPutsTheMarkerBackInFront()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var outfit = CreateGameObject("Outfit", target.transform);
            CreateRenderer("OutfitRenderer", outfit.transform);
            MateriluneSetupService.Setup(outfit, MateriluneOrphanAction.Keep);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var marker = manager.transform.parent;
            marker.SetAsLastSibling();
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            m_window.FixOrderForTests();

            Assert.That(marker.GetSiblingIndex(), Is.Zero);
            Assert.That(m_window.IsOrderFixOfferedForTests(), Is.False);
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
        public void SelectingAnotherPresetSwitchesTheDisplayedPreset()
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
        public void SelectingFromAllInactiveSwitchesTheDisplayedPreset()
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
        /// Verifies selecting a row is a display act alone: nothing activates, nothing lands
        /// on the undo stack.
        /// </summary>
        /// <remarks>
        /// Selection used to activate the preset as a side effect, and a test held that
        /// behavior in place. The activation toggle replaced it (spec 4.9): the row picks what
        /// is edited, the toggle picks what the avatar wears, and an inactive preset can be
        /// edited without being switched on.
        /// </remarks>
        [Test]
        public void SelectingAPresetDisplaysItWithoutActivatingIt()
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
            Assert.That(firstPreset.gameObject.activeSelf, Is.True, "selection must not deactivate");
            Assert.That(secondPreset.gameObject.activeSelf, Is.False, "selection must not activate");

            // Nothing was changed, so an undo must find nothing to take back.
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(firstPreset.gameObject.activeSelf, Is.True);
            Assert.That(secondPreset.gameObject.activeSelf, Is.False);
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

        /// <summary>
        /// Verifies the update offer appears once the target gains a material and clears after
        /// updating, so it cannot stay on with no way to satisfy it. The status bar reports a
        /// state in both cases, so the row it occupies is never an empty gap.
        /// </summary>
        [Test]
        public void UpdateIsOfferedForANewMaterialAndClearsAfterUpdating()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform);
            var first = CreateMaterial(shader);
            renderer.sharedMaterials = new[] { first };
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            Assert.That(m_window.IsUpdateOfferedForTests(), Is.False);
            Assert.That(m_window.GetStatusMessageForTests(), Is.Not.Empty);

            var second = CreateMaterial(shader);
            renderer.sharedMaterials = new[] { first, second };
            m_window.SetTargetForTests(target);

            Assert.That(m_window.IsUpdateOfferedForTests(), Is.True);
            var warningMessage = m_window.GetStatusMessageForTests();
            Assert.That(warningMessage, Is.Not.Empty);

            m_window.UpdateEntriesForTests();

            Assert.That(m_window.IsUpdateOfferedForTests(), Is.False);
            Assert.That(m_window.GetStatusMessageForTests(), Is.Not.Empty);
            Assert.That(m_window.GetStatusMessageForTests(), Is.Not.EqualTo(warningMessage));
            var preset = manager.GetPresets()[0];
            Assert.That(preset.Swaps, Has.Count.EqualTo(2));
            Assert.That(preset.Swaps[1].From, Is.EqualTo(second));
        }

        /// <summary>
        /// Verifies clearing a panel sets every replacement back to none without removing the
        /// entries, reaches the Material Swap component, and is taken back by one undo.
        /// </summary>
        [Test]
        public void ClearingReplacementsKeepsTheEntriesAndCanBeUndone()
        {
            var shader = GetShader();
            var target = CreateTarget();
            var renderer = CreateRenderer("Renderer", target.transform);
            var first = CreateMaterial(shader);
            var second = CreateMaterial(shader);
            renderer.sharedMaterials = new[] { first, second };
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var preset = manager.GetPresets()[0];
            var replacement = CreateMaterial(shader);
            preset.Swaps[0] = new MateriluneMaterialSwapEntry(first, replacement);
            preset.Swaps[1] = new MateriluneMaterialSwapEntry(second, replacement);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);
            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();

            Assert.That(m_window.IsRootClearOfferedForTests(), Is.True);

            m_window.ClearRootReplacementsForTests();

            Assert.That(preset.Swaps, Has.Count.EqualTo(2));
            Assert.That(preset.Swaps[0].From, Is.EqualTo(first));
            Assert.That(preset.Swaps[0].To, Is.Null);
            Assert.That(preset.Swaps[1].To, Is.Null);
            Assert.That(preset.GetComponent<ModularAvatarMaterialSwap>().Swaps, Is.Empty);
            Assert.That(m_window.IsRootClearOfferedForTests(), Is.False);

            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(preset.Swaps[0].To, Is.EqualTo(replacement));
            Assert.That(preset.Swaps[1].To, Is.EqualTo(replacement));
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

        /// <summary>
        /// Verifies the row's toggle reflects the preset's active state.
        /// </summary>
        [Test]
        public void PresetRowToggleShowsTheActiveState()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            var activeRow = m_window.BuildPresetRowForTests(0);
            var inactiveRow = m_window.BuildPresetRowForTests(1);

            Assert.That(activeRow.Q<RadioButton>("tgl-preset-active").value, Is.True);
            Assert.That(inactiveRow.Q<RadioButton>("tgl-preset-active").value, Is.False);
        }

        /// <summary>
        /// Verifies activating a preset from the window leaves exactly that one active.
        /// </summary>
        /// <remarks>
        /// The window itself only switches one on; the single-active watcher is what puts the
        /// other out, and the pair must land inside one undo step.
        /// </remarks>
        [Test]
        public void ActivatingAPresetLeavesOnlyThatPresetActive()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            var presets = manager.GetPresets();
            Assert.That(presets[0].gameObject.activeSelf, Is.True, "the first preset should start active");
            Assert.That(presets[1].gameObject.activeSelf, Is.False, "the added preset should start inactive");
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            m_window.ActivatePresetForTests(presets[1]);

            Assert.That(presets[1].gameObject.activeSelf, Is.True);
            Assert.That(presets[0].gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// Verifies one undo restores the previous active arrangement whole.
        /// </summary>
        [Test]
        public void UndoingAnActivationRestoresThePreviousArrangement()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            var presets = manager.GetPresets();
            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            m_window.ActivatePresetForTests(presets[1]);
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(presets[0].gameObject.activeSelf, Is.True);
            Assert.That(presets[1].gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// Verifies the rename field sits in the row, hidden without leaving the layout.
        /// </summary>
        /// <remarks>
        /// The field lies over the label, so starting an edit must not move the row; hiding it
        /// with visibility rather than display is what this pins down.
        /// </remarks>
        [Test]
        public void PresetRowKeepsTheRenameFieldHiddenInPlace()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            var row = m_window.BuildPresetRowForTests(0);
            var nameField = row.Q<TextField>("txt-preset-name");

            Assert.That(nameField, Is.Not.Null);
            Assert.That(nameField.visible, Is.False);
            Assert.That(nameField.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        /// <summary>
        /// Verifies a pooled row renames only the preset it currently shows.
        /// </summary>
        /// <remarks>
        /// The list reuses its rows. A rename handler left over from an earlier binding kept
        /// its captured preset and fired alongside the new one, so renaming the row's current
        /// preset also renamed whichever preset the row used to show.
        /// </remarks>
        [Test]
        public void RebindingAPresetRowDropsTheOldRenameTarget()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            var presets = manager.GetPresets();
            var firstName = presets[0].gameObject.name;
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            // The same row is bound to one preset and then reused for another, which is what
            // the ListView's pooling does on scroll. The field only dispatches its change
            // event while attached to a panel, so the row is hosted in the window.
            var row = m_window.BuildPresetRowForTests(0);
            m_window.rootVisualElement.Add(row);
            m_window.RebindPresetRowForTests(row, 1);

            row.Q<TextField>("txt-preset-name").value = "Renamed";

            Assert.That(presets[1].gameObject.name, Is.EqualTo("Renamed"));
            Assert.That(presets[0].gameObject.name, Is.EqualTo(firstName));
        }

        /// <summary>
        /// Verifies removing the shown preset never leaves the window with nothing selected.
        /// </summary>
        /// <remarks>
        /// The fallback used to be the active preset alone, and with every remaining preset
        /// inactive there was nothing to fall back to: no selected row, empty panels, after a
        /// removal or an import. The first preset stands in now.
        /// </remarks>
        [Test]
        public void RemovingTheShownPresetFallsBackToAnotherPreset()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var second = MateriluneSetupService.AddPreset(manager);
            var third = MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            // The shown preset goes away while everything left is inactive: the first preset
            // is deactivated by hand and the second, which was being shown, is destroyed.
            manager.GetPresets()[0].gameObject.SetActive(false);
            m_window.SetDisplayedPresetForTests(second);
            Object.DestroyImmediate(second.gameObject);
            m_window.SetTargetForTests(target);

            Assert.That(m_window.DisplayedPreset, Is.Not.Null);
            Assert.That(m_window.DisplayedPreset == third || m_window.DisplayedPreset == manager.GetPresets()[0], Is.True);
        }

        /// <summary>
        /// Verifies branches without a renderer anywhere below them stay out of the tree.
        /// </summary>
        /// <remarks>
        /// The tree exists to pick a mesh; armature bones and anchor points cannot end in one
        /// and only bury the rows that can.
        /// </remarks>
        [Test]
        public void TheTargetTreeHidesBranchesWithoutRenderers()
        {
            var target = CreateTarget();
            CreateRenderer("Body", target.transform);
            var armature = CreateGameObject("Armature", target.transform);
            CreateGameObject("Hips", armature.transform);
            var clothed = CreateGameObject("Outfit", target.transform);
            CreateRenderer("OutfitMesh", clothed.transform);
            MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            m_window = MateriluneWindow.OpenForTests();

            m_window.SetTargetForTests(target);

            var tree = m_window.rootVisualElement.Q<TreeView>("tv-swap-override-components");
            var rootIds = tree.GetRootIds();
            var rootId = -1;
            foreach (var id in rootIds)
            {
                rootId = id;
                break;
            }

            var childIds = new List<int>(tree.viewController.GetChildrenIds(rootId));
            Assert.That(childIds, Has.Count.EqualTo(2), "only the branches that reach a renderer remain");
            Assert.That(childIds, Has.No.Member(armature.transform.GetInstanceID()));
        }

        /// <summary>
        /// Verifies the list snaps back to the displayed preset when its selection is dropped.
        /// </summary>
        /// <remarks>
        /// A click on the empty area under the rows, a ctrl-click on the selected row or
        /// Escape clears a ListView's selection without choosing anything else. The window
        /// keeps showing the same preset through all of those, so the list has to come back
        /// to it rather than sit unselected.
        /// </remarks>
        [Test]
        public void PresetListReselectsTheDisplayedPresetWhenSelectionIsCleared()
        {
            var target = CreateTarget();
            CreateRenderer("Renderer", target.transform);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            MateriluneSetupService.AddPreset(manager);
            m_window = MateriluneWindow.OpenForTests();
            m_window.SetTargetForTests(target);

            var presetList = m_window.rootVisualElement.Q<ListView>("lv-preset-list");
            Assert.That(presetList.selectedIndex, Is.EqualTo(0), "the displayed preset should start selected");

            presetList.ClearSelection();

            Assert.That(presetList.selectedIndex, Is.EqualTo(0));
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
