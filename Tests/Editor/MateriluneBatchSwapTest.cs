using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests learning a replacement rule from one example and planning what it would do.
    /// </summary>
    public sealed class MateriluneBatchSwapTest
    {
        private readonly List<Material> m_materials = new List<Material>();
        private Shader m_shader;
        private string m_testDirectory;

        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MateriluneBatchSwapTest_" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.IsValidFolder("Assets/" + folderName), Is.False);
            AssetDatabase.CreateFolder("Assets", folderName);
            m_testDirectory = "Assets/" + folderName;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var material in m_materials)
            {
                if (material != null && !AssetDatabase.Contains(material))
                {
                    Object.DestroyImmediate(material);
                }
            }

            m_materials.Clear();
            if (!string.IsNullOrEmpty(m_testDirectory) && AssetDatabase.IsValidFolder(m_testDirectory))
            {
                AssetDatabase.DeleteAsset(m_testDirectory);
            }

            m_testDirectory = null;

            foreach (var window in Resources.FindObjectsOfTypeAll<MateriluneBatchSwapWindow>())
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        /// <summary>
        /// Verifies opening the window again replaces the one already open instead of stacking
        /// a second one, so the single window on screen belongs to the button pressed last.
        /// </summary>
        [Test]
        public void OpenReplacesTheWindowAlreadyOpen()
        {
            MateriluneBatchSwapWindow.Open(
                new List<MateriluneMaterialSwapEntry>(),
                MateriluneCandidateMode.None,
                approved => { });
            var firstWindows = Resources.FindObjectsOfTypeAll<MateriluneBatchSwapWindow>();
            Assert.That(firstWindows, Has.Length.EqualTo(1));
            var firstWindow = firstWindows[0];

            MateriluneBatchSwapWindow.Open(
                new List<MateriluneMaterialSwapEntry>(),
                MateriluneCandidateMode.None,
                approved => { });

            // What survives must be the second window: the count alone would also pass if
            // the first one somehow refused the replacement.
            var remainingWindows = Resources.FindObjectsOfTypeAll<MateriluneBatchSwapWindow>();
            Assert.That(remainingWindows, Has.Length.EqualTo(1));
            Assert.That(remainingWindows[0], Is.Not.SameAs(firstWindow));
        }

        /// <summary>
        /// Verifies the rule is the part of the name the example actually changes.
        /// </summary>
        /// <remarks>
        /// The shared part runs to "PrismRibbon_00", the two zeroes included, so what is left
        /// over is shorter than the numbering reads. That is the point: the rule carries only
        /// what differs, and putting it back gives the same names either way.
        /// </remarks>
        [Test]
        public void LearnTakesTheDifferenceBetweenTheTwoNames()
        {
            var sample = CreateAsset("PrismRibbon_001Pink.mat");
            var replacement = CreateAsset("PrismRibbon_002Blue.mat");

            var rule = MateriluneBatchSwapRule.Learn(sample, replacement);

            Assert.That(rule.IsValid, Is.True);
            Assert.That(rule.From, Is.EqualTo("1Pink"));
            Assert.That(rule.To, Is.EqualTo("2Blue"));
            Assert.That(rule.Apply("PrismRibbon_001Pink_Jewel"), Is.EqualTo("PrismRibbon_002Blue_Jewel"));
        }

        /// <summary>
        /// Verifies a replacement that only adds to the name falls back to the whole name.
        /// </summary>
        /// <remarks>
        /// Trimming the shared parts of "Pink" and "PinkPastel" leaves nothing to look for, and
        /// pairs like these are common in real data, so the whole sample name serves as the
        /// rule instead of the difference.
        /// </remarks>
        [Test]
        public void LearnFallsBackToTheWholeNameWhenNothingIsRemoved()
        {
            var sample = CreateAsset("Pink.mat");
            var replacement = CreateAsset("PinkPastel.mat");

            var rule = MateriluneBatchSwapRule.Learn(sample, replacement);

            Assert.That(rule.IsValid, Is.True);
            Assert.That(rule.From, Is.EqualTo("Pink"));
            Assert.That(rule.To, Is.EqualTo("PinkPastel"));
            Assert.That(rule.Apply("Pink_Jewel"), Is.EqualTo("PinkPastel_Jewel"));
        }

        /// <summary>
        /// Verifies a replacement that only shortens the name still uses the difference.
        /// </summary>
        /// <remarks>
        /// The reverse direction leaves the removed part as the thing to look for, so it needs
        /// no fallback and keeps matching rows the whole name would miss.
        /// </remarks>
        [Test]
        public void LearnUsesTheRemovedPartWhenTheNameOnlyShrinks()
        {
            var sample = CreateAsset("PinkPastel.mat");
            var replacement = CreateAsset("Pink.mat");

            var rule = MateriluneBatchSwapRule.Learn(sample, replacement);

            Assert.That(rule.IsValid, Is.True);
            Assert.That(rule.From, Is.EqualTo("Pastel"));
            Assert.That(rule.To, Is.EqualTo(string.Empty));
            Assert.That(rule.Apply("PinkPastel_Jewel"), Is.EqualTo("Pink_Jewel"));
        }

        /// <summary>
        /// Verifies a rule cannot be learned from two identical names.
        /// </summary>
        /// <remarks>
        /// There is no difference to carry to the other rows, and an empty rule would otherwise
        /// match every name and replace nothing in it.
        /// </remarks>
        [Test]
        public void LearnRefusesTwoNamesThatAreTheSame()
        {
            // Not assets: saving two files of one name renames the second, and the point here
            // is two materials whose names really are identical. Learn only reads the names.
            var sample = CreateLooseMaterial("Same");
            var replacement = CreateLooseMaterial("Same");

            Assert.That(MateriluneBatchSwapRule.Learn(sample, replacement).IsValid, Is.False);
        }

        /// <summary>
        /// Verifies every occurrence in a name is replaced, not only the first.
        /// </summary>
        [Test]
        public void ApplyReplacesEveryOccurrence()
        {
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("Pink.mat"),
                CreateAsset("Blue.mat"));

            Assert.That(rule.Apply("Pink_Body_Pink"), Is.EqualTo("Blue_Body_Blue"));
        }

        /// <summary>
        /// Verifies a name the rule says nothing about is left alone.
        /// </summary>
        [Test]
        public void ApplyReturnsNullForAnUnrelatedName()
        {
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("Pink.mat"),
                CreateAsset("Blue.mat"));

            Assert.That(rule.Apply("kirakira"), Is.Null);
        }

        /// <summary>
        /// Verifies the plan finds the renamed material and marks the row ready.
        /// </summary>
        [Test]
        public void PlanFindsTheRenamedMaterialAmongTheCandidates()
        {
            var pink = CreateAsset("PrismRibbon_001Pink_Jewel.mat");
            CreateAsset("PrismRibbon_002Blue_Jewel.mat");
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("PrismRibbon_001Pink.mat"),
                CreateAsset("PrismRibbon_002Blue.mat"));

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(pink, null) },
                rule,
                MateriluneCandidateMode.SameDirectory);

            Assert.That(plan, Has.Count.EqualTo(1));
            Assert.That(plan[0].Status, Is.EqualTo(MateriluneBatchSwapStatus.Ready));
            Assert.That(plan[0].To.name, Is.EqualTo("PrismRibbon_002Blue_Jewel"));
        }

        /// <summary>
        /// Verifies a row that already carries a replacement is marked as an overwrite.
        /// </summary>
        /// <remarks>
        /// The window leaves these unticked, so the distinction has to survive planning.
        /// </remarks>
        [Test]
        public void PlanMarksARowThatAlreadyHasAReplacement()
        {
            var pink = CreateAsset("PrismRibbon_001Pink_Jewel.mat");
            var blue = CreateAsset("PrismRibbon_002Blue_Jewel.mat");
            var existing = CreateAsset("Something_Else.mat");
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("PrismRibbon_001Pink.mat"),
                CreateAsset("PrismRibbon_002Blue.mat"));

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(pink, existing) },
                rule,
                MateriluneCandidateMode.SameDirectory);

            Assert.That(plan[0].Status, Is.EqualTo(MateriluneBatchSwapStatus.Overwrite));
            Assert.That(plan[0].To, Is.EqualTo(blue));
        }

        /// <summary>
        /// Verifies a row the rule says nothing about is reported as such.
        /// </summary>
        [Test]
        public void PlanReportsARowTheRuleDoesNotApplyTo()
        {
            var unrelated = CreateAsset("kirakira.mat");
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("PrismRibbon_001Pink.mat"),
                CreateAsset("PrismRibbon_002Blue.mat"));

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(unrelated, null) },
                rule,
                MateriluneCandidateMode.SameDirectory);

            Assert.That(plan[0].Status, Is.EqualTo(MateriluneBatchSwapStatus.NotMatched));
            Assert.That(plan[0].IsApplicable, Is.False);
        }

        /// <summary>
        /// Verifies a renamed material that no candidate offers is reported, not invented.
        /// </summary>
        /// <remarks>
        /// The search is confined to what the row's own picker would show, so a material that
        /// exists elsewhere in the project must not be pulled in.
        /// </remarks>
        [Test]
        public void PlanReportsARowWhoseRenamedMaterialIsNotOffered()
        {
            var pink = CreateAsset("PrismRibbon_001Pink_Jewel.mat");
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("PrismRibbon_001Pink.mat"),
                CreateAsset("PrismRibbon_002Blue.mat"));

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(pink, null) },
                rule,
                MateriluneCandidateMode.SameDirectory);

            Assert.That(plan[0].Status, Is.EqualTo(MateriluneBatchSwapStatus.NoCandidate));
            Assert.That(plan[0].To, Is.Null);
            Assert.That(plan[0].ExpectedName, Is.EqualTo("PrismRibbon_002Blue_Jewel"));
        }

        /// <summary>
        /// Verifies planning without a usable rule produces nothing rather than everything.
        /// </summary>
        [Test]
        public void PlanReturnsNothingWithoutAUsableRule()
        {
            var pink = CreateAsset("PrismRibbon_001Pink.mat");

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(pink, null) },
                default,
                MateriluneCandidateMode.SameDirectory);

            Assert.That(plan, Is.Empty);
        }

        /// <summary>
        /// Verifies planning with the component's default mode still searches the real tabs.
        /// </summary>
        /// <remarks>
        /// Components store None unless someone changed it, and None as a search range finds
        /// nothing. This is how the feature failed in a real scene while every test, which
        /// spelled the mode out, stayed green.
        /// </remarks>
        [Test]
        public void PlanSearchesThePickerTabsWhenTheComponentModeIsNone()
        {
            var pink = CreateAsset("PrismRibbon_001Pink_Jewel.mat");
            CreateAsset("PrismRibbon_002Blue_Jewel.mat");
            var rule = MateriluneBatchSwapRule.Learn(
                CreateAsset("PrismRibbon_001Pink.mat"),
                CreateAsset("PrismRibbon_002Blue.mat"));

            var plan = MateriluneBatchSwap.Plan(
                new List<MateriluneMaterialSwapEntry> { new MateriluneMaterialSwapEntry(pink, null) },
                rule,
                MateriluneCandidateMode.None);

            Assert.That(plan[0].Status, Is.EqualTo(MateriluneBatchSwapStatus.Ready));
            Assert.That(plan[0].To.name, Is.EqualTo("PrismRibbon_002Blue_Jewel"));
        }

        private Material CreateLooseMaterial(string name)
        {
            var material = new Material(m_shader) { name = name };
            m_materials.Add(material);
            return material;
        }

        private Material CreateAsset(string fileName)
        {
            var material = new Material(m_shader);
            var path = AssetDatabase.GenerateUniqueAssetPath(m_testDirectory + "/" + fileName);
            AssetDatabase.CreateAsset(material, path);
            var loaded = AssetDatabase.LoadAssetAtPath<Material>(path);
            m_materials.Add(loaded);
            return loaded;
        }
    }
}
