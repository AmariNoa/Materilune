using System;
using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Hosts the material candidate picker as a dropdown editor window.
    /// </summary>
    internal sealed class MateriluneCandidatePickerWindow : EditorWindow
    {
        // 320 x 280 fits the translated tabs, empty message, and a short material list without resizing.
        private static readonly Vector2 PopupSize = new Vector2(320f, 280f);

        private Material m_current;
        private MateriluneCandidateMode m_initialTab;
        private Action<Material> m_onSelected;
        private MateriluneCandidatePickerView m_picker;

        /// <summary>
        /// Opens a candidate picker below the specified button.
        /// </summary>
        /// <param name="buttonWorldBound">
        /// The opening button's worldBound, in the coordinates of the panel that holds it.
        /// </param>
        /// <param name="current">The current replacement material or its source material.</param>
        /// <param name="initialTab">The tab selected when the picker is shown.</param>
        /// <param name="onSelected">The callback invoked after a candidate is selected.</param>
        internal static void Open(
            Rect buttonWorldBound,
            Material current,
            MateriluneCandidateMode initialTab,
            Action<Material> onSelected)
        {
            var window = CreateInstance<MateriluneCandidatePickerWindow>();
            window.m_current = current;
            window.m_initialTab = initialTab;
            window.m_onSelected = onSelected;

            // worldBound is measured inside the panel, while ShowAsDropDown places the window on
            // the screen, so the rectangle has to be converted or the popup lands somewhere
            // unrelated to the button. ShowAsDropDown itself keeps the window on screen, putting
            // it below the rectangle when there is room and above it when there is not.
            window.ShowAsDropDown(GUIUtility.GUIToScreenRect(buttonWorldBound), PopupSize);
        }

        private void CreateGUI()
        {
            ClearPicker();
            rootVisualElement.Clear();
            m_picker = new MateriluneCandidatePickerView();
            rootVisualElement.Add(m_picker);
            m_picker.CandidateSelected += OnCandidateSelected;
            m_picker.Show(m_current, m_initialTab);
        }

        private void OnDisable()
        {
            ClearPicker();
            m_onSelected = null;
            m_current = null;
        }

        private void OnCandidateSelected(Material material)
        {
            try
            {
                m_onSelected?.Invoke(material);
            }
            finally
            {
                Close();
            }
        }

        private void ClearPicker()
        {
            if (m_picker == null)
            {
                return;
            }

            m_picker.CandidateSelected -= OnCandidateSelected;
            m_picker.Clear();
            m_picker = null;
        }
    }
}
