using UnityEngine;

namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Represents a material replacement mapping.
    /// </summary>
    [System.Serializable]
    public struct MateriluneMaterialSwapEntry
    {
        [SerializeField] private Material m_from;
        [SerializeField] private Material m_to;

        /// <summary>
        /// Gets or sets the material to replace.
        /// </summary>
        public Material From
        {
            get => m_from;
            set => m_from = value;
        }

        /// <summary>
        /// Gets or sets the replacement material.
        /// </summary>
        public Material To
        {
            get => m_to;
            set => m_to = value;
        }

        /// <summary>
        /// Initializes a material replacement mapping.
        /// </summary>
        /// <param name="from">The material to replace.</param>
        /// <param name="to">The replacement material.</param>
        public MateriluneMaterialSwapEntry(Material from, Material to)
        {
            m_from = from;
            m_to = to;
        }
    }
}
