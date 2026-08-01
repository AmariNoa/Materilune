using com.amari_noa.unity_editor_localization_core.editor;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Registers the Materilune localization source when the editor loads.
    /// </summary>
    [InitializeOnLoad]
    internal static class MateriluneLocalizationSourceRegistration
    {
        static MateriluneLocalizationSourceRegistration()
        {
            var localizationFolderGuid = MateriluneConstants.LocalizationFolderGuid;
            if (string.IsNullOrWhiteSpace(localizationFolderGuid))
            {
                Debug.LogWarning("[Materilune] Localization folder guid is empty.");
                return;
            }

            var localizationFolderPath = AssetDatabase.GUIDToAssetPath(localizationFolderGuid);
            if (string.IsNullOrWhiteSpace(localizationFolderPath))
            {
                Debug.LogWarning($"[Materilune] Localization folder guid could not be resolved. guid={localizationFolderGuid}");
                return;
            }

            EditorLocalization.Service.RegisterSource(new EditorLocalizationSourceDefinition
            {
                SourceId = MateriluneConstants.LocalizationSourceId,
                DisplayName = MateriluneConstants.LocalizationDisplayName,
                LocalizationFolderGuid = MateriluneConstants.LocalizationFolderGuid,
                DefaultLanguageCode = MateriluneConstants.LocalizationDefaultLanguageCode,
                BaseLanguageCode = MateriluneConstants.LocalizationDefaultLanguageCode
            });
        }
    }
}
