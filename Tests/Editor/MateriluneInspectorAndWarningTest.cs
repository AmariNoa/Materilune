using System.Collections.Generic;
using com.amari_noa.materilune.editor;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests the summary inspectors and renderer management detection.
    /// </summary>
    public sealed class MateriluneInspectorAndWarningTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();
        private readonly List<Material> m_materials = new List<Material>();
        private readonly List<Editor> m_editors = new List<Editor>();

        [TearDown]
        public void TearDown()
        {
            foreach (var editor in m_editors)
            {
                if (editor != null)
                {
                    Object.DestroyImmediate(editor);
                }
            }

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

            m_editors.Clear();
            m_gameObjects.Clear();
            m_materials.Clear();
            MateriluneRendererInspectorWarning.InvalidateCache();
            Undo.ClearAll();
            MateriluneInspectorIsolation.RestoreSelection();
        }

        [Test]
        public void EachSummaryInspectorCreatesTheSharedLayout()
        {
            var manager = CreateGameObject("Manager").AddComponent<MateriluneSwap>();
            var preset = CreateGameObject("Preset", manager.transform).AddComponent<MateriluneSwapRoot>();
            var operationOverride = CreateGameObject("Override", preset.transform)
                .AddComponent<MateriluneSwapOverride>();

            AssertInspectorLayout(manager);
            AssertInspectorLayout(preset);
            AssertInspectorLayout(operationOverride);
        }

        [Test]
        public void SummariesContainTheCurrentComponentState()
        {
            var managerObject = CreateGameObject("Manager");
            var manager = managerObject.AddComponent<MateriluneSwap>();
            var firstPreset = CreateGameObject("FirstPreset", managerObject.transform)
                .AddComponent<MateriluneSwapRoot>();
            var activePreset = CreateGameObject("ActivePreset", managerObject.transform)
                .AddComponent<MateriluneSwapRoot>();
            firstPreset.gameObject.SetActive(false);

            var managerEditor = CreateEditor(manager);
            var managerVisualTree = managerEditor.CreateInspectorGUI();
            Assert.That(managerVisualTree.Q<Label>("lbl-summary").text, Does.Contain("2"));
            Assert.That(managerVisualTree.Q<Label>("lbl-summary").text, Does.Contain(activePreset.name));

            var targetObject = CreateGameObject("Target");
            activePreset.SetupTarget = targetObject;
            var fromMaterial = CreateMaterial();
            var toMaterial = CreateMaterial();
            activePreset.AvailableMaterials.Add(fromMaterial);
            activePreset.Swaps.Add(new MateriluneMaterialSwapEntry(fromMaterial, toMaterial));
            activePreset.Swaps.Add(new MateriluneMaterialSwapEntry(fromMaterial, null));
            var overrideObject = CreateGameObject("Override", activePreset.transform);
            var operationOverride = overrideObject.AddComponent<MateriluneSwapOverride>();
            var renderer = targetObject.AddComponent<MeshRenderer>();
            operationOverride.TargetRenderer = renderer;
            operationOverride.Swaps.Add(new MateriluneMaterialSwapEntry(fromMaterial, toMaterial));

            var rootEditor = CreateEditor(activePreset);
            var root = rootEditor.CreateInspectorGUI();
            var rootSummary = root.Q<Label>("lbl-summary").text;
            Assert.That(rootSummary, Does.Contain(targetObject.name));
            Assert.That(rootSummary, Does.Contain("2"));
            Assert.That(rootSummary, Does.Contain("3"));

            var overrideEditor = CreateEditor(operationOverride);
            var overrideVisualTree = overrideEditor.CreateInspectorGUI();
            var overrideSummary = overrideVisualTree.Q<Label>("lbl-summary").text;
            Assert.That(overrideSummary, Does.Contain("1"));
            Assert.That(overrideSummary, Does.Contain(renderer.name));
        }

        [Test]
        public void InspectorsDoNotThrowForNullAndDestroyedReferences()
        {
            var nullPreset = CreateGameObject("NullPreset").AddComponent<MateriluneSwapRoot>();
            var nullOverride = CreateGameObject("NullOverride")
                .AddComponent<MateriluneSwapOverride>();
            Assert.DoesNotThrow(() => CreateEditor(nullPreset).CreateInspectorGUI());
            Assert.DoesNotThrow(() => CreateEditor(nullOverride).CreateInspectorGUI());

            var targetObject = CreateGameObject("Target");
            var renderer = targetObject.AddComponent<MeshRenderer>();
            var preset = CreateGameObject("Preset").AddComponent<MateriluneSwapRoot>();
            preset.SetupTarget = targetObject;
            var operationOverride = CreateGameObject("Override")
                .AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = renderer;

            Object.DestroyImmediate(targetObject);

            Assert.DoesNotThrow(() => CreateEditor(preset).CreateInspectorGUI());
            Assert.DoesNotThrow(() => CreateEditor(operationOverride).CreateInspectorGUI());
        }

        [Test]
        public void MultipleSelectionDoesNotThrow()
        {
            var first = CreateGameObject("First").AddComponent<MateriluneSwap>();
            var second = CreateGameObject("Second").AddComponent<MateriluneSwap>();
            var editor = Editor.CreateEditor(new Object[] { first, second });
            m_editors.Add(editor);

            Assert.DoesNotThrow(() => editor.CreateInspectorGUI());
        }

        [Test]
        public void RendererManagementUsesReferenceIdentityNotNames()
        {
            var managedObject = CreateGameObject("SameName");
            var unmanagedObject = CreateGameObject("SameName");
            var managedRenderer = managedObject.AddComponent<MeshRenderer>();
            var unmanagedRenderer = unmanagedObject.AddComponent<MeshRenderer>();
            var operationOverride = CreateGameObject("Override")
                .AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = managedRenderer;

            MateriluneRendererInspectorWarning.InvalidateCache();

            Assert.That(MateriluneRendererInspectorWarning.IsManaged(managedRenderer), Is.True);
            Assert.That(MateriluneRendererInspectorWarning.IsManaged(unmanagedRenderer), Is.False);
        }

        /// <summary>
        /// Verifies the warning applies to the game object whose header the inspector draws.
        /// The header event carries that object rather than each component below it, so testing
        /// only for a Renderer target would never match and the warning would never appear.
        /// </summary>
        [Test]
        public void WarningAppliesToTheGameObjectThatCarriesAManagedRenderer()
        {
            var managedObject = CreateGameObject("Managed");
            var unmanagedObject = CreateGameObject("Unmanaged");
            var managedRenderer = managedObject.AddComponent<MeshRenderer>();
            unmanagedObject.AddComponent<MeshRenderer>();
            var operationOverride = CreateGameObject("Override")
                .AddComponent<MateriluneSwapOverride>();
            operationOverride.TargetRenderer = managedRenderer;

            MateriluneRendererInspectorWarning.InvalidateCache();

            Assert.That(MateriluneRendererInspectorWarning.ShouldWarnFor(managedObject), Is.True);
            Assert.That(MateriluneRendererInspectorWarning.ShouldWarnFor(managedRenderer), Is.True);
            Assert.That(MateriluneRendererInspectorWarning.ShouldWarnFor(unmanagedObject), Is.False);
            Assert.That(MateriluneRendererInspectorWarning.ShouldWarnFor(null), Is.False);
        }

        private void AssertInspectorLayout(Object component)
        {
            var editor = CreateEditor(component);
            var visualTree = editor.CreateInspectorGUI();
            Assert.That(visualTree, Is.Not.Null);
            Assert.That(visualTree.Q<Label>("lbl-summary"), Is.Not.Null);
            Assert.That(visualTree.Q<Button>("btn-open-window"), Is.Not.Null);
        }

        private Editor CreateEditor(Object component)
        {
            var editor = Editor.CreateEditor(component);
            m_editors.Add(editor);
            return editor;
        }

        private GameObject CreateGameObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            m_gameObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            m_materials.Add(material);
            return material;
        }
    }
}
