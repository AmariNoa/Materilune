using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests automatic material replacement entry generation and filtering.
    /// </summary>
    public sealed class MateriluneSwapEntriesTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

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
            Undo.ClearAll();
            MateriluneInspectorIsolation.RestoreSelection();
        }

        [Test]
        public void RebuildCreatesEntriesInAvailableMaterialOrderWithNullDestinations()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var first = CreateMaterial();
            var second = CreateMaterial();
            root.AvailableMaterials.AddRange(new[] { first, second });

            Assert.That(MateriluneSwapEntries.Rebuild(root), Is.True);

            Assert.That(root.Swaps, Has.Count.EqualTo(2));
            AssertEntry(root.Swaps[0], first, null);
            AssertEntry(root.Swaps[1], second, null);
        }

        [Test]
        public void RebuildPreservesExistingDestinationsForMatchingSources()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var first = CreateMaterial();
            var second = CreateMaterial();
            var replacement = CreateMaterial();
            root.AvailableMaterials.AddRange(new[] { first, second });
            root.Swaps.Add(new MateriluneMaterialSwapEntry(second, replacement));

            MateriluneSwapEntries.Rebuild(root);

            AssertEntry(root.Swaps[0], first, null);
            AssertEntry(root.Swaps[1], second, replacement);
        }

        [Test]
        public void RebuildRetainsOrphansAfterAvailableEntriesInTheirOriginalOrder()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var available = CreateMaterial();
            var firstOrphan = CreateMaterial();
            var secondOrphan = CreateMaterial();
            var firstReplacement = CreateMaterial();
            var secondReplacement = CreateMaterial();
            root.AvailableMaterials.Add(available);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(firstOrphan, firstReplacement));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(available, null));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(null, secondReplacement));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(secondOrphan, secondReplacement));

            MateriluneSwapEntries.Rebuild(root);

            Assert.That(root.Swaps, Has.Count.EqualTo(3));
            AssertEntry(root.Swaps[0], available, null);
            AssertEntry(root.Swaps[1], firstOrphan, firstReplacement);
            AssertEntry(root.Swaps[2], secondOrphan, secondReplacement);
        }

        [Test]
        public void RebuildReturnsFalseWithoutDirtyingOrRecordingAnUnchangedObject()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var material = CreateMaterial();
            root.AvailableMaterials.Add(material);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(material, null));
            EditorUtility.ClearDirty(root);
            Undo.ClearAll();

            Assert.That(MateriluneSwapEntries.Rebuild(root), Is.False);
            Assert.That(EditorUtility.IsDirty(root), Is.False);

            MateriluneInspectorIsolation.DeselectAll();
            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();
            Assert.That(root.Swaps, Has.Count.EqualTo(1));
            AssertEntry(root.Swaps[0], material, null);
        }

        /// <summary>
        /// Verifies the update check runs against the materials the meshes carry now, not against
        /// the list recorded at setup time, which is itself what falls behind.
        /// </summary>
        [Test]
        public void NeedsUpdateFollowsTheMaterialsAssignedToTheTarget()
        {
            var target = CreateGameObject();
            var renderer = target.AddComponent<MeshRenderer>();
            var first = CreateMaterial();
            renderer.sharedMaterials = new[] { first };
            var manager = CreateGameObject().AddComponent<MateriluneSwap>();
            var preset = CreateGameObject(manager.transform).AddComponent<MateriluneSwapRoot>();
            preset.SetupTarget = target;
            var operationOverride = CreateGameObject(preset.transform).AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = renderer;

            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.True);

            var orphan = CreateMaterial();
            var replacement = CreateMaterial();
            preset.Swaps.Add(new MateriluneMaterialSwapEntry(first, null));
            preset.Swaps.Add(new MateriluneMaterialSwapEntry(orphan, replacement));
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(first, null));

            // Updating does not remove the orphan, so its presence is not a reason to update.
            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.False);

            var second = CreateMaterial();
            renderer.sharedMaterials = new[] { first, second };
            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.True);
        }

        /// <summary>
        /// Verifies a mesh added after setup is reported even when it carries no material, since
        /// the material comparison alone cannot see a mesh that has no operation object.
        /// </summary>
        [Test]
        public void NeedsUpdateReportsAMeshWithoutAnOperationObject()
        {
            var target = CreateGameObject();
            var mesh = CreateGameObject(target.transform);
            var renderer = mesh.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[0];
            var manager = CreateGameObject().AddComponent<MateriluneSwap>();
            var preset = CreateGameObject(manager.transform).AddComponent<MateriluneSwapRoot>();
            preset.SetupTarget = target;

            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.True);

            var operationOverride = CreateGameObject(preset.transform).AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = renderer;
            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.False);
        }

        [Test]
        public void SynchronizationExcludesNullDestinationsAndOrphansButKeepsEmptyComponents()
        {
            var rootObject = CreateGameObject();
            var root = rootObject.AddComponent<MateriluneSwapRoot>();
            var materialSwap = rootObject.AddComponent<ModularAvatarMaterialSwap>();
            var available = CreateMaterial();
            var orphan = CreateMaterial();
            var replacement = CreateMaterial();
            root.AvailableMaterials.Add(available);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(available, null));
            root.Swaps.Add(new MateriluneMaterialSwapEntry(orphan, replacement));

            MateriluneSwapSynchronizer.Sync(root);

            Assert.That(materialSwap, Is.Not.Null);
            Assert.That(materialSwap.Swaps, Is.Empty);
        }

        [Test]
        public void SynchronizationCopiesOnlyCurrentEntriesForEachOverride()
        {
            var rootObject = CreateGameObject();
            var root = rootObject.AddComponent<MateriluneSwapRoot>();
            var rootMaterialSwap = rootObject.AddComponent<ModularAvatarMaterialSwap>();
            var overrideObject = CreateGameObject(rootObject.transform);
            var operationOverride = overrideObject.AddComponent<MateriluneSwapOverride>();
            var overrideMaterialSwap = overrideObject.AddComponent<ModularAvatarMaterialSwap>();
            var available = CreateMaterial();
            var replacement = CreateMaterial();
            var orphan = CreateMaterial();
            operationOverride.AvailableMaterials.Add(available);
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(available, replacement));
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(orphan, replacement));

            MateriluneSwapSynchronizer.Sync(root);

            Assert.That(rootMaterialSwap.Swaps, Is.Empty);
            Assert.That(overrideMaterialSwap.Swaps, Has.Count.EqualTo(1));
            Assert.That(overrideMaterialSwap.Swaps[0].From, Is.EqualTo(available));
            Assert.That(overrideMaterialSwap.Swaps[0].To, Is.EqualTo(replacement));
        }

        [Test]
        public void SynchronizationKeepsEntriesWhenAvailableMaterialsAreEmpty()
        {
            var rootObject = CreateGameObject();
            var root = rootObject.AddComponent<MateriluneSwapRoot>();
            var materialSwap = rootObject.AddComponent<ModularAvatarMaterialSwap>();
            var from = CreateMaterial();
            var to = CreateMaterial();
            root.Swaps.Add(new MateriluneMaterialSwapEntry(from, to));

            MateriluneSwapSynchronizer.Sync(root);

            Assert.That(materialSwap.Swaps, Has.Count.EqualTo(1));
            Assert.That(materialSwap.Swaps[0].From, Is.EqualTo(from));
            Assert.That(materialSwap.Swaps[0].To, Is.EqualTo(to));
        }

        /// <summary>
        /// Verifies an override whose mesh left the target does not keep the update prompt on.
        /// Updating cannot give such an override entries, so reporting it would never clear.
        /// </summary>
        [Test]
        public void NeedsUpdateIgnoresAnOverrideWhoseMeshLeftTheTarget()
        {
            var target = CreateGameObject();
            var outsideMesh = CreateGameObject();
            var outsideRenderer = outsideMesh.AddComponent<MeshRenderer>();
            outsideRenderer.sharedMaterials = new[] { CreateMaterial() };
            var manager = CreateGameObject().AddComponent<MateriluneSwap>();
            var preset = CreateGameObject(manager.transform).AddComponent<MateriluneSwapRoot>();
            preset.SetupTarget = target;
            var operationOverride = CreateGameObject(preset.transform).AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = outsideRenderer;

            Assert.That(MateriluneSwapEntries.NeedsUpdate(manager), Is.False);
        }

        /// <summary>
        /// Verifies an entry whose source material asset was deleted is kept. The reference
        /// reports itself as null through the Unity operator but can resolve again, so dropping
        /// it would lose the replacement the user chose.
        /// </summary>
        [Test]
        public void RebuildKeepsAnEntryWhoseSourceMaterialWasDestroyed()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var available = CreateMaterial();
            var destroyed = CreateMaterial();
            var replacement = CreateMaterial();
            root.AvailableMaterials.Add(available);
            root.Swaps.Add(new MateriluneMaterialSwapEntry(destroyed, replacement));
            Object.DestroyImmediate(destroyed);

            Assert.That(MateriluneSwapEntries.Rebuild(root), Is.True);

            Assert.That(root.Swaps, Has.Count.EqualTo(2));
            AssertEntry(root.Swaps[0], available, null);
            Assert.That(root.Swaps[1].To, Is.EqualTo(replacement));
        }

        [Test]
        public void RebuildThrowsForNullArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => MateriluneSwapEntries.Rebuild((MateriluneSwapRoot)null));
            Assert.Throws<ArgumentNullException>(
                () => MateriluneSwapEntries.Rebuild((MateriluneSwapOverride)null));
            Assert.Throws<ArgumentNullException>(
                () => MateriluneSwapEntries.NeedsUpdate(null));
        }

        private GameObject CreateGameObject(Transform parent = null)
        {
            var gameObject = new GameObject("MateriluneSwapEntriesTestObject");
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial()
        {
            var material = new Material(GetShader());
            m_materials.Add(material);
            return material;
        }

        private static Shader GetShader()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            return shader;
        }

        private static void AssertEntry(
            MateriluneMaterialSwapEntry entry,
            Material expectedFrom,
            Material expectedTo)
        {
            Assert.That(entry.From, Is.EqualTo(expectedFrom));
            Assert.That(entry.To, Is.EqualTo(expectedTo));
        }
    }
}
