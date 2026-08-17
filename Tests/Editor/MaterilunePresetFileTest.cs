using System;
using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests writing a preset to the .mlsp form and building one back from it.
    /// </summary>
    public sealed class MaterilunePresetFileTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private Shader m_shader;
        private string m_testDirectory;

        [SetUp]
        public void SetUp()
        {
            m_shader = Shader.Find("Unlit/Color");
            Assert.That(m_shader, Is.Not.Null);

            var folderName = "MaterilunePresetFileTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            m_testDirectory = "Assets/" + folderName;
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

            m_gameObjects.Clear();
            if (!string.IsNullOrEmpty(m_testDirectory) && AssetDatabase.IsValidFolder(m_testDirectory))
            {
                AssetDatabase.DeleteAsset(m_testDirectory);
            }

            Undo.ClearAll();
            MateriluneInspectorIsolation.RestoreSelection();
        }

        /// <summary>
        /// Verifies a preset written out and read back carries its replacements across.
        /// </summary>
        /// <remarks>
        /// The import lands on a second, structurally identical target, which is the sold-
        /// outfit case the file exists for: same meshes, same material assets, new scene.
        /// </remarks>
        [Test]
        public void ExportedPresetImportsOntoAnIdenticalTarget()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            SetReplacement(sourcePreset.Swaps, baseMaterial, blue);
            sourcePreset.gameObject.name = "Ocean";

            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.Preset, Is.Not.Null);
            Assert.That(result.Preset.gameObject.name, Is.EqualTo("Ocean"));
            Assert.That(result.Preset.gameObject.activeSelf, Is.False, "imports arrive inactive");
            Assert.That(result.AppliedCount, Is.GreaterThan(0));
            Assert.That(result.MissingMaterials, Is.Empty);
            Assert.That(FindReplacement(result.Preset.Swaps, baseMaterial), Is.EqualTo(blue));
        }

        /// <summary>
        /// Verifies a per-mesh replacement follows its renderer by stored path.
        /// </summary>
        [Test]
        public void ExportedOverrideFollowsItsRendererAcrossTargets()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            var sourceRenderer = source.GetComponentInChildren<MeshRenderer>();
            var sourceOverride = FindOverrideFor(sourcePreset, sourceRenderer);
            Assert.That(sourceOverride, Is.Not.Null);
            SetReplacement(sourceOverride.Swaps, baseMaterial, blue);

            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.UnmatchedOverrides, Is.Empty);
            var destinationRenderer = destination.GetComponentInChildren<MeshRenderer>();
            var destinationOverride = FindOverrideFor(result.Preset, destinationRenderer);
            Assert.That(destinationOverride, Is.Not.Null);
            Assert.That(FindReplacement(destinationOverride.Swaps, baseMaterial), Is.EqualTo(blue));
        }

        /// <summary>
        /// Verifies a material whose asset does not exist is reported, never substituted.
        /// </summary>
        [Test]
        public void ImportReportsAMaterialWhoseGuidResolvesToNothing()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            SetReplacement(sourcePreset.Swaps, baseMaterial, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            // The replacement's asset disappears between export and import, as it would when
            // a buyer imports a preset without the matching product.
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(blue));

            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.MissingMaterials, Has.Count.EqualTo(1));

            // The report names the material and carries its stored asset path for diagnosis,
            // so the message starts with the name rather than equalling it.
            Assert.That(result.MissingMaterials[0], Does.StartWith("Blue"));
            Assert.That(result.MissingMaterials[0], Does.Contain(".mat"));
            Assert.That(FindReplacement(result.Preset.Swaps, baseMaterial), Is.Null);
        }

        /// <summary>
        /// Verifies an override whose stored path matches nothing is reported and skipped.
        /// </summary>
        [Test]
        public void ImportReportsAnOverrideWhosePathMatchesNothing()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            var sourceRenderer = source.GetComponentInChildren<MeshRenderer>();
            SetReplacement(FindOverrideFor(sourcePreset, sourceRenderer).Swaps, baseMaterial, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = BuildTarget("Destination", baseMaterial);
            destination.GetComponentInChildren<MeshRenderer>().gameObject.name = "SomethingElse";
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.UnmatchedOverrides, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Verifies twins are told apart by their stored sibling index.
        /// </summary>
        /// <remarks>
        /// Unity allows two siblings of one name, and a path made of names alone would land
        /// every import on the first twin.
        /// </remarks>
        [Test]
        public void StoredPathsTellSameNamedSiblingsApart()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var other = CreateAsset("Other.mat");
            var blue = CreateAsset("Blue.mat");

            var source = CreateGameObject("Source", null);
            source.AddComponent<NDMFAvatarRoot>();
            CreateRenderer("Twin", source.transform, baseMaterial);
            var secondTwin = CreateRenderer("Twin", source.transform, other);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            SetReplacement(FindOverrideFor(sourcePreset, secondTwin).Swaps, other, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = CreateGameObject("Destination", null);
            destination.AddComponent<NDMFAvatarRoot>();
            CreateRenderer("Twin", destination.transform, baseMaterial);
            var destinationSecondTwin = CreateRenderer("Twin", destination.transform, other);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.UnmatchedOverrides, Is.Empty);
            var landed = FindOverrideFor(result.Preset, destinationSecondTwin);
            Assert.That(landed, Is.Not.Null);
            Assert.That(FindReplacement(landed.Swaps, other), Is.EqualTo(blue));
        }

        /// <summary>
        /// Verifies one undo removes the imported preset whole.
        /// </summary>
        [Test]
        public void UndoingAnImportRemovesTheImportedPreset()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            SetReplacement(sourcePreset.Swaps, baseMaterial, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            MateriluneInspectorIsolation.DeselectAll();
            Undo.ClearAll();

            MaterilunePresetFile.ImportFromJson(destinationManager, json);
            Assert.That(destinationManager.GetPresets(), Has.Count.EqualTo(2));

            Undo.FlushUndoRecordObjects();
            MateriluneInspectorIsolation.PerformUndo();

            Assert.That(destinationManager.GetPresets(), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Verifies an unreadable file is refused with an explanation, not half-imported.
        /// </summary>
        [Test]
        public void ImportRefusesAFileOfAnotherShape()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);

            Assert.That(
                () => MaterilunePresetFile.ImportFromJson(destinationManager, "{\"schema\":999}"),
                Throws.ArgumentException);
            Assert.That(destinationManager.GetPresets(), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Verifies a replacement on the setup target's own renderer survives the trip.
        /// </summary>
        /// <remarks>
        /// The stored path of that renderer is empty, since walking from the renderer up to
        /// the target crosses nothing, and the resolver used to treat empty as invalid.
        /// </remarks>
        [Test]
        public void ExportCarriesTheTargetsOwnRenderer()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = CreateGameObject("Source", null);
            source.AddComponent<NDMFAvatarRoot>();
            source.AddComponent<MeshRenderer>().sharedMaterials = new[] { baseMaterial };
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            var sourceOverride = FindOverrideFor(sourcePreset, source.GetComponent<MeshRenderer>());
            Assert.That(sourceOverride, Is.Not.Null);
            SetReplacement(sourceOverride.Swaps, baseMaterial, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = CreateGameObject("Destination", null);
            destination.AddComponent<NDMFAvatarRoot>();
            destination.AddComponent<MeshRenderer>().sharedMaterials = new[] { baseMaterial };
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.UnmatchedOverrides, Is.Empty);
            var landed = FindOverrideFor(result.Preset, destination.GetComponent<MeshRenderer>());
            Assert.That(landed, Is.Not.Null);
            Assert.That(FindReplacement(landed.Swaps, baseMaterial), Is.EqualTo(blue));
        }

        /// <summary>
        /// Verifies a file with null entries is imported without stopping.
        /// </summary>
        /// <remarks>
        /// A hand-edited file is outside input; stopping halfway would leave a half-filled
        /// preset in the scene with an exception instead of a report.
        /// </remarks>
        [Test]
        public void ImportToleratesNullEntriesInTheFile()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);

            var json = "{\"schema\":1,\"presetName\":\"Broken\",\"candidateMode\":\"\","
                + "\"rootSwaps\":[null],\"overrides\":[null]}";
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            Assert.That(result.Preset, Is.Not.Null);
            Assert.That(result.AppliedCount, Is.Zero);
        }

        /// <summary>
        /// Verifies each override's candidate mode travels with the file.
        /// </summary>
        [Test]
        public void ExportCarriesTheOverrideCandidateMode()
        {
            var baseMaterial = CreateAsset("Base.mat");
            var blue = CreateAsset("Blue.mat");
            var source = BuildTarget("Source", baseMaterial);
            var sourceManager = MateriluneSetupService.Setup(source, MateriluneOrphanAction.Keep);
            var sourcePreset = sourceManager.GetPresets()[0];
            var sourceRenderer = source.GetComponentInChildren<MeshRenderer>();
            var sourceOverride = FindOverrideFor(sourcePreset, sourceRenderer);
            sourceOverride.CandidateMode = MateriluneCandidateMode.SiblingDirectory;
            SetReplacement(sourceOverride.Swaps, baseMaterial, blue);
            var json = MaterilunePresetFile.ExportToJson(sourcePreset);

            var destination = BuildTarget("Destination", baseMaterial);
            var destinationManager = MateriluneSetupService.Setup(destination, MateriluneOrphanAction.Keep);
            var result = MaterilunePresetFile.ImportFromJson(destinationManager, json);

            var landed = FindOverrideFor(result.Preset, destination.GetComponentInChildren<MeshRenderer>());
            Assert.That(landed.CandidateMode, Is.EqualTo(MateriluneCandidateMode.SiblingDirectory));
        }

        private GameObject BuildTarget(string name, Material material)
        {
            var target = CreateGameObject(name, null);
            target.AddComponent<NDMFAvatarRoot>();
            var body = CreateGameObject("Body", target.transform);
            CreateRenderer("Mesh", body.transform, material);
            return target;
        }

        private MeshRenderer CreateRenderer(string name, Transform parent, Material material)
        {
            var renderer = CreateGameObject(name, parent).AddComponent<MeshRenderer>();
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

        private Material CreateAsset(string fileName)
        {
            var material = new Material(m_shader);
            var path = AssetDatabase.GenerateUniqueAssetPath(m_testDirectory + "/" + fileName);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void SetReplacement(List<MateriluneMaterialSwapEntry> swaps, Material from, Material to)
        {
            for (var index = 0; index < swaps.Count; index++)
            {
                if (swaps[index].From == from)
                {
                    swaps[index] = new MateriluneMaterialSwapEntry(from, to);
                    return;
                }
            }

            Assert.Fail("The generated entries never offered the material the test relies on.");
        }

        private static Material FindReplacement(List<MateriluneMaterialSwapEntry> swaps, Material from)
        {
            foreach (var swap in swaps)
            {
                if (swap.From == from)
                {
                    return swap.To;
                }
            }

            return null;
        }

        private static MateriluneSwapOverride FindOverrideFor(MateriluneSwapRoot preset, Renderer renderer)
        {
            MateriluneSwapOverride last = null;
            foreach (var candidate in preset.GetComponentsInChildren<MateriluneSwapOverride>(true))
            {
                if (candidate != null && candidate.TargetRenderer == renderer)
                {
                    last = candidate;
                }
            }

            return last;
        }
    }
}
