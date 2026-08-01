using com.amari_noa.materilune.editor;
using com.amari_noa.unity_editor_localization_core.editor;
using NUnit.Framework;
using UnityEditor;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Verifies the Materilune localization foundation.
    /// </summary>
    public sealed class MateriluneLocalizationTest
    {
        /// <summary>
        /// This test intentionally fails until the Localization folder guid is set after Unity generates its meta file.
        /// </summary>
        [Test]
        public void LocalizationFolderGuidResolvesToAssetPath()
        {
            Assert.That(MateriluneConstants.LocalizationFolderGuid, Is.Not.Empty);
            Assert.That(
                AssetDatabase.GUIDToAssetPath(MateriluneConstants.LocalizationFolderGuid),
                Is.Not.Empty);
        }

        /// <summary>
        /// Verifies that the Materilune localization source is registered.
        /// </summary>
        [Test]
        public void LocalizationSourceIsRegistered()
        {
            Assert.That(
                EditorLocalization.Service.RegisteredSourceIds,
                Does.Contain(MateriluneConstants.LocalizationSourceId));
        }

        /// <summary>
        /// Verifies that both supported language codes are available.
        /// </summary>
        [Test]
        public void AvailableLanguagesIncludeEnglishAndJapanese()
        {
            var availableLanguages = MateriluneL10n.GetAvailableLanguages();

            Assert.That(availableLanguages, Does.Contain("en-US"));
            Assert.That(availableLanguages, Does.Contain("ja-JP"));
        }

        /// <summary>
        /// Verifies that the Japanese translation has the same keys as the base translation.
        /// </summary>
        [Test]
        public void JapaneseTranslationHasNoKeyDifferences()
        {
            var result = EditorLocalization.Service.ValidateLanguageDiff(
                MateriluneConstants.LocalizationSourceId,
                "ja-JP");

            Assert.That(result.HasError, Is.False);
            Assert.That(result.MissingKeys, Is.Empty);
            Assert.That(result.ExtraKeys, Is.Empty);
            Assert.That(result.ParseErrors, Is.Empty);
        }

        /// <summary>
        /// Verifies that a translated string can be retrieved.
        /// </summary>
        [Test]
        public void ExistingKeyReturnsLocalizedText()
        {
            Assert.That(
                MateriluneL10n.Get("materilune.warning.managed_by_materilune", "FALLBACK"),
                Is.Not.EqualTo("FALLBACK"));
        }

        /// <summary>
        /// Verifies that a missing key returns its fallback text.
        /// </summary>
        [Test]
        public void MissingKeyReturnsFallbackText()
        {
            Assert.That(
                MateriluneL10n.Get("materilune.no.such.key", "FALLBACK"),
                Is.EqualTo("FALLBACK"));
        }
    }
}
