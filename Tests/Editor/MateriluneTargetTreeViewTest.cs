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
    /// Tests the Materilune target hierarchy tree user interface.
    /// </summary>
    public sealed class MateriluneTargetTreeViewTest
    {
        private const string RowClass = "materilune-target-tree__row";
        private const string SelectableRowClass = "materilune-target-tree__row--selectable";
        private const string SelectedRowClass = "materilune-target-tree__row--selected";
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<MateriluneTargetTreeView> m_views = new List<MateriluneTargetTreeView>();

        [TearDown]
        public void TearDown()
        {
            foreach (var view in m_views)
            {
                view.Unbind();
            }

            m_views.Clear();
            for (var index = m_gameObjects.Count - 1; index >= 0; index--)
            {
                if (m_gameObjects[index] != null)
                {
                    Object.DestroyImmediate(m_gameObjects[index]);
                }
            }

            m_gameObjects.Clear();
            Undo.ClearAll();
        }

        [Test]
        public void BindBuildsHierarchyAndMakesOnlyRendererRowsSelectable()
        {
            var target = CreateGameObject("Target", null);
            var meshObject = CreateGameObject("Mesh", target.transform);
            var skinnedObject = CreateGameObject("Skinned", target.transform);
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            var skinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            var view = CreateView();

            view.Bind(target);

            var rows = GetRows(view);
            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(FindRow(rows, "Target").ClassListContains(SelectableRowClass), Is.False);
            Assert.That(FindRow(rows, meshObject.name).ClassListContains(SelectableRowClass), Is.True);
            Assert.That(FindRow(rows, skinnedObject.name).ClassListContains(SelectableRowClass), Is.True);
            Assert.That(meshRenderer, Is.Not.Null);
            Assert.That(skinnedRenderer, Is.Not.Null);
        }

        [Test]
        public void SelectRendererRaisesEventOnceAndMarksOnlySelectedRow()
        {
            var target = CreateGameObject("Target", null);
            var meshObject = CreateGameObject("Mesh", target.transform);
            var otherObject = CreateGameObject("Other", target.transform);
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            var otherRenderer = otherObject.AddComponent<SkinnedMeshRenderer>();
            var view = CreateView();
            var selectedCount = 0;
            Renderer selectedRenderer = null;
            view.RendererSelected += renderer =>
            {
                selectedCount++;
                selectedRenderer = renderer;
            };
            view.Bind(target);

            view.SelectRenderer(meshRenderer);
            view.SelectRenderer(meshRenderer);

            Assert.That(selectedCount, Is.EqualTo(1));
            Assert.That(selectedRenderer, Is.SameAs(meshRenderer));
            Assert.That(view.SelectedRenderer, Is.SameAs(meshRenderer));
            Assert.That(FindRow(GetRows(view), meshObject.name).ClassListContains(SelectedRowClass), Is.True);
            Assert.That(FindRow(GetRows(view), otherObject.name).ClassListContains(SelectedRowClass), Is.False);
            Assert.That(otherRenderer, Is.Not.Null);
        }

        [Test]
        public void MateriluneSubtreeIsNotDisplayed()
        {
            var target = CreateGameObject("Target", null);
            var excludedObject = CreateGameObject("Excluded", target.transform);
            excludedObject.AddComponent<MateriluneSwap>();
            var hiddenMeshObject = CreateGameObject("HiddenMesh", excludedObject.transform);
            hiddenMeshObject.AddComponent<MeshRenderer>();
            var visibleObject = CreateGameObject("Visible", target.transform);
            visibleObject.AddComponent<MeshRenderer>();
            var view = CreateView();

            view.Bind(target);

            var rows = GetRows(view);
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(FindRowOrNull(rows, excludedObject.name), Is.Null);
            Assert.That(FindRowOrNull(rows, hiddenMeshObject.name), Is.Null);
            Assert.That(FindRowOrNull(rows, visibleObject.name), Is.Not.Null);
        }

        [Test]
        public void RefreshIncludesExternalChildAndPreservesSelectionByReference()
        {
            var target = CreateGameObject("Target", null);
            var meshObject = CreateGameObject("Mesh", target.transform);
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            var view = CreateView();
            view.Bind(target);
            view.SelectRenderer(meshRenderer);

            var addedObject = CreateGameObject("Added", target.transform);
            addedObject.AddComponent<SkinnedMeshRenderer>();
            view.Refresh();

            Assert.That(GetRows(view), Has.Count.EqualTo(3));
            Assert.That(view.SelectedRenderer, Is.SameAs(meshRenderer));
            Assert.That(FindRow(GetRows(view), meshObject.name).ClassListContains(SelectedRowClass), Is.True);
        }

        [Test]
        public void RefreshClearsSelectionWhenSelectedRendererIsDestroyed()
        {
            var target = CreateGameObject("Target", null);
            var meshObject = CreateGameObject("Mesh", target.transform);
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            var view = CreateView();
            view.Bind(target);
            view.SelectRenderer(meshRenderer);

            Object.DestroyImmediate(meshObject);

            Assert.DoesNotThrow(() => view.Refresh());
            Assert.That(view.SelectedRenderer, Is.Null);
        }

        [Test]
        public void OperationsAfterTargetDestructionAreIgnored()
        {
            var target = CreateGameObject("Target", null);
            var meshObject = CreateGameObject("Mesh", target.transform);
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            var view = CreateView();
            view.Bind(target);

            Object.DestroyImmediate(target);

            Assert.DoesNotThrow(() => view.Refresh());
            Assert.DoesNotThrow(() => view.SelectRenderer(meshRenderer));
            Assert.That(GetRows(view), Has.Count.EqualTo(0));
            Assert.That(view.SelectedRenderer, Is.Null);
        }

        private MateriluneTargetTreeView CreateView()
        {
            var view = new MateriluneTargetTreeView();
            m_views.Add(view);
            return view;
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private static List<VisualElement> GetRows(MateriluneTargetTreeView view)
        {
            var tree = view.Q<ScrollView>("tree");
            Assert.That(tree, Is.Not.Null);
            var rows = new List<VisualElement>();
            foreach (var child in tree.contentContainer.Children())
            {
                if (child.ClassListContains(RowClass))
                {
                    rows.Add(child);
                }
            }

            return rows;
        }

        private static VisualElement FindRow(IEnumerable<VisualElement> rows, string name)
        {
            var row = FindRowOrNull(rows, name);
            Assert.That(row, Is.Not.Null, "Could not find row for " + name + ".");
            return row;
        }

        private static VisualElement FindRowOrNull(IEnumerable<VisualElement> rows, string name)
        {
            foreach (var row in rows)
            {
                var label = row.Q<Label>();
                if (label != null && label.text == name)
                {
                    return row;
                }
            }

            return null;
        }
    }
}
