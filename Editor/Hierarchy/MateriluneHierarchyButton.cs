using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Draws the Materilune launcher on rows that have been set up for Materilune.
    /// </summary>
    [InitializeOnLoad]
    internal static class MateriluneHierarchyButton
    {
        private const float ButtonHeight = 15f;
        private const float MinimumButtonWidth = 24f;

        // A night sky behind a moon: deep blue ground, warm yellow letters.
        private static readonly Color NightSky = new Color32(24, 32, 78, 255);
        private static readonly Color MoonYellow = new Color32(255, 214, 92, 255);

        private static GUIStyle s_style;
        private static bool s_reportedFailure;

        static MateriluneHierarchyButton()
        {
            // A static constructor that throws leaves the type unusable for the rest of the
            // session, and this one runs while the Hierarchy is drawing a row, which would take
            // the whole row down with it. Nothing here is worth that, so it reports and carries
            // on without the button.
            try
            {
                EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
                MateriluneHierarchyButtonRegistry.RegisterSelf();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Determines whether a Hierarchy row has a directly managed child.
        /// </summary>
        /// <param name="gameObject">The object represented by the row.</param>
        /// <returns><see langword="true" /> when a direct child carries the Materilune marker.</returns>
        internal static bool HasMateriluneChild(GameObject gameObject)
        {
            if (gameObject == null || gameObject.transform == null)
            {
                return false;
            }

            var parent = gameObject.transform;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child == null || child.gameObject == null)
                {
                    continue;
                }

                if (child.GetComponent<Materilune>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // The subscribers of this callback share one invocation, so an exception escaping
            // here stops the ones registered after it from drawing their own buttons. It is
            // reported rather than swallowed, but only the first time: this runs for every row
            // of every repaint, and a recurring fault would otherwise bury the console.
            try
            {
                DrawButton(instanceID, selectionRect);
            }
            catch (System.Exception exception)
            {
                if (!s_reportedFailure)
                {
                    s_reportedFailure = true;
                    Debug.LogException(exception);
                }
            }
        }

        private static void DrawButton(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (!HasMateriluneChild(gameObject))
            {
                return;
            }

            var style = GetStyle();
            if (style == null)
            {
                return;
            }

            // The label is an abbreviation of the package name and is expected to read the
            // same in every language, but it still goes through the translation table: the rule
            // that displayed text is never a literal has one exception, and it is MenuItem paths.
            var label = MateriluneL10n.Get(
                "materilune.ui.hierarchy.button_label",
                MateriluneHierarchyButtonRegistry.ButtonLabel);
            var tooltip = MateriluneL10n.Get(
                "materilune.ui.hierarchy.button_tooltip",
                "Open Materilune");
            var content = new GUIContent(label, tooltip);

            // Measured with the style that draws it, so the bold letters are accounted for, and
            // measured here because the editor is drawing. The width registered at load could
            // only be the fallback, so it is corrected now.
            var buttonWidth = Mathf.Max(MinimumButtonWidth, style.CalcSize(content).x);
            MateriluneHierarchyButtonRegistry.UpdateRegisteredWidth(buttonWidth);
            var offset = MateriluneHierarchyButtonRegistry.ComputeOffset(
                MateriluneHierarchyButtonRegistry.ToolId,
                nadena.dev.ndmf.runtime.RuntimeUtil.IsAvatarRoot(gameObject.transform));
            // Centred on the row, then snapped to a whole pixel. An odd height in a 16 pixel
            // row lands on a half pixel, which display scaling rounds up or down unpredictably;
            // this is the drift the sibling package papers over with a fixed 2 pixel shift.
            var buttonRect = new Rect(
                Mathf.Round(selectionRect.xMax - offset - buttonWidth),
                Mathf.Round(selectionRect.y + ((selectionRect.height - ButtonHeight) * 0.5f)),
                buttonWidth,
                ButtonHeight);

            // The colour is applied by tinting a white background rather than by assigning a
            // texture to the style, which is why it is set around the call and restored after.
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = NightSky;
            try
            {
                if (GUI.Button(buttonRect, content, style))
                {
                    MateriluneWindow.ShowWindow(gameObject);
                }
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }
        }

        /// <summary>
        /// Builds the button style, reusing it across rows and frames.
        /// </summary>
        /// <returns>The style, or <see langword="null" /> when it cannot be built yet.</returns>
        /// <remarks>
        /// A colour cannot be given to a button by putting a filled texture in the style's
        /// background: a skin style also carries scaled backgrounds for high density displays,
        /// and those are drawn in preference to it, so the assigned colour never appears. The
        /// working approach, and the one the sibling package already uses, is to give every
        /// state the built-in white texture and tint it with GUI.backgroundColor at draw time.
        /// </remarks>
        private static GUIStyle GetStyle()
        {
            if (s_style != null)
            {
                return s_style;
            }

            try
            {
                var white = Texture2D.whiteTexture;
                s_style = new GUIStyle(GUI.skin.button)
                {
                    // Same weight and size as the sibling package's Hierarchy button, and its
                    // padding is left untouched for the same reason: overriding the vertical
                    // padding to zero is what made the letters sit against the top edge.
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    normal = { background = white, textColor = MoonYellow },
                    hover = { background = white, textColor = MoonYellow },
                    active = { background = white, textColor = MoonYellow },
                    focused = { background = white, textColor = MoonYellow },
                };

                // The scaled backgrounds inherited from the skin would be drawn instead of the
                // tinted white texture, so they are cleared on every state that is used.
                var empty = new Texture2D[0];
                s_style.normal.scaledBackgrounds = empty;
                s_style.hover.scaledBackgrounds = empty;
                s_style.active.scaledBackgrounds = empty;
                s_style.focused.scaledBackgrounds = empty;
                return s_style;
            }
            catch (System.Exception)
            {
                // GUI.skin is only usable while drawing. Outside that it is unavailable, and the
                // button waits for a frame where the style can be built.
                s_style = null;
                return null;
            }
        }
    }
}
