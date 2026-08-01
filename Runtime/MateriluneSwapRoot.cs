using System.Collections.Generic;
using UnityEngine;

namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Stores material swap settings for a setup target.
    /// </summary>
    [AddComponentMenu("Materilune/Materilune Swap Root")]
    public sealed class MateriluneSwapRoot : MonoBehaviour, nadena.dev.ndmf.INDMFEditorOnly
    {
        [SerializeField] private GameObject m_setupTarget;
        [SerializeField] private List<Material> m_availableMaterials = new List<Material>();
        [SerializeField] private List<MateriluneMaterialSwapEntry> m_swaps = new List<MateriluneMaterialSwapEntry>();

        /// <summary>
        /// Gets or sets the object that this component configures.
        /// </summary>
        public GameObject SetupTarget
        {
            get => m_setupTarget;
            set => m_setupTarget = value;
        }

        /// <summary>
        /// Gets the materials available on all target meshes.
        /// </summary>
        public List<Material> AvailableMaterials
        {
            get
            {
                if (m_availableMaterials == null)
                {
                    m_availableMaterials = new List<Material>();
                }

                return m_availableMaterials;
            }
        }

        /// <summary>
        /// Gets the material replacement mappings.
        /// </summary>
        public List<MateriluneMaterialSwapEntry> Swaps
        {
            get
            {
                if (m_swaps == null)
                {
                    m_swaps = new List<MateriluneMaterialSwapEntry>();
                }

                return m_swaps;
            }
        }
    }
}
