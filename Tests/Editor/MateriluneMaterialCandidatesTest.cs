using System;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests material candidate discovery.
    /// </summary>
    public class MateriluneMaterialCandidatesTest
    {
        private Shader m_shader;
        private string TestDirectory { get; set; }

        /// <summary>
        /// Creates the temporary asset directory used by each test. The name is unique per run
        /// so the test never deletes a folder it did not create itself.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MateriluneCandidatesTest_" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.IsValidFolder("Assets/" + folderName), Is.False);
            AssetDatabase.CreateFolder("Assets", folderName);
            TestDirectory = "Assets/" + folderName;
        }

        /// <summary>
        /// Removes the directory this test created, and only that directory.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(TestDirectory) && AssetDatabase.IsValidFolder(TestDirectory))
            {
                AssetDatabase.DeleteAsset(TestDirectory);
            }

            TestDirectory = null;
        }

        /// <summary>
        /// Verifies modes and materials without an asset path return no candidates.
        /// </summary>
        [Test]
        public void GetCandidatesReturnsEmptyForNoneNullAndNonAssetMaterials()
        {
            var assetMaterial = CreateMaterial(TestDirectory, "Asset.mat");
            var transientMaterial = new Material(m_shader);

            try
            {
                Assert.That(MateriluneMaterialCandidates.GetCandidates(assetMaterial, MateriluneCandidateMode.None), Is.Empty);
                Assert.That(MateriluneMaterialCandidates.GetCandidates(null, MateriluneCandidateMode.SameDirectory), Is.Empty);
                Assert.That(MateriluneMaterialCandidates.GetCandidates(transientMaterial, MateriluneCandidateMode.SameDirectory), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(transientMaterial);
            }
        }

        /// <summary>
        /// Verifies same-directory candidates are sorted and exclude nested directories.
        /// </summary>
        [Test]
        public void GetCandidatesSameDirectoryReturnsSortedDirectMaterialsIncludingCurrent()
        {
            var current = CreateMaterial(TestDirectory, "B_Current.mat");
            var first = CreateMaterial(TestDirectory, "A_First.mat");
            var last = CreateMaterial(TestDirectory, "C_Last.mat");
            var nestedDirectory = CreateFolder(TestDirectory, "Nested");
            CreateMaterial(nestedDirectory, "Ignored.mat");

            var result = MateriluneMaterialCandidates.GetCandidates(current, MateriluneCandidateMode.SameDirectory);

            Assert.That(result, Is.EqualTo(new[] { first, current, last }));
        }

        /// <summary>
        /// Verifies each sibling directory contributes only its closest material.
        /// </summary>
        [Test]
        public void GetCandidatesSiblingDirectorySelectsClosestMaterialPerDirectory()
        {
            var colorA = CreateFolder(TestDirectory, "ColorA");
            var colorB = CreateFolder(TestDirectory, "ColorB");
            var current = CreateMaterial(colorA, "Skin_A.mat");
            var closestInA = CreateMaterial(colorA, "Skin_A_Alt.mat");
            CreateMaterial(colorA, "Unrelated.mat");
            var closestInB = CreateMaterial(colorB, "Skin_B.mat");
            CreateMaterial(colorB, "Unrelated.mat");

            var result = MateriluneMaterialCandidates.GetCandidates(current, MateriluneCandidateMode.SiblingDirectory);

            Assert.That(result, Is.EqualTo(new[] { current, closestInB }));
            Assert.That(result, Has.No.Member(closestInA));
        }

        /// <summary>
        /// Verifies distance comparison ignores case and equal distances fall back to path order.
        /// </summary>
        [Test]
        public void GetCandidatesSiblingDirectoryIgnoresCaseAndBreaksTiesByPath()
        {
            var colorA = CreateFolder(TestDirectory, "ColorA");
            var colorB = CreateFolder(TestDirectory, "ColorB");
            var current = CreateMaterial(colorA, "Skin.mat");
            // Same name in different case is distance zero when case is ignored, so it must win
            // over a candidate that differs by one character.
            var caseOnly = CreateMaterial(colorB, "SKIN.mat");
            CreateMaterial(colorB, "Skin1.mat");
            var colorC = CreateFolder(TestDirectory, "ColorC");
            // Both differ from Skin by exactly one appended character; the tie must resolve to
            // the candidate whose asset path sorts first.
            var tieFirst = CreateMaterial(colorC, "SkinA.mat");
            CreateMaterial(colorC, "SkinB.mat");

            var result = MateriluneMaterialCandidates.GetCandidates(current, MateriluneCandidateMode.SiblingDirectory);

            Assert.That(result, Is.EqualTo(new[] { current, caseOnly, tieFirst }));
        }

        /// <summary>
        /// Verifies materials in grandchild directories are not candidates.
        /// </summary>
        [Test]
        public void GetCandidatesSiblingDirectoryExcludesGrandchildDirectories()
        {
            var colorA = CreateFolder(TestDirectory, "ColorA");
            var colorB = CreateFolder(TestDirectory, "ColorB");
            var current = CreateMaterial(colorA, "Skin.mat");
            var directCandidate = CreateMaterial(colorB, "SkinVariant.mat");
            var nestedDirectory = CreateFolder(colorB, "Nested");
            CreateMaterial(nestedDirectory, "Skin.mat");

            var result = MateriluneMaterialCandidates.GetCandidates(current, MateriluneCandidateMode.SiblingDirectory);

            Assert.That(result, Is.EqualTo(new[] { current, directCandidate }));
        }

        /// <summary>
        /// Verifies unknown candidate modes are rejected.
        /// </summary>
        [Test]
        public void GetCandidatesThrowsForUnknownMode()
        {
            var material = CreateMaterial(TestDirectory, "Material.mat");

            Assert.That(
                () => MateriluneMaterialCandidates.GetCandidates(material, (MateriluneCandidateMode)123),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => MateriluneMaterialCandidates.GetCandidates(null, (MateriluneCandidateMode)123),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>
        /// Verifies candidate modes are stored by both runtime components.
        /// </summary>
        [Test]
        public void ComponentsStoreCandidateModeWithNoneAsTheDefault()
        {
            var rootObject = new GameObject();
            var overrideObject = new GameObject();
            try
            {
                var root = rootObject.AddComponent<MateriluneSwapRoot>();
                var overrideComponent = overrideObject.AddComponent<MateriluneSwapOverride>();

                Assert.That(root.CandidateMode, Is.EqualTo(MateriluneCandidateMode.None));
                Assert.That(overrideComponent.CandidateMode, Is.EqualTo(MateriluneCandidateMode.None));

                root.CandidateMode = MateriluneCandidateMode.SameDirectory;
                overrideComponent.CandidateMode = MateriluneCandidateMode.SiblingDirectory;

                Assert.That(root.CandidateMode, Is.EqualTo(MateriluneCandidateMode.SameDirectory));
                Assert.That(overrideComponent.CandidateMode, Is.EqualTo(MateriluneCandidateMode.SiblingDirectory));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(overrideObject);
            }
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
