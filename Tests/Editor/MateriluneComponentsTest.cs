using System.Collections.Generic;
using com.amari_noa.materilune.runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Tests Materilune runtime component state.
    /// </summary>
    public class MateriluneComponentsTest
    {
        private readonly List<GameObject> m_gameObjects = new List<GameObject>();

        /// <summary>
        /// Destroys objects created by the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in m_gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            m_gameObjects.Clear();
        }

        /// <summary>
        /// Verifies the root component stores its setup target.
        /// </summary>
        [Test]
        public void SwapRootCanBeAddedAndStoresSetupTarget()
        {
            var gameObject = CreateGameObject();
            var setupTarget = CreateGameObject();
            var component = gameObject.AddComponent<MateriluneSwapRoot>();

            component.SetupTarget = setupTarget;

            Assert.That(component.SetupTarget, Is.EqualTo(setupTarget));
        }

        /// <summary>
        /// Verifies the override component stores its target renderer.
        /// </summary>
        [Test]
        public void SwapOverrideCanBeAddedAndStoresTargetRenderer()
        {
            var gameObject = CreateGameObject();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var component = gameObject.AddComponent<MateriluneSwapOverride>();

            component.TargetRenderer = renderer;

            Assert.That(component.TargetRenderer, Is.EqualTo(renderer));
        }

        /// <summary>
        /// Verifies both components initialize their collections.
        /// </summary>
        [Test]
        public void ComponentsInitializeAvailableMaterialsAndSwapsAsEmptyLists()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var overrideComponent = CreateGameObject().AddComponent<MateriluneSwapOverride>();

            Assert.That(root.AvailableMaterials, Is.Not.Null.And.Empty);
            Assert.That(root.Swaps, Is.Not.Null.And.Empty);
            Assert.That(overrideComponent.AvailableMaterials, Is.Not.Null.And.Empty);
            Assert.That(overrideComponent.Swaps, Is.Not.Null.And.Empty);
        }

        /// <summary>
        /// Verifies both components implement the NDMF editor-only marker.
        /// </summary>
        [Test]
        public void ComponentsImplementNdmfEditorOnly()
        {
            var root = CreateGameObject().AddComponent<MateriluneSwapRoot>();
            var overrideComponent = CreateGameObject().AddComponent<MateriluneSwapOverride>();

            Assert.That(root, Is.InstanceOf<nadena.dev.ndmf.INDMFEditorOnly>());
            Assert.That(overrideComponent, Is.InstanceOf<nadena.dev.ndmf.INDMFEditorOnly>());
        }

        private GameObject CreateGameObject()
        {
            var gameObject = new GameObject();
            m_gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
