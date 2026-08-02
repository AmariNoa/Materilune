using System;
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
    /// Tests the material replacement list user interface.
    /// </summary>
    public sealed class MateriluneSwapListViewTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private Shader m_shader;
        private string m_testDirectory;

        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MateriluneSwapListViewTest_" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.IsValidFolder("Assets/" + folderName), Is.False);
            AssetDatabase.CreateFolder("Assets", folderName);
            m_testDirectory = "Assets/" + folderName;
        }

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
            if (!string.IsNullOrEmpty(m_testDirectory) && AssetDatabase.IsValidFolder(m_testDirectory))
            {
                AssetDatabase.DeleteAsset(m_testDirectory);
            }

            m_testDirectory = null;
        }

        [Test]
        public void BindBuildsOneRowPerArrayElement()
        {
            var firstFrom = CreateMaterial("FirstFrom.mat");
            var firstTo = CreateMaterial("FirstTo.mat");
            var secondFrom = CreateMaterial("SecondFrom.mat");
            var secondTo = CreateMaterial("SecondTo.mat");
            var component = CreateOverride(
                new MateriluneMaterialSwapEntry(firstFrom, firstTo),
                new MateriluneMaterialSwapEntry(secondFrom, secondTo));
            var view = new MateriluneSwapListView();

            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);

            var entries = view.Q<VisualElement>("entries");
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.childCount, Is.EqualTo(2));
            foreach (var row in entries.Children())
            {
                Assert.That(row.Q<MateriluneSwapEntryView>(), Is.Not.Null);
            }
        }

        [Test]
        public void AddEntryAddsEmptyElementAndRaisesChanged()
        {
            var component = CreateOverride();
            var view = new MateriluneSwapListView();
            var changedCount = 0;
            view.Changed += () => changedCount++;
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);

            view.AddEntry();

            var property = GetSwapsProperty(component);
            property.serializedObject.Update();
            var entry = property.GetArrayElementAtIndex(0);
            Assert.That(property.arraySize, Is.EqualTo(1));
            Assert.That(entry.FindPropertyRelative("m_from").objectReferenceValue, Is.Null);
            Assert.That(entry.FindPropertyRelative("m_to").objectReferenceValue, Is.Null);
            Assert.That(view.Q<VisualElement>("entries").childCount, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void RemoveEntryAtRemovesEntryAndRaisesChanged()
        {
            var firstFrom = CreateMaterial("FirstFrom.mat");
            var firstTo = CreateMaterial("FirstTo.mat");
            var remainingFrom = CreateMaterial("RemainingFrom.mat");
            var remainingTo = CreateMaterial("RemainingTo.mat");
            var component = CreateOverride(
                new MateriluneMaterialSwapEntry(firstFrom, firstTo),
                new MateriluneMaterialSwapEntry(remainingFrom, remainingTo));
            var view = new MateriluneSwapListView();
            var changedCount = 0;
            view.Changed += () => changedCount++;
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);

            view.RemoveEntryAt(0);

            var property = GetSwapsProperty(component);
            property.serializedObject.Update();
            var entry = property.GetArrayElementAtIndex(0);
            Assert.That(property.arraySize, Is.EqualTo(1));
            Assert.That(entry.FindPropertyRelative("m_from").objectReferenceValue, Is.EqualTo(remainingFrom));
            Assert.That(entry.FindPropertyRelative("m_to").objectReferenceValue, Is.EqualTo(remainingTo));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void AddDroppedMaterialsFiltersNonMaterialsAndDuplicateFromValues()
        {
            var existing = CreateMaterial("Existing.mat");
            var droppedFirst = CreateMaterial("DroppedFirst.mat");
            var droppedSecond = CreateMaterial("DroppedSecond.mat");
            var component = CreateOverride(new MateriluneMaterialSwapEntry(existing, existing));
            var unrelatedObject = new GameObject("Unrelated Drag Object");
            m_gameObjects.Add(unrelatedObject);
            var view = new MateriluneSwapListView();
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);

            var added = view.AddDroppedMaterials(new UnityEngine.Object[]
            {
                droppedFirst,
                unrelatedObject,
                existing,
                droppedFirst,
                droppedSecond,
            });

            var property = GetSwapsProperty(component);
            property.serializedObject.Update();
            Assert.That(added, Is.EqualTo(2));
            Assert.That(property.arraySize, Is.EqualTo(3));
            Assert.That(GetMaterial(property.GetArrayElementAtIndex(1), "m_from"), Is.EqualTo(droppedFirst));
            Assert.That(GetMaterial(property.GetArrayElementAtIndex(1), "m_to"), Is.EqualTo(droppedFirst));
            Assert.That(GetMaterial(property.GetArrayElementAtIndex(2), "m_from"), Is.EqualTo(droppedSecond));
            Assert.That(GetMaterial(property.GetArrayElementAtIndex(2), "m_to"), Is.EqualTo(droppedSecond));
        }

        [Test]
        public void AddDroppedMaterialsCanBeUndone()
        {
            var material = CreateMaterial("Dropped.mat");
            var component = CreateOverride();
            var view = new MateriluneSwapListView();
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);
            Undo.ClearAll();

            Assert.That(view.AddDroppedMaterials(new UnityEngine.Object[] { material }), Is.EqualTo(1));

            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            var property = GetSwapsProperty(component);
            property.serializedObject.Update();
            Assert.That(property.arraySize, Is.EqualTo(0));
        }

        [Test]
        public void UndoAfterAddRebuildsRows()
        {
            var material = CreateMaterial("UndoRebuild.mat");
            var component = CreateOverride();
            var view = new MateriluneSwapListView();
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);
            Undo.ClearAll();

            Assert.That(view.AddDroppedMaterials(new UnityEngine.Object[] { material }), Is.EqualTo(1));
            Assert.That(view.Q<VisualElement>("entries").childCount, Is.EqualTo(1));

            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(view.Q<VisualElement>("entries").childCount, Is.EqualTo(0));

            Undo.PerformRedo();

            Assert.That(view.Q<VisualElement>("entries").childCount, Is.EqualTo(1));
            view.Unbind();
        }

        [Test]
        public void OperationsAfterTargetDestructionAreIgnored()
        {
            var material = CreateMaterial("DestroyedTargetMaterial.mat");
            var component = CreateOverride(new MateriluneMaterialSwapEntry(material, material));
            var view = new MateriluneSwapListView();
            view.Bind(GetSwapsProperty(component), null, MateriluneCandidateMode.None);
            var rowCount = view.Q<VisualElement>("entries").childCount;

            Object.DestroyImmediate(component.gameObject);

            Assert.That(() => view.AddEntry(), Throws.Nothing);
            Assert.That(() => view.RemoveEntryAt(0), Throws.Nothing);
            Assert.That(() => view.AddDroppedMaterials(new UnityEngine.Object[] { material }), Throws.Nothing);
            Assert.That(view.Q<VisualElement>("entries").childCount, Is.EqualTo(rowCount));
        }

        private MateriluneSwapOverride CreateOverride(params MateriluneMaterialSwapEntry[] entries)
        {
            var gameObject = new GameObject("Materilune Swap List Test");
            m_gameObjects.Add(gameObject);
            var component = gameObject.AddComponent<MateriluneSwapOverride>();
            var serializedObject = new SerializedObject(component);
            var swaps = serializedObject.FindProperty("m_swaps");
            swaps.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = swaps.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("m_from").objectReferenceValue = entries[index].From;
                entry.FindPropertyRelative("m_to").objectReferenceValue = entries[index].To;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private Material CreateMaterial(string fileName)
        {
            var material = new Material(m_shader);
            AssetDatabase.CreateAsset(material, m_testDirectory + "/" + fileName);
            return material;
        }

        private static SerializedProperty GetSwapsProperty(MateriluneSwapOverride component)
        {
            return new SerializedObject(component).FindProperty("m_swaps");
        }

        private static Material GetMaterial(SerializedProperty entry, string propertyName)
        {
            return entry.FindPropertyRelative(propertyName).objectReferenceValue as Material;
        }
    }
}
