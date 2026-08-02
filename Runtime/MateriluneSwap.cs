using System.Collections.Generic;
using UnityEngine;

namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Manages the swap presets for a setup target.
    /// </summary>
    [AddComponentMenu("Materilune/Materilune Swap")]
    public sealed class MateriluneSwap : MonoBehaviour, nadena.dev.ndmf.INDMFEditorOnly
    {
        /// <summary>
        /// Gets the preset roots that are direct children of this manager.
        /// </summary>
        /// <returns>The preset roots in sibling order, including inactive presets.</returns>
        public List<MateriluneSwapRoot> GetPresets()
        {
            var presets = new List<MateriluneSwapRoot>();
            foreach (Transform child in transform)
            {
                var preset = child.GetComponent<MateriluneSwapRoot>();
                if (preset != null)
                {
                    presets.Add(preset);
                }
            }

            return presets;
        }
    }
}
