using System;
using System.Collections.Generic;
using System.Globalization;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the shared Hierarchy button registry and Materilune row detection.
    /// </summary>
    public sealed class MateriluneHierarchyButtonTest
    {
        private const string MissingValue = "__materilune_hierarchy_button_test_missing__";

        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private string m_savedTools;
        private string m_savedMateriluneEntry;
        private string m_savedAmariEntry;
        private string m_savedGarbageEntry;
        private bool m_hadExtraOffset;
        private float m_savedExtraOffset;

        [SetUp]
        public void SetUp()
        {
            m_savedTools = ReadSessionValue(MateriluneHierarchyButtonRegistry.ToolsKey);
            m_savedMateriluneEntry = ReadSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "materilune");
            m_savedAmariEntry = ReadSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "amari");
            m_savedGarbageEntry = ReadSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "garbage");
            m_hadExtraOffset = EditorPrefs.HasKey(MateriluneHierarchyButtonRegistry.ExtraOffsetKey);
            m_savedExtraOffset = EditorPrefs.GetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in m_gameObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            m_gameObjects.Clear();
            RestoreSessionValue(MateriluneHierarchyButtonRegistry.ToolsKey, m_savedTools);
            RestoreSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "materilune",
                m_savedMateriluneEntry);
            RestoreSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "amari",
                m_savedAmariEntry);
            RestoreSessionValue(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "garbage",
                m_savedGarbageEntry);

            if (m_hadExtraOffset)
            {
                EditorPrefs.SetFloat(
                    MateriluneHierarchyButtonRegistry.ExtraOffsetKey,
                    m_savedExtraOffset);
            }
            else
            {
                EditorPrefs.DeleteKey(MateriluneHierarchyButtonRegistry.ExtraOffsetKey);
            }
        }

        /// <summary>
        /// Verifies registration format and idempotence.
        /// </summary>
        [Test]
        public void RegisterSelfAddsOneValidEntry()
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "amari");

            MateriluneHierarchyButtonRegistry.RegisterSelf();
            MateriluneHierarchyButtonRegistry.RegisterSelf();

            var tools = SessionState.GetString(MateriluneHierarchyButtonRegistry.ToolsKey, string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(Array.FindAll(tools, tool => tool == "materilune"), Has.Length.EqualTo(1));

            var entry = SessionState.GetString(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "materilune",
                string.Empty);
            var parts = entry.Split('|');
            Assert.That(parts, Has.Length.EqualTo(4));
            Assert.That(parts[0], Is.EqualTo("1"));
            Assert.That(
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var width),
                Is.True);
            Assert.That(width, Is.GreaterThan(0f));
            Assert.That(parts[2], Is.EqualTo("200"));

            // The row kind tells other tools that this button is only drawn on rows that have
            // been set up, so they do not reserve space for it everywhere.
            Assert.That(
                parts[3],
                Is.EqualTo(MateriluneHierarchyButtonRegistry.RowKindMateriluneSetup));
        }

        /// <summary>
        /// Verifies lower-priority tools contribute their width and malformed entries do not.
        /// </summary>
        [Test]
        public void ComputeOffsetIncludesLowerPriorityToolsAndIgnoresMalformedEntries()
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "materilune");
            MateriluneHierarchyButtonRegistry.RegisterSelf();
            var baseline = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false);

            SessionState.SetString(MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "amari", "1|52|100");
            SessionState.SetString(MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "garbage", "garbage");
            SessionState.SetString(
                MateriluneHierarchyButtonRegistry.ToolsKey,
                "materilune;amari;garbage");

            var withOtherTool = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false);
            Assert.That(withOtherTool - baseline, Is.EqualTo(54f).Within(0.001f));
            Assert.DoesNotThrow(() => MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false));
        }

        /// <summary>
        /// Verifies the shared user offset is included in the computed placement.
        /// </summary>
        /// <summary>
        /// Verifies a tool that only draws on avatar roots reserves space on those rows alone.
        /// Reserving everywhere would leave a gap beside a button that is not drawn.
        /// </summary>
        [Test]
        public void ComputeOffsetOnlyCountsToolsThatDrawOnTheRow()
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "materilune;amari");
            SessionState.SetString(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "materilune",
                "1|24|200|materilune-setup");
            SessionState.SetString(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "amari",
                "1|52|100|avatar-root");

            var onPlainRow = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false);
            var onAvatarRoot = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", true);

            Assert.That(onAvatarRoot - onPlainRow, Is.GreaterThanOrEqualTo(52f));
        }

        /// <summary>
        /// Verifies an unusable stored offset reserves nothing instead of reaching the rectangle.
        /// </summary>
        /// <remarks>
        /// The preference key is not private to this package, so the value can be anything. A
        /// non-finite offset would put the button somewhere undrawable and a negative one would
        /// cancel out space another button already occupies.
        /// </remarks>
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-40f)]
        public void ComputeOffsetIgnoresUnusableExtraOffset(float stored)
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "materilune");
            MateriluneHierarchyButtonRegistry.RegisterSelf();
            EditorPrefs.SetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, 0f);
            var baseline = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false);

            EditorPrefs.SetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, stored);

            Assert.That(
                MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false),
                Is.EqualTo(baseline).Within(0.001f));
        }

        /// <summary>
        /// Verifies an implausibly wide registered entry is rejected rather than honoured.
        /// </summary>
        /// <remarks>
        /// Honouring it would push every button ordered to its left clean off the row.
        /// </remarks>
        [Test]
        public void ComputeOffsetIgnoresAnImplausiblyWideEntry()
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "materilune;garbage");
            SessionState.SetString(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "materilune",
                "1|24|200|materilune-setup");
            SessionState.SetString(
                MateriluneHierarchyButtonRegistry.EntryKeyPrefix + "garbage",
                "1|100000|100|avatar-root");

            Assert.That(
                MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", true),
                Is.LessThan(1000f));
        }

        [Test]
        public void ComputeOffsetIncludesExtraOffset()
        {
            SessionState.SetString(MateriluneHierarchyButtonRegistry.ToolsKey, "materilune");
            MateriluneHierarchyButtonRegistry.RegisterSelf();
            EditorPrefs.SetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, 0f);
            var baseline = MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false);

            EditorPrefs.SetFloat(MateriluneHierarchyButtonRegistry.ExtraOffsetKey, 13.5f);

            Assert.That(
                MateriluneHierarchyButtonRegistry.ComputeOffset("materilune", false) - baseline,
                Is.EqualTo(13.5f).Within(0.001f));
        }

        /// <summary>
        /// Verifies only a direct child marker makes a row eligible.
        /// </summary>
        [Test]
        public void HasMateriluneChildRequiresDirectMarkerComponent()
        {
            var target = CreateGameObject("Target", null);
            var directChild = CreateGameObject("AnyName", target.transform);
            directChild.AddComponent<Materilune>();
            Assert.That(MateriluneHierarchyButton.HasMateriluneChild(target), Is.True);

            var nestedTarget = CreateGameObject("NestedTarget", null);
            var parent = CreateGameObject("Parent", nestedTarget.transform);
            var grandchild = CreateGameObject("Grandchild", parent.transform);
            grandchild.AddComponent<Materilune>();
            Assert.That(MateriluneHierarchyButton.HasMateriluneChild(nestedTarget), Is.False);

            var nameOnlyTarget = CreateGameObject("NameOnlyTarget", null);
            CreateGameObject("Materilune", nameOnlyTarget.transform);
            Assert.That(MateriluneHierarchyButton.HasMateriluneChild(nameOnlyTarget), Is.False);
            Assert.That(MateriluneHierarchyButton.HasMateriluneChild(CreateGameObject("Empty", null)), Is.False);
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private static string ReadSessionValue(string key)
        {
            return SessionState.GetString(key, MissingValue);
        }

        private static void RestoreSessionValue(string key, string value)
        {
            if (value == MissingValue)
            {
                SessionState.EraseString(key);
            }
            else
            {
                SessionState.SetString(key, value);
            }
        }
    }
}
