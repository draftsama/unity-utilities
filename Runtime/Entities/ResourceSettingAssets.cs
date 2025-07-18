using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{

    public class ResourceSettingAssets : ScriptableObject
    {
        public ResourceStoreType m_ResourceStoreType;
        public string m_ExternalResourcesFolderName = "";
    }


#if UNITY_EDITOR
    public class ResourceSettingAssetsEditor
    {
        [MenuItem("Assets/Create/[Draft Utility]/Create ResourceSettingAssets", priority = 0)]
        public static void CreateResourceSettingAsset()
        {
            // Ensure the "Resources" folder exists
            string resourcesFolderPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            //find ResourceSettingAssets in Resources folder if it exists, if it does, select it
            ResourceSettingAssets[] resourceSettingAssets = Resources.LoadAll<ResourceSettingAssets>("");
            if (resourceSettingAssets.Length > 0)
            {
                Debug.Log("ResourceSettingAssets already exists in Resources folder, selecting it");

                
                Selection.activeObject = resourceSettingAssets[0];

                // show the object in the project view
                EditorGUIUtility.PingObject(resourceSettingAssets[0]);

                
                return;
            }

            

            // Create the asset in the "Resources" folder
            ResourceSettingAssets asset = ScriptableObject.CreateInstance<ResourceSettingAssets>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(resourcesFolderPath + "/NewResourceSetting.asset");

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
#endif

}
