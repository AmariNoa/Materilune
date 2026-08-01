using System;
using System.Collections.Generic;
using com.amari_noa.unity_editor_localization_core.editor;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides access to Materilune localized text.
    /// </summary>
    internal static class MateriluneL10n
    {
        /// <summary>
        /// Gets the localized text for the specified key.
        /// </summary>
        /// <param name="key">The localization key.</param>
        /// <param name="fallback">The text to return when the key is unavailable.</param>
        /// <returns>The localized text or fallback text.</returns>
        internal static string Get(string key, string fallback)
        {
            return EditorLocalization.Service.Get(MateriluneConstants.LocalizationSourceId, key, fallback);
        }

        /// <summary>
        /// Gets the language codes available to Materilune.
        /// </summary>
        /// <returns>The available language codes.</returns>
        internal static IReadOnlyList<string> GetAvailableLanguages()
        {
            return EditorLocalization.Service.GetAvailableLanguages(MateriluneConstants.LocalizationSourceId);
        }

        /// <summary>
        /// Gets the current editor-wide language code.
        /// </summary>
        internal static string CurrentLanguageCode => EditorLocalization.Service.CurrentLanguageCode;

        /// <summary>
        /// Sets the editor-wide language code through the Materilune source.
        /// </summary>
        /// <param name="languageCode">The language code to set.</param>
        /// <returns><see langword="true" /> when the language was set successfully; otherwise, <see langword="false" />.</returns>
        internal static bool SetLanguage(string languageCode)
        {
            return EditorLocalization.Service.SetLanguage(
                MateriluneConstants.LocalizationSourceId,
                languageCode) == EditorLocalizationSetLanguageResult.SUCCESS;
        }

        /// <summary>
        /// Adds a listener for editor-wide language changes.
        /// </summary>
        /// <param name="listener">The listener to add.</param>
        internal static void AddLanguageChangedListener(Action<string> listener)
        {
            EditorLocalization.Service.LanguageChanged += listener;
        }

        /// <summary>
        /// Removes a listener for editor-wide language changes.
        /// </summary>
        /// <param name="listener">The listener to remove.</param>
        internal static void RemoveLanguageChangedListener(Action<string> listener)
        {
            EditorLocalization.Service.LanguageChanged -= listener;
        }
    }
}
