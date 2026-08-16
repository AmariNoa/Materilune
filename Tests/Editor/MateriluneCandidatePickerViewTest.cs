using System;
using System.Collections;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the material candidate picker without opening its editor window.
    /// </summary>
    public sealed class MateriluneCandidatePickerViewTest
    {
        private Shader m_shader;
        private string m_testDirectory;
        private MateriluneCandidatePickerView m_view;

        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MateriluneCandidatePickerViewTest_" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.IsValidFolder("Assets/" + folderName), Is.False);
            AssetDatabase.CreateFolder("Assets", folderName);
            m_testDirectory = "Assets/" + folderName;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_view != null)
            {
                m_view.Clear();
                m_view = null;
            }

            Undo.ClearAll();
            if (!string.IsNullOrEmpty(m_testDirectory) && AssetDatabase.IsValidFolder(m_testDirectory))
            {
                AssetDatabase.DeleteAsset(m_testDirectory);
            }

            m_testDirectory = null;
        }

        [Test]
        public void ShowUsesSameDirectoryByDefaultAndSiblingTabReplacesCandidates()
        {
            var currentDirectory = CreateFolder(m_testDirectory, "Current");
            var siblingDirectory = CreateFolder(m_testDirectory, "Variant");
            var first = CreateMaterial(currentDirectory, "A_Skin.mat");
            var current = CreateMaterial(currentDirectory, "B_Skin.mat");
            var last = CreateMaterial(currentDirectory, "C_Skin.mat");
            var sibling = CreateMaterial(siblingDirectory, "B_Skin.mat");
            m_view = new MateriluneCandidatePickerView();

            m_view.Show(current, MateriluneCandidateMode.None);

            var list = m_view.Q<ListView>("lv-candidates");
            Assert.That(GetMaterials(list), Is.EqualTo(new Material[] { null, first, current, last }));
            Assert.That(
                m_view.Q<Button>("btn-tab-same-directory").ClassListContains(
                    "materilune-candidate-picker__tab--selected"),
                Is.True);

            m_view.SelectTab(MateriluneCandidateMode.SiblingDirectory);

            Assert.That(GetMaterials(list), Is.EqualTo(new Material[] { null, current, sibling }));
            Assert.That(
                m_view.Q<Button>("btn-tab-sibling-directory").ClassListContains(
                    "materilune-candidate-picker__tab--selected"),
                Is.True);
        }

        /// <summary>
        /// Verifies an empty result shows the message without taking anything out of the layout.
        /// The message is toggled with visibility and the list stays in the tree, so the popup
        /// keeps its shape whichever tab is open.
        /// </summary>
        /// <remarks>
        /// The checks read the inline styles the code sets, not resolved values. A resolved
        /// value needs the element attached to a panel and a layout pass, which an EditMode test
        /// cannot rely on. Styles written in the uxml style attribute are not readable through
        /// IStyle either, since they are applied as a generated rule rather than inline.
        /// </remarks>
        [Test]
        public void EmptyCandidatesShowMessageAndKeepListElement()
        {
            var currentDirectory = CreateFolder(m_testDirectory, "Current");
            var current = CreateMaterial(currentDirectory, "Only.mat");
            m_view = new MateriluneCandidatePickerView();

            m_view.Show(null, MateriluneCandidateMode.None);

            var list = m_view.Q<ListView>("lv-candidates");
            var emptyLabel = m_view.Q<Label>("lbl-empty");
            Assert.That(GetMaterials(list), Is.EqualTo(new Material[] { null }));
            Assert.That(emptyLabel.style.visibility.value, Is.EqualTo(Visibility.Visible));
            Assert.That(list.parent, Is.Not.Null);

            m_view.Show(current, MateriluneCandidateMode.SameDirectory);

            Assert.That(emptyLabel.style.visibility.value, Is.EqualTo(Visibility.Hidden));
            Assert.That(list.parent, Is.Not.Null);

            // Neither state may take an element out of the layout, so display stays untouched.
            Assert.That(emptyLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(list.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void SelectingCandidateRaisesEventOnce()
        {
            var currentDirectory = CreateFolder(m_testDirectory, "Current");
            var first = CreateMaterial(currentDirectory, "A_Skin.mat");
            var current = CreateMaterial(currentDirectory, "B_Skin.mat");
            CreateMaterial(currentDirectory, "C_Skin.mat");
            m_view = new MateriluneCandidatePickerView();
            var selected = new List<Material>();
            m_view.CandidateSelected += selected.Add;

            m_view.Show(current, MateriluneCandidateMode.SameDirectory);
            m_view.Q<ListView>("lv-candidates").SetSelection(1);

            Assert.That(selected, Is.EqualTo(new[] { first }));
        }

        /// <summary>
        /// Verifies the first row clears the replacement. It reports a null material, which the
        /// host writes to the entry the same way it writes a chosen one.
        /// </summary>
        [Test]
        public void SelectingTheFirstRowReportsNoMaterial()
        {
            var currentDirectory = CreateFolder(m_testDirectory, "Current");
            var current = CreateMaterial(currentDirectory, "B_Skin.mat");
            m_view = new MateriluneCandidatePickerView();
            var raised = 0;
            Material reported = null;
            m_view.CandidateSelected += material =>
            {
                raised++;
                reported = material;
            };

            m_view.Show(current, MateriluneCandidateMode.SameDirectory);
            m_view.Q<ListView>("lv-candidates").SetSelection(0);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(reported, Is.Null);
        }

        /// <summary>
        /// Verifies a candidate row shows the material name and reserves a slot for the preview.
        /// The slot is bound to the material even before Unity has rendered the thumbnail, so a
        /// row keeps its shape while previews arrive.
        /// </summary>
        [Test]
        public void CandidateRowShowsTheNameAndReservesThePreviewSlot()
        {
            var currentDirectory = CreateFolder(m_testDirectory, "Current");
            var current = CreateMaterial(currentDirectory, "Only.mat");
            m_view = new MateriluneCandidatePickerView();
            m_view.Show(current, MateriluneCandidateMode.SameDirectory);

            var clearRow = m_view.BuildCandidateRowForTests(0);
            var materialRow = m_view.BuildCandidateRowForTests(1);

            Assert.That(clearRow, Is.Not.Null);
            Assert.That(clearRow.Q<Label>("lbl-material-name").text, Is.Not.Empty);
            Assert.That(clearRow.Q<Image>("img-material-preview").userData, Is.Null);

            Assert.That(materialRow, Is.Not.Null);
            var label = materialRow.Q<Label>("lbl-material-name");
            var preview = materialRow.Q<Image>("img-material-preview");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo(current.name));
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.userData, Is.SameAs(current));
        }

        /// <summary>
        /// Verifies the measured width follows the longest name the popup will show, so a name
        /// is not cut off. The window clamps the result, which is why this checks the raw
        /// measurement rather than a final size.
        /// </summary>
        [Test]
        public void MeasuredWidthGrowsWithTheLongestCandidateName()
        {
            var shortDirectory = CreateFolder(m_testDirectory, "Short");
            var shortNamed = CreateMaterial(shortDirectory, "A.mat");

            var narrow = MateriluneCandidatePickerView.MeasureRequiredWidth(shortNamed);

            var longDirectory = CreateFolder(m_testDirectory, "Long");
            var longNamed = CreateMaterial(
                longDirectory,
                "A_very_long_material_name_that_does_not_fit_the_default_popup_width.mat");

            var wide = MateriluneCandidatePickerView.MeasureRequiredWidth(longNamed);

            Assert.That(wide, Is.GreaterThan(narrow));
            Assert.That(wide, Is.GreaterThan(320f));
        }

        private static List<Material> GetMaterials(ListView list)
        {
            var materials = new List<Material>();
            foreach (var item in (IEnumerable)list.itemsSource)
            {
                materials.Add(item as Material);
            }

            return materials;
        }

        private static string CreateFolder(string parent, string name)
        {
            AssetDatabase.CreateFolder(parent, name);
            return parent + "/" + name;
        }

        private Material CreateMaterial(string directory, string fileName)
        {
            var material = new Material(m_shader);
            AssetDatabase.CreateAsset(material, directory + "/" + fileName);
            return material;
        }
    }
}
