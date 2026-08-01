using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests material swap composition.
    /// </summary>
    public class MateriluneSwapComposerTest
    {
        private readonly List<Material> m_materials = new List<Material>();

        /// <summary>
        /// Destroys materials created by the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var material in m_materials)
            {
                Object.DestroyImmediate(material);
            }

            m_materials.Clear();
        }

        /// <summary>
        /// Verifies root-only composition.
        /// </summary>
        [Test]
        public void ComposeWithRootOnlyReturnsRootEntries()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var from = CreateMaterial(shader);
            var to = CreateMaterial(shader);
            var rootSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(from, to),
            };

            var result = MateriluneSwapComposer.Compose(rootSwaps, null);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].From, Is.EqualTo(from));
            Assert.That(result[0].To, Is.EqualTo(to));
        }

        /// <summary>
        /// Verifies matching overrides replace the root destination.
        /// </summary>
        [Test]
        public void ComposeWithMatchingOverrideUsesOverrideTo()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var from = CreateMaterial(shader);
            var rootTo = CreateMaterial(shader);
            var overrideTo = CreateMaterial(shader);
            var rootSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(from, rootTo),
            };
            var overrideSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(from, overrideTo),
            };

            var result = MateriluneSwapComposer.Compose(rootSwaps, overrideSwaps);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].From, Is.EqualTo(from));
            Assert.That(result[0].To, Is.EqualTo(overrideTo));
        }

        /// <summary>
        /// Verifies override-only mappings follow root mappings.
        /// </summary>
        [Test]
        public void ComposeWithOverrideOnlyFromAppendsItAfterRootEntries()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var rootFrom = CreateMaterial(shader);
            var rootTo = CreateMaterial(shader);
            var overrideFrom = CreateMaterial(shader);
            var overrideTo = CreateMaterial(shader);
            var rootSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(rootFrom, rootTo),
            };
            var overrideSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(overrideFrom, overrideTo),
            };

            var result = MateriluneSwapComposer.Compose(rootSwaps, overrideSwaps);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].From, Is.EqualTo(rootFrom));
            Assert.That(result[1].From, Is.EqualTo(overrideFrom));
        }

        /// <summary>
        /// Verifies mappings without a source material are excluded.
        /// </summary>
        [Test]
        public void ComposeExcludesEntriesWithNullFrom()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var rootFrom = CreateMaterial(shader);
            var rootTo = CreateMaterial(shader);
            var rootIgnoredTo = CreateMaterial(shader);
            var overrideIgnoredTo = CreateMaterial(shader);
            var rootSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(null, rootIgnoredTo),
                new MateriluneMaterialSwapEntry(rootFrom, rootTo),
            };
            var overrideSwaps = new List<MateriluneMaterialSwapEntry>
            {
                new MateriluneMaterialSwapEntry(null, overrideIgnoredTo),
            };

            var result = MateriluneSwapComposer.Compose(rootSwaps, overrideSwaps);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].From, Is.EqualTo(rootFrom));
        }

        /// <summary>
        /// Verifies null inputs produce an empty list.
        /// </summary>
        [Test]
        public void ComposeWithNullArgumentsReturnsEmptyList()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);

            var result = MateriluneSwapComposer.Compose(null, null);

            Assert.That(result, Is.Empty);
        }

        private Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
        }
    }
}
