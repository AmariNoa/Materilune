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
        [SerializeField] private MateriluneSwapOverride m_targetOverride;
        [SerializeField] private List<Material> m_availableMaterials = new List<Material>();
        [SerializeField] private List<MateriluneMaterialSwapEntry> m_swaps = new List<MateriluneMaterialSwapEntry>();
        [SerializeField] private MateriluneCandidateMode m_candidateMode;

        /// <summary>
        /// Gets or sets the object that this component configures.
        /// </summary>
        public GameObject SetupTarget
        {
            get => m_setupTarget;
            set => m_setupTarget = value;
        }

        /// <summary>
        /// Gets or sets the override that stands for the setup target itself. It sits directly
        /// under this preset and hosts every mesh's operation object, so it is held by
        /// reference rather than located by position or by name.
        /// </summary>
        public MateriluneSwapOverride TargetOverride
        {
            get => m_targetOverride;
            set => m_targetOverride = value;
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

        /// <summary>
        /// Gets or sets the mode used to find material candidates.
        /// </summary>
        public MateriluneCandidateMode CandidateMode
        {
            get => m_candidateMode;
            set => m_candidateMode = value;
        }
    }
}
