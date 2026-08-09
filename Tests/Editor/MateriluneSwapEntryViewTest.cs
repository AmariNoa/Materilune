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
        private EditorWindow m_window;

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
            if (m_window != null)
            {
                m_window.Close();
                Object.DestroyImmediate(m_window);
                m_window = null;
            }

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

        // BaseField dispatches ChangeEvent only while attached to a panel, so tests that drive a
        // field through its value setter host the view in a throwaway window.
        private void AttachToPanel(MateriluneSwapEntryView view)
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.ShowUtility();
            m_window.rootVisualElement.Add(view);
        }

        [Test]
        public void EditingFieldWritesValueToPropertyAndRaisesChangedOnce()
        {
            var from = CreateMaterial("From.mat");
            var replacement = CreateMaterial("Replacement.mat");
            var property = CreateSwapEntryProperty(from, null);
            var view = new MateriluneSwapEntryView();
            AttachToPanel(view);
            view.Bind(property, null, MateriluneCandidateMode.None);
            var changedCount = 0;
            view.Changed += () => changedCount++;

            view.Q<ObjectField>("to-field").value = replacement;

            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(replacement));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies rebinding reports no change. A rebind that looked like an edit made the host
        /// rebuild from inside its own rebuild, which overflowed the stack.
        /// </summary>
        [Test]
        public void RebindingTheSameEntryRaisesNoChange()
        {
            var from = CreateMaterial("From.mat");
            var to = CreateMaterial("To.mat");
            var property = CreateSwapEntryProperty(from, to);
            var view = new MateriluneSwapEntryView();
            AttachToPanel(view);
            view.Bind(property, null, MateriluneCandidateMode.None);
            var changedCount = 0;
            view.Changed += () => changedCount++;

            view.Bind(property, null, MateriluneCandidateMode.None);

            Assert.That(changedCount, Is.EqualTo(0));
            Assert.That(view.Q<ObjectField>("from-field").value, Is.EqualTo(from));
            Assert.That(view.Q<ObjectField>("to-field").value, Is.EqualTo(to));
        }

        /// <summary>
        /// Verifies an edit reported to a host that rebinds the view in response settles after
        /// one report. This is the loop that overflowed the stack: the rebind looked like a
        /// further edit, which made the host rebind again.
        /// </summary>
        [Test]
        public void RebindingFromTheChangedHandlerSettlesAfterOneReport()
        {
            const int RecursionLimit = 5;
            var from = CreateMaterial("From.mat");
            var replacement = CreateMaterial("Replacement.mat");
            var property = CreateSwapEntryProperty(from, null);
            var view = new MateriluneSwapEntryView();
            AttachToPanel(view);
            view.Bind(property, null, MateriluneCandidateMode.None);
            var changedCount = 0;
            view.Changed += () =>
            {
                changedCount++;

                // Stop before the stack overflows so a regression fails the assertion below
                // instead of taking down the test runner.
                if (changedCount >= RecursionLimit)
                {
                    return;
                }

                view.Bind(property, null, MateriluneCandidateMode.None);
            };

            view.Q<ObjectField>("to-field").value = replacement;

            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(GetMaterial(property, "m_to"), Is.EqualTo(replacement));
        }

        /// <summary>
        /// Verifies the replacement source is shown for reference only. Entries are generated
        /// from the target meshes, so the source must not be editable.
        /// </summary>
        [Test]
        public void ReplacementSourceFieldIsReadOnly()
        {
            var from = CreateMaterial("From.mat");
            var other = CreateMaterial("Other.mat");
            var property = CreateSwapEntryProperty(from, null);
            var view = new MateriluneSwapEntryView();
            AttachToPanel(view);
            view.Bind(property, null, MateriluneCandidateMode.None);
            var changedCount = 0;
            view.Changed += () => changedCount++;
            var fromField = view.Q<ObjectField>("from-field");

            Assert.That(fromField.enabledSelf, Is.False);

            fromField.value = other;

            Assert.That(GetMaterial(property, "m_from"), Is.EqualTo(from));
            Assert.That(changedCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a row whose source material is no longer offered by the owning component is
        /// marked, so the user can see why that replacement never reaches Material Swap.
        /// </summary>
        [Test]
        public void OrphanedEntryIsMarked()
        {
            var offered = CreateMaterial("Offered.mat");
            var missing = CreateMaterial("Missing.mat");
            var view = new MateriluneSwapEntryView();
            var row = view.Q<VisualElement>(className: "materilune-swap-entry");
            Assert.That(row, Is.Not.Null);

            view.Bind(CreateSwapEntryProperty(missing, null), new[] { offered }, MateriluneCandidateMode.None);
            Assert.That(row.ClassListContains(MateriluneSwapEntryView.OrphanedClass), Is.True);

            view.Bind(CreateSwapEntryProperty(offered, null), new[] { offered }, MateriluneCandidateMode.None);
            Assert.That(row.ClassListContains(MateriluneSwapEntryView.OrphanedClass), Is.False);
        }

        /// <summary>
        /// Verifies no row is marked when the owning component recorded no materials, which
        /// matches the synchronizer skipping the orphan test in that case.
        /// </summary>
        [Test]
        public void NoEntryIsMarkedWhenTheComponentOffersNothing()
        {
            var material = CreateMaterial("Material.mat");
            var view = new MateriluneSwapEntryView();
            var row = view.Q<VisualElement>(className: "materilune-swap-entry");

            view.Bind(CreateSwapEntryProperty(material, null), null, MateriluneCandidateMode.None);

            Assert.That(row.ClassListContains(MateriluneSwapEntryView.OrphanedClass), Is.False);
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
