using NUnit.Framework;

namespace com.amari_noa.materilune.tests.editor
{
    /// <summary>
    /// Verifies that the package assemblies resolve their Modular Avatar and NDMF references.
    /// Failing to resolve them breaks compilation before this test runs, so the assertions here
    /// guard against a reference being silently dropped from an asmdef.
    /// </summary>
    public class AssemblyWiringTest
    {
        [Test]
        public void ModularAvatarMaterialSwapTypeIsResolvable()
        {
            Assert.That(typeof(nadena.dev.modular_avatar.core.ModularAvatarMaterialSwap), Is.Not.Null);
        }

        [Test]
        public void NdmfEditorOnlyInterfaceIsResolvable()
        {
            Assert.That(typeof(nadena.dev.ndmf.INDMFEditorOnly), Is.Not.Null);
        }
    }
}
