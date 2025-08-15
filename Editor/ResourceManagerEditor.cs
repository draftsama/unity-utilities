using UnityEngine;
using UnityEditor;
using System.IO;

namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(ResourceManager))]
    public class ResourceManagerEditor : UnityEditor.Editor
    {


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var instance = (ResourceManager)target;

            var resourceSettingAssets = serializedObject.FindProperty("m_ResourceSettingAssets");
            var dontDestroyOnLoad = serializedObject.FindProperty("m_DontDestroyOnLoad");
            var resourceResponseList = serializedObject.FindProperty("m_ResourceResponseList");



            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.PropertyField(resourceSettingAssets, true);
            EditorGUILayout.BeginHorizontal();
            if (resourceSettingAssets.objectReferenceValue == null)
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("New"))
                {

                    string resourcesFolderPath = "Assets/Resources";
                    if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
                    {
                        AssetDatabase.CreateFolder("Assets", "Resources");
                    }
                    ResourceSettingAssets asset = ScriptableObject.CreateInstance<ResourceSettingAssets>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(resourcesFolderPath + "/NewResourceSetting.asset");

                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.SaveAssets();
                    resourceSettingAssets.objectReferenceValue = asset;
                }
            }
            else
            {
                ResourceSettingAssets settings = (ResourceSettingAssets)resourceSettingAssets.objectReferenceValue;
                string folderPath = settings.GetExternalResourcesFolderPath();

                //text
                EditorGUILayout.LabelField(folderPath);

                if (GUILayout.Button("Open Folder"))
                {

                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        if (!System.IO.Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }
                        EditorUtility.RevealInFinder(folderPath);
                    }
                    else
                    {
                        Debug.Log("External resources folder path is not set or does not exist.");
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.PropertyField(dontDestroyOnLoad);
            EditorGUILayout.PropertyField(resourceResponseList, true);

            serializedObject.ApplyModifiedProperties();
        }
    }

}
