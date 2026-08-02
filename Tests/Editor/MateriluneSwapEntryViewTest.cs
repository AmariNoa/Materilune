using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the material replacement entry user interface.
    /// </summary>
    public sealed class MateriluneSwapEntryViewTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private Shader m_shader;
        private string m_testDirectory;

        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MateriluneSwapEntryViewTest_" + Guid.NewGuid().ToString("N");
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
            if (!string.IsNullOrEmpty(m_testDirectory) && AssetDatabase.IsValidFolder(m_testDirectory))
            {
                AssetDatabase.DeleteAsset(m_testDirectory);
            }

            m_testDirectory = null;
        }

        [Test]
        public void ConstructorCreatesNamedUxmlElements()
        {
            var view = new MateriluneSwapEntryView();

            Assert.That(view.Q<ObjectField>("from-field"), Is.Not.Null);
            Assert.That(view.Q<Button>("from-picker"), Is.Not.Null);
            Assert.That(view.Q<ObjectField>("to-field"), Is.Not.Null);
            Assert.That(view.Q<Button>("to-previous"), Is.Not.Null);
            Assert.That(view.Q<Button>("to-next"), Is.Not.Null);
        }

        [Test]
        public void BindReflectsCurrentPropertyValues()
        {
            var from = CreateMaterial("From.mat");
            var to = CreateMaterial("To.mat");
            var property = CreateSwapEntryProperty(from, to);
            var view = new MateriluneSwapEntryView();

            view.Bind(property, null, MateriluneCandidateMode.None);

            Assert.That(view.Q<ObjectField>("from-field").value, Is.EqualTo(from));
            Assert.That(view.Q<ObjectField>("to-field").value, Is.EqualTo(to));
        }

        [Test]
        public void ApplyFromCandidateSetsEmptyToAndRaisesChanged()
        {
            var from = CreateMaterial("From.mat");
            var property = CreateSwapEntryProperty(null, null);
            var view = new MateriluneSwapEntryView();
            var changedCount = 0;
            view.Changed += () => changedCount++;
            view.Bind(property, new[] { from }, MateriluneCandidateMode.None);

            view.ApplyFromCandidate(from);

            Assert.That(GetMaterial(property, "m_from"), Is.EqualTo(from));
            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(from));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyFromCandidatePreservesExistingTo()
        {
            var from = CreateMaterial("From.mat");
            var existingTo = CreateMaterial("ExistingTo.mat");
            var property = CreateSwapEntryProperty(null, existingTo);
            var view = new MateriluneSwapEntryView();
            view.Bind(property, new[] { from }, MateriluneCandidateMode.None);

            view.ApplyFromCandidate(from);

            Assert.That(GetMaterial(property, "m_from"), Is.EqualTo(from));
            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(existingTo));
        }

        [Test]
        public void StepToCandidateMovesForwardAndWrapsInSameDirectory()
        {
            var first = CreateMaterial("A_First.mat");
            var current = CreateMaterial("B_Current.mat");
            var last = CreateMaterial("C_Last.mat");
            var property = CreateSwapEntryProperty(current, current);
            var view = new MateriluneSwapEntryView();
            view.Bind(property, null, MateriluneCandidateMode.SameDirectory);

            view.StepToCandidate(1);
            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(last));

            view.StepToCandidate(1);
            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(first));
        }

        [Test]
        public void BindWithNoneModeDisablesCandidateButtons()
        {
            var material = CreateMaterial("Material.mat");
            var property = CreateSwapEntryProperty(material, material);
            var view = new MateriluneSwapEntryView();

            view.Bind(property, null, MateriluneCandidateMode.None);

            Assert.That(view.Q<Button>("to-previous").enabledSelf, Is.False);
            Assert.That(view.Q<Button>("to-next").enabledSelf, Is.False);
        }

        /// <summary>
        /// Verifies operations after the bound component was destroyed neither throw nor apply.
        /// </summary>
        [Test]
        public void OperationsAfterTargetDestructionAreIgnored()
        {
            var from = CreateMaterial("Destroyed_From.mat");
            var entry = CreateSwapEntryProperty(from, null);
            var view = new MateriluneSwapEntryView();
            view.Bind(entry, new[] { from }, MateriluneCandidateMode.SameDirectory);

            Object.DestroyImmediate(entry.serializedObject.targetObject);

            Assert.That(() => view.ApplyFromCandidate(from), Throws.Nothing);
            Assert.That(() => view.StepToCandidate(1), Throws.Nothing);
            Assert.That(() => view.Unbind(), Throws.Nothing);
        }

        private SerializedProperty CreateSwapEntryProperty(Material from, Material to)
        {
            var gameObject = new GameObject("Swap Entry Test");
            m_gameObjects.Add(gameObject);
            var component = gameObject.AddComponent<MateriluneSwapOverride>();
            var serializedObject = new SerializedObject(component);
            var swaps = serializedObject.FindProperty("m_swaps");
            swaps.arraySize = 1;
            var entry = swaps.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("m_from").objectReferenceValue = from;
            entry.FindPropertyRelative("m_to").objectReferenceValue = to;
            serializedObject.ApplyModifiedProperties();
            return new SerializedObject(component).FindProperty("m_swaps").GetArrayElementAtIndex(0);
        }

        private Material CreateMaterial(string fileName)
        {
            var material = new Material(m_shader);
            AssetDatabase.CreateAsset(material, m_testDirectory + "/" + fileName);
            return material;
        }

        private static Material GetMaterial(SerializedProperty entry, string propertyName)
        {
            entry.serializedObject.Update();
            return entry.FindPropertyRelative(propertyName).objectReferenceValue as Material;
        }
    }
}
