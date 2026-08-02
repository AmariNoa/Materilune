using System.Collections.Generic;
using UnityEngine;

namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Stores per-renderer material swap settings.
    /// </summary>
    [AddComponentMenu("Materilune/Materilune Swap Override")]
    public sealed class MateriluneSwapOverride : MonoBehaviour, nadena.dev.ndmf.INDMFEditorOnly
    {
        [SerializeField] private Renderer m_targetRenderer;
        [SerializeField] private List<Material> m_availableMaterials = new List<Material>();
        [SerializeField] private List<MateriluneMaterialSwapEntry> m_swaps = new List<MateriluneMaterialSwapEntry>();
        [SerializeField] private MateriluneCandidateMode m_candidateMode;

        /// <summary>
        /// Gets or sets the renderer that this component overrides.
        /// </summary>
        public Renderer TargetRenderer
        {
            get => m_targetRenderer;
            set => m_targetRenderer = value;
        }

        /// <summary>
        /// Gets the materials available on the target renderer.
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
