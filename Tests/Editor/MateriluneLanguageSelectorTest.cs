using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the Materilune language selector user interface.
    /// </summary>
    public sealed class MateriluneLanguageSelectorTest
    {
        private string m_originalLanguageCode;
        private UnityEditor.EditorWindow m_window;

        [SetUp]
        public void SetUp()
        {
            m_originalLanguageCode = MateriluneL10n.CurrentLanguageCode;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_window != null)
            {
                m_window.Close();
                m_window = null;
            }

            if (!string.IsNullOrEmpty(m_originalLanguageCode))
            {
                var restored = MateriluneL10n.SetLanguage(m_originalLanguageCode);
                var availableLanguages = MateriluneL10n.GetAvailableLanguages();
                if (availableLanguages != null
                    && new List<string>(availableLanguages).Contains(m_originalLanguageCode))
                {
                    Assert.That(restored, Is.True,
                        "Failed to restore the editor language to " + m_originalLanguageCode + ".");
                }
            }

            m_originalLanguageCode = null;
        }

        // BaseField dispatches ChangeEvent only while attached to a panel, so tests that
        // drive the dropdown through its value setter host the selector in a throwaway window.
        private void AttachToPanel(MateriluneLanguageSelector selector)
        {
            m_window = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            m_window.ShowUtility();
            m_window.rootVisualElement.Add(selector);
        }

        [Test]
        public void ConstructorCreatesLanguageDropdownAndNote()
        {
            var selector = new MateriluneLanguageSelector();
            var dropdown = selector.Q<DropdownField>("language");
            var note = selector.Q<Label>("note");

            Assert.That(dropdown, Is.Not.Null);
            Assert.That(note, Is.Not.Null);
            CollectionAssert.AreEqual(
                new List<string>(MateriluneL10n.GetAvailableLanguages()),
                dropdown.choices);
            Assert.That(dropdown.value, Is.EqualTo(MateriluneL10n.CurrentLanguageCode));
        }

        [Test]
        public void ChangingDropdownValueChangesCurrentLanguage()
        {
            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            if (availableLanguages.Count < 2)
            {
                Assert.Ignore("At least two languages are required for this test.");
            }

            var selector = new MateriluneLanguageSelector();
            AttachToPanel(selector);
            var dropdown = selector.Q<DropdownField>("language");
            var targetLanguage = availableLanguages[0] == MateriluneL10n.CurrentLanguageCode
                ? availableLanguages[1]
                : availableLanguages[0];

            dropdown.value = targetLanguage;

            Assert.That(MateriluneL10n.CurrentLanguageCode, Is.EqualTo(targetLanguage));
        }

        [Test]
        public void ApplyingLocalizedTextsUsesTheSelectedLanguageTranslation()
        {
            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            if (availableLanguages.Count < 2)
            {
                Assert.Ignore("At least two languages are required for this test.");
            }

            var selector = new MateriluneLanguageSelector();
            var note = selector.Q<Label>("note");
            var targetLanguage = availableLanguages[0] == MateriluneL10n.CurrentLanguageCode
                ? availableLanguages[1]
                : availableLanguages[0];

            var initialText = note.text;
            Assert.That(MateriluneL10n.SetLanguage(targetLanguage), Is.True);
            selector.Refresh();
            selector.ApplyLocalizedTexts();

            Assert.That(
                note.text,
                Is.EqualTo(MateriluneL10n.Get(
                    "materilune.language.shared_note",
                    "This language setting is shared across the whole Unity Editor and applies to all packages that use Unity Editor Localization Core.")));
            Assert.That(note.text, Is.Not.EqualTo(initialText));
        }

        [Test]
        public void InvalidDropdownValueIsRejectedWithoutThrowing()
        {
            var selector = new MateriluneLanguageSelector();
            AttachToPanel(selector);
            var dropdown = selector.Q<DropdownField>("language");
            var originalLanguageCode = dropdown.value;

            Assert.DoesNotThrow(() => dropdown.value = "invalid-language-code");

            Assert.That(MateriluneL10n.CurrentLanguageCode, Is.EqualTo(originalLanguageCode));
            Assert.That(dropdown.value, Is.EqualTo(originalLanguageCode));
        }
    }
}
