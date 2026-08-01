namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Defines shared Materilune editor constants.
    /// </summary>
    internal static class MateriluneConstants
    {
        /// <summary>
        /// Gets the localization source identifier.
        /// </summary>
        public const string LocalizationSourceId = "com.amari-noa.materilune";

        /// <summary>
        /// Gets the localization source display name.
        /// </summary>
        public const string LocalizationDisplayName = "Materilune";

        /// <summary>
        /// Gets the default localization language code.
        /// </summary>
        public const string LocalizationDefaultLanguageCode = "en-US";

        /// <summary>
        /// Gets the localization folder asset guid.
        /// </summary>
        // Must match the guid in Editor/Localization.meta. Never delete and recreate that folder:
        // a new guid would break translation loading.
        public const string LocalizationFolderGuid = "187ea7b78d4e0d44f94a6e8bf9b1ed48";
    }
}
