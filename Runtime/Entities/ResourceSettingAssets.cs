using UnityEngine;
using System.IO;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{

    public class ResourceSettingAssets : ScriptableObject
    {
        public ResourceStoreType m_ResourceStoreType;
        public string m_ExternalResourcesFolderName = "";

        public string GetExternalResourcesFolderPath()
        {
            if (string.IsNullOrEmpty(m_ExternalResourcesFolderName))
            {
                return "";
            }
            return Path.Combine(System.Environment.CurrentDirectory, m_ExternalResourcesFolderName);
        }


      
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ResourceSettingAssets))]
    public class ResourceSettingAssetsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ResourceSettingAssets settings = (ResourceSettingAssets)target;

            EditorGUI.BeginChangeCheck();

            // Show m_ResourceStoreType
            settings.m_ResourceStoreType = (ResourceStoreType)EditorGUILayout.EnumPopup("Resource Store Type", settings.m_ResourceStoreType);

            // Conditionally show m_ExternalResourcesFolderName
            if (settings.m_ResourceStoreType == ResourceStoreType.ExternalResources)
            {
                settings.m_ExternalResourcesFolderName = EditorGUILayout.TextField("External Resources Folder Name", settings.m_ExternalResourcesFolderName);
                
                // Show the full path as a help box
                if (!string.IsNullOrEmpty(settings.m_ExternalResourcesFolderName))
                {
                    string fullPath = settings.GetExternalResourcesFolderPath();
                    
                    // Check if directory exists
                    if (Directory.Exists(fullPath))
                    {
                        EditorGUILayout.HelpBox($"Full Path: {fullPath}", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Directory does not exist!\nPath: {fullPath}", MessageType.Error);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }
        }

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
