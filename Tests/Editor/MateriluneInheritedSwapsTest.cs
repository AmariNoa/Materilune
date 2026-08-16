using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests how a nested setup reports the replacements an enclosing setup applies.
    /// </summary>
    public sealed class MateriluneInheritedSwapsTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_gameObjects.Count - 1; index >= 0; index--)
            {
                if (m_gameObjects[index] != null)
                {
                    Object.DestroyImmediate(m_gameObjects[index]);
                }
            }

            foreach (var material in m_materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            m_gameObjects.Clear();
            m_materials.Clear();
        }

        /// <summary>
        /// Verifies a replacement set on the enclosing preset reaches the inner per-mesh row.
        /// </summary>
        [Test]
        public void ResolveForOverrideFindsTheEnclosingPresetReplacement()
        {
            var shared = CreateMaterial();
            var replacement = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            SetSwap(nested.OuterPreset, shared, replacement);

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.SameAs(replacement));
        }

        /// <summary>
        /// Verifies the enclosing per-mesh setting wins over its own whole-preset setting.
        /// </summary>
        /// <remarks>
        /// It sits deeper, which is the order Material Swap itself applies them in.
        /// </remarks>
        [Test]
        public void ResolveForOverridePrefersTheEnclosingPerMeshReplacement()
        {
            var shared = CreateMaterial();
            var presetWide = CreateMaterial();
            var perMesh = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            SetSwap(nested.OuterPreset, shared, presetWide);
            var outerPerMesh = FindOverrideFor(nested.OuterPreset, nested.InnerRenderer);
            Assert.That(outerPerMesh, Is.Not.Null, "the outer setup should cover the inner renderer");
            SetSwap(outerPerMesh, shared, perMesh);

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.SameAs(perMesh));
        }

        /// <summary>
        /// Verifies a switched-off preset contributes nothing, since it is never applied.
        /// </summary>
        [Test]
        public void ResolveForOverrideIgnoresAnInactivePreset()
        {
            var shared = CreateMaterial();
            var replacement = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            SetSwap(nested.OuterPreset, shared, replacement);
            nested.OuterPreset.gameObject.SetActive(false);

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.Null);
        }

        /// <summary>
        /// Verifies a setup with nothing above it inherits nothing.
        /// </summary>
        [Test]
        public void ResolveForOverrideReturnsNullWithoutAnEnclosingSetup()
        {
            var shared = CreateMaterial();
            var target = CreateAvatarRoot();
            var renderer = CreateRenderer(target.transform, shared);
            var manager = MateriluneSetupService.Setup(target, MateriluneOrphanAction.Keep);
            var preset = manager.GetPresets()[0];

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(
                FindOverrideFor(preset, renderer),
                shared);

            Assert.That(resolved, Is.Null);
        }

        /// <summary>
        /// Verifies the whole-preset panel reads only the enclosing whole-preset replacements.
        /// </summary>
        /// <remarks>
        /// An enclosing per-mesh setting covers one renderer, so presenting it as the value for
        /// a panel that covers every mesh would overstate what it does.
        /// </remarks>
        [Test]
        public void ResolveForRootIgnoresTheEnclosingPerMeshReplacement()
        {
            var shared = CreateMaterial();
            var perMesh = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            var outerPerMesh = FindOverrideFor(nested.OuterPreset, nested.InnerRenderer);
            Assert.That(outerPerMesh, Is.Not.Null, "the outer setup should cover the inner renderer");
            SetSwap(outerPerMesh, shared, perMesh);

            var resolved = MateriluneInheritedSwaps.ResolveForRoot(nested.InnerPreset, shared);

            Assert.That(resolved, Is.Null);
        }

        /// <summary>
        /// Verifies the whole-preset panel picks up the enclosing whole-preset replacement.
        /// </summary>
        [Test]
        public void ResolveForRootFindsTheEnclosingPresetReplacement()
        {
            var shared = CreateMaterial();
            var replacement = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            SetSwap(nested.OuterPreset, shared, replacement);

            var resolved = MateriluneInheritedSwaps.ResolveForRoot(nested.InnerPreset, shared);

            Assert.That(resolved, Is.SameAs(replacement));
        }

        /// <summary>
        /// Verifies a setup never reads its own replacements as inherited ones.
        /// </summary>
        [Test]
        public void ResolveForOverrideIgnoresItsOwnSetup()
        {
            var shared = CreateMaterial();
            var own = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            SetSwap(nested.InnerPreset, shared, own);

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.Null);
        }

        /// <summary>
        /// Verifies the deepest of several components addressing one renderer is the one read.
        /// </summary>
        /// <remarks>
        /// A preset can hold more than one, and Material Swap applies the deepest, so reading
        /// the first one found would report a value the avatar never shows.
        /// </remarks>
        [Test]
        public void ResolveForOverrideReadsTheDeepestComponentForTheRenderer()
        {
            var shared = CreateMaterial();
            var shallow = CreateMaterial();
            var deep = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            var outerPerMesh = FindOverrideFor(nested.OuterPreset, nested.InnerRenderer);
            Assert.That(outerPerMesh, Is.Not.Null, "the outer setup should cover the inner renderer");
            SetSwap(outerPerMesh, shared, shallow);

            var extra = CreateGameObject("Extra", outerPerMesh.transform)
                .AddComponent<MateriluneSwapOverride>();
            extra.TargetRenderer = nested.InnerRenderer;
            SetSwap(extra, shared, deep);

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.SameAs(deep));
        }

        /// <summary>
        /// Verifies the later of two components at the same depth is the one read.
        /// </summary>
        /// <remarks>
        /// Material Swap orders by the walk, not by depth, so siblings are settled by their
        /// order among themselves. A rule written in terms of depth alone cannot tell these
        /// two apart and would pick whichever happened to come first.
        /// </remarks>
        [Test]
        public void ResolveForOverrideReadsTheLaterOfTwoSiblingComponents()
        {
            var shared = CreateMaterial();
            var earlier = CreateMaterial();
            var later = CreateMaterial();
            var nested = BuildNestedSetups(shared);
            var outerPerMesh = FindOverrideFor(nested.OuterPreset, nested.InnerRenderer);
            Assert.That(outerPerMesh, Is.Not.Null, "the outer setup should cover the inner renderer");

            // Both hang off the same object, so they are at one depth and only their order
            // among themselves separates them.
            var host = outerPerMesh.transform;
            var first = CreateGameObject("First", host).AddComponent<MateriluneSwapOverride>();
            first.TargetRenderer = nested.InnerRenderer;
            SetSwap(first, shared, earlier);
            var second = CreateGameObject("Second", host).AddComponent<MateriluneSwapOverride>();
            second.TargetRenderer = nested.InnerRenderer;
            SetSwap(second, shared, later);
            Assert.That(second.transform.GetSiblingIndex(), Is.GreaterThan(first.transform.GetSiblingIndex()));

            var resolved = MateriluneInheritedSwaps.ResolveForOverride(nested.InnerOverride, shared);

            Assert.That(resolved, Is.SameAs(later));
        }

        private NestedSetups BuildNestedSetups(Material shared)
        {
            var outerTarget = CreateAvatarRoot();
            var innerTarget = CreateGameObject("Inner", outerTarget.transform);
            var innerRenderer = CreateRenderer(innerTarget.transform, shared);

            var outerManager = MateriluneSetupService.Setup(outerTarget, MateriluneOrphanAction.Keep);
            var innerManager = MateriluneSetupService.Setup(innerTarget, MateriluneOrphanAction.Keep);
            var outerPreset = outerManager.GetPresets()[0];
            var innerPreset = innerManager.GetPresets()[0];

            return new NestedSetups
            {
                OuterPreset = outerPreset,
                InnerPreset = innerPreset,
                InnerRenderer = innerRenderer,
                InnerOverride = FindOverrideFor(innerPreset, innerRenderer),
            };
        }

        private static MateriluneSwapOverride FindOverrideFor(MateriluneSwapRoot preset, Renderer renderer)
        {
            foreach (var candidate in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (candidate != null && candidate.TargetRenderer == renderer)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void SetSwap(MateriluneSwapRoot preset, Material from, Material to)
        {
            SetSwap(preset.Swaps, from, to);
        }

        private static void SetSwap(MateriluneSwapOverride operationOverride, Material from, Material to)
        {
            SetSwap(operationOverride.Swaps, from, to);
        }

        /// <summary>
        /// Replaces the entries of a component with a single one.
        /// </summary>
        /// <remarks>
        /// The property exposes the list itself and cannot be assigned, so the contents are
        /// replaced in place. Setup fills these lists in already, and leaving those entries
        /// there would let an unrelated one answer the lookup under test.
        /// </remarks>
        private static void SetSwap(List<MateriluneMaterialSwapEntry> swaps, Material from, Material to)
        {
            swaps.Clear();
            swaps.Add(new MateriluneMaterialSwapEntry(from, to));
        }

        private GameObject CreateAvatarRoot()
        {
            var root = CreateGameObject("Avatar", null);
            root.AddComponent<NDMFAvatarRoot>();
            return root;
        }

        private Renderer CreateRenderer(Transform parent, Material material)
        {
            var renderer = CreateGameObject("Renderer", parent).AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            return renderer;
        }

        private GameObject CreateGameObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            m_materials.Add(material);
            return material;
        }

        private struct NestedSetups
        {
            internal MateriluneSwapRoot OuterPreset;
            internal MateriluneSwapRoot InnerPreset;
            internal Renderer InnerRenderer;
            internal MateriluneSwapOverride InnerOverride;
        }
    }
}
