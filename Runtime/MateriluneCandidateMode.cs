namespace com.amari_noa.materilune.runtime
{
    /// <summary>
    /// Defines how material replacement candidates are discovered.
    /// </summary>
    public enum MateriluneCandidateMode
    {
        /// <summary>
        /// Does not provide material candidates.
        /// </summary>
        None,

        /// <summary>
        /// Searches for materials in the current material's directory.
        /// </summary>
        SameDirectory,

        /// <summary>
        /// Searches sibling directories for their closest material matches.
        /// </summary>
        SiblingDirectory,
    }
}
