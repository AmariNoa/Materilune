using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Reads and writes the session-local registry shared by Hierarchy button tools.
    /// </summary>
    internal static class MateriluneHierarchyButtonRegistry
    {
        internal const string ToolsKey = "AmariNoa.HierarchyButtons.v1.Tools";
        internal const string EntryKeyPrefix = "AmariNoa.HierarchyButtons.v1.Entry.";
        internal const string ExtraOffsetKey = "AmariNoa.HierarchyButtons.ExtraOffset";
        internal const string ToolId = "materilune";
        internal const int Priority = 200;
        internal const float Gap = 2f;
        internal const string ButtonLabel = "Mt";

        /// <summary>Row kind for a tool that only draws on avatar roots.</summary>
        internal const string RowKindAvatarRoot = "avatar-root";

        /// <summary>Row kind for a tool that only draws on rows set up for Materilune.</summary>
        internal const string RowKindMateriluneSetup = "materilune-setup";

        private const int CurrentSchema = 1;
        private const float MinimumButtonWidth = 24f;

        // A registered width beyond this is a corrupt entry rather than a button, and honouring
        // it would push every button to the left of it off the row.
        private const float MaximumEntryWidth = 512f;
        private const float FaceEmoWidth = 30f;
        private const string FaceEmoHideKey = "FaceEmo_HideHierarchyIcon";
        private const string FaceEmoOffsetKey = "FaceEmo_HierarchyIconOffset";

        // FaceEmo defaults this offset to 20, not 0, so reading it with a zero default would
        // under-reserve by that much whenever the user has not changed the setting, and the two
        // buttons would overlap (jp.suzuryg.face-emo Editor/Detail/DetailConstants.cs:42).
        private const float FaceEmoDefaultOffset = 20f;

        /// <summary>
        /// Registers Materilune with the current editor session.
        /// </summary>
        internal static void RegisterSelf()
        {
            var tools = ReadSessionString(ToolsKey);
            var toolIds = new List<string>(tools.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            var found = false;
            var normalizedTools = new List<string>(toolIds.Count + 1);
            foreach (var toolId in toolIds)
            {
                if (string.Equals(toolId, ToolId, StringComparison.Ordinal))
                {
                    if (found)
                    {
                        continue;
                    }

                    found = true;
                }

                normalizedTools.Add(toolId);
            }

            if (!found)
            {
                normalizedTools.Add(ToolId);
            }

            WriteSessionString(ToolsKey, string.Join(";", normalizedTools));

            // Registration runs at load, where nothing can be measured, so the fallback width
            // goes in and the first draw corrects it through UpdateRegisteredWidth.
            WriteEntry(MeasureButtonWidth());
        }

        private static void WriteEntry(float width)
        {
            WriteSessionString(
                EntryKeyPrefix + ToolId,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}",
                    CurrentSchema,
                    width.ToString("R", CultureInfo.InvariantCulture),
                    Priority,
                    RowKindMateriluneSetup));
        }

        /// <summary>
        /// Computes the horizontal offset for a registered tool.
        /// </summary>
        /// <param name="toolId">The tool whose button is being drawn.</param>
        /// <param name="isAvatarRoot">Whether the row being drawn is an avatar root.</param>
        /// <returns>The offset from the right edge of the Hierarchy row.</returns>
        /// <remarks>
        /// Only the tools that draw on this row reserve space on it. Tools do not all draw on
        /// the same rows, so reserving for every registered tool everywhere would leave a gap
        /// beside buttons that are not there.
        /// </remarks>
        internal static float ComputeOffset(string toolId, bool isAvatarRoot)
        {
            var offset = ReadFaceEmoReservation(isAvatarRoot) + ReadExtraOffset();
            if (string.IsNullOrEmpty(toolId))
            {
                return offset;
            }

            var ownEntry = ReadEntry(toolId);
            if (!ownEntry.IsValid)
            {
                return offset;
            }

            var tools = ReadSessionString(ToolsKey);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var registeredToolId in tools.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!seen.Add(registeredToolId) ||
                    string.Equals(registeredToolId, toolId, StringComparison.Ordinal))
                {
                    continue;
                }

                var entry = ReadEntry(registeredToolId);
                if (!entry.IsValid ||
                    !DrawsOnRow(entry.RowKind, isAvatarRoot) ||
                    entry.Priority > ownEntry.Priority ||
                    (entry.Priority == ownEntry.Priority &&
                     string.CompareOrdinal(registeredToolId, toolId) >= 0))
                {
                    continue;
                }

                offset += entry.Width + Gap;
            }

            return offset;
        }

        /// <summary>
        /// Measures the button using the same style that is used for drawing it.
        /// </summary>
        /// <returns>The measured width, or the fallback width when it cannot be measured.</returns>
        /// <remarks>
        /// EditorStyles is only usable while the editor is drawing. Outside that, the property
        /// getter throws rather than returning null, so a null check cannot guard it and the
        /// call has to be caught. Registration runs at load, long before any drawing, and an
        /// escaping exception there leaves the whole type permanently broken.
        /// </remarks>
        internal static float MeasureButtonWidth()
        {
            try
            {
                var style = EditorStyles.miniButton;
                if (style == null)
                {
                    return MinimumButtonWidth;
                }

                return Mathf.Max(MinimumButtonWidth, style.CalcSize(new GUIContent(ButtonLabel)).x);
            }
            catch (Exception)
            {
                return MinimumButtonWidth;
            }
        }

        /// <summary>
        /// Writes the measured width when it differs from what is registered.
        /// </summary>
        /// <param name="width">The width measured while drawing.</param>
        /// <remarks>
        /// The width registered at load is the fallback, since nothing can be measured then.
        /// The first draw measures for real and corrects the registration, so the space other
        /// tools leave matches what is actually drawn.
        /// </remarks>
        internal static void UpdateRegisteredWidth(float width)
        {
            var entry = ReadEntry(ToolId);
            if (entry.IsValid && Mathf.Approximately(entry.Width, width))
            {
                return;
            }

            WriteEntry(width);
        }

        /// <summary>
        /// Determines whether a tool that declared a row kind draws on the row being measured.
        /// </summary>
        /// <param name="rowKind">The declared row kind, or empty when not declared.</param>
        /// <param name="isAvatarRoot">Whether the row is an avatar root.</param>
        /// <returns><see langword="true" /> when the tool draws here.</returns>
        /// <remarks>
        /// An undeclared or unknown row kind counts everywhere. Reserving space that turns out
        /// to be unused only leaves a gap, while skipping space that is used makes two buttons
        /// overlap, so the unknown case takes the harmless side.
        /// </remarks>
        private static bool DrawsOnRow(string rowKind, bool isAvatarRoot)
        {
            if (string.Equals(rowKind, RowKindAvatarRoot, StringComparison.Ordinal))
            {
                return isAvatarRoot;
            }

            if (string.Equals(rowKind, RowKindMateriluneSetup, StringComparison.Ordinal))
            {
                // Only rows Materilune itself draws on reach this code.
                return true;
            }

            return true;
        }

        private static float ReadFaceEmoReservation(bool isAvatarRoot)
        {
            if (!isAvatarRoot)
            {
                // FaceEmo only draws on avatar roots, so no other row owes it space.
                return 0f;
            }

#if MATERILUNE_FACEEMO_INSTALLED
            try
            {
                if (EditorPrefs.GetBool(FaceEmoHideKey, false))
                {
                    return 0f;
                }

                return FaceEmoWidth
                    + Sanitize(EditorPrefs.GetFloat(FaceEmoOffsetKey, FaceEmoDefaultOffset))
                    + Gap;
            }
            catch (Exception)
            {
                return 0f;
            }
#else
            return 0f;
#endif
        }

        private static float ReadExtraOffset()
        {
            try
            {
                return Sanitize(EditorPrefs.GetFloat(ExtraOffsetKey, 0f));
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>
        /// Reduces a stored preference to a usable offset.
        /// </summary>
        /// <param name="value">The value read from the preferences.</param>
        /// <returns>The value itself, or zero when it cannot be used as an offset.</returns>
        /// <remarks>
        /// These values come from preference keys that anything can write, including another
        /// tool. A NaN or an infinity would propagate into the button rectangle and put it
        /// somewhere undrawable, and a negative offset would cancel out space that a button is
        /// already occupying, so both fall back to reserving nothing.
        /// </remarks>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        private static RegistryEntry ReadEntry(string toolId)
        {
            var value = ReadSessionString(EntryKeyPrefix + toolId);
            var parts = value.Split('|');
            var rowKind = parts.Length >= 4 ? parts[3] : string.Empty;
            if (parts.Length < 3 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var schema) ||
                schema != CurrentSchema ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority) ||
                width <= 0f ||
                float.IsNaN(width) ||
                float.IsInfinity(width) ||
                width > MaximumEntryWidth)
            {
                return default;
            }

            return new RegistryEntry(width, priority, rowKind, true);
        }

        private static string ReadSessionString(string key)
        {
            try
            {
                return SessionState.GetString(key, string.Empty) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void WriteSessionString(string key, string value)
        {
            SessionState.SetString(key, value);
        }

        private readonly struct RegistryEntry
        {
            internal RegistryEntry(float width, int priority, string rowKind, bool isValid)
            {
                Width = width;
                Priority = priority;
                RowKind = rowKind;
                IsValid = isValid;
            }

            internal readonly float Width;
            internal readonly int Priority;
            internal readonly string RowKind;
            internal readonly bool IsValid;
        }
    }
}
