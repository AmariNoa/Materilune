using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Provides a dropdown for the editor-wide language setting.
    /// </summary>
    public class MateriluneLanguageSelector : VisualElement
    {
        private const string UxmlPath = "Packages/com.amari-noa.materilune/Editor/UI/LanguageSelector/MateriluneLanguageSelector.uxml";
        private const string UssPath = "Packages/com.amari-noa.materilune/Editor/UI/LanguageSelector/MateriluneLanguageSelector.uss";

        private DropdownField m_language;
        private Label m_note;

        /// <summary>
        /// Creates the UXML factory for this element.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<MateriluneLanguageSelector>
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MateriluneLanguageSelector"/> class.
        /// </summary>
        public MateriluneLanguageSelector()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                LogLoadError();
                return;
            }

            visualTree.CloneTree(this);
            styleSheets.Add(styleSheet);

            m_language = this.Q<DropdownField>("language");
            m_note = this.Q<Label>("note");
            if (!HasControls())
            {
                LogLoadError();
                Clear();
                m_language = null;
                m_note = null;
                return;
            }

            m_language.RegisterValueChangedCallback(OnLanguageValueChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            ApplyLocalizedTexts();
            Refresh();
        }

        /// <summary>
        /// Refreshes the available language codes and the current language value.
        /// </summary>
        public void Refresh()
        {
            if (m_language == null)
            {
                return;
            }

            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            var choices = availableLanguages == null
                ? new List<string>()
                : new List<string>(availableLanguages);
            var currentLanguageCode = MateriluneL10n.CurrentLanguageCode;
            if (!string.IsNullOrEmpty(currentLanguageCode) && !choices.Contains(currentLanguageCode))
            {
                choices.Add(currentLanguageCode);
            }

            m_language.choices = choices;
            m_language.SetValueWithoutNotify(currentLanguageCode);
        }

        /// <summary>
        /// Applies the current language to the visible labels and dropdown value.
        /// </summary>
        internal void ApplyLocalizedTexts()
        {
            if (m_language != null)
            {
                m_language.label = MateriluneL10n.Get(
                    "materilune.language.label",
                    "Language");
                m_language.SetValueWithoutNotify(MateriluneL10n.CurrentLanguageCode);
            }

            if (m_note != null)
            {
                m_note.text = MateriluneL10n.Get(
                    "materilune.language.shared_note",
                    "This language setting is shared across the whole Unity Editor and applies to all packages that use Unity Editor Localization Core.");
            }
        }

        private void OnLanguageValueChanged(ChangeEvent<string> changeEvent)
        {
            if (m_language == null)
            {
                return;
            }

            // The shared service falls back to its default language (with a modal dialog) when
            // it receives a code without a Materilune table, so unlisted codes never reach it.
            if (!IsAvailableLanguage(changeEvent.newValue) || !MateriluneL10n.SetLanguage(changeEvent.newValue))
            {
                m_language.SetValueWithoutNotify(changeEvent.previousValue);
            }
        }

        private static bool IsAvailableLanguage(string languageCode)
        {
            var availableLanguages = MateriluneL10n.GetAvailableLanguages();
            if (string.IsNullOrEmpty(languageCode) || availableLanguages == null)
            {
                return false;
            }

            foreach (var availableLanguage in availableLanguages)
            {
                if (availableLanguage == languageCode)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnLanguageChanged(string languageCode)
        {
            if (!HasControls())
            {
                return;
            }

            ApplyLocalizedTexts();
            Refresh();
        }

        private void OnAttachToPanel(AttachToPanelEvent attachEvent)
        {
            MateriluneL10n.AddLanguageChangedListener(OnLanguageChanged);
            ApplyLocalizedTexts();
            Refresh();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
        {
            MateriluneL10n.RemoveLanguageChangedListener(OnLanguageChanged);
        }

        private bool HasControls()
        {
            return m_language != null && m_note != null;
        }

        private static void LogLoadError()
        {
            Debug.LogError(MateriluneL10n.Get(
                "materilune.ui.language_selector.load_error",
                "Materilune could not load the language selector UI assets."));
        }
    }
}
