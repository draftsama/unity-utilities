using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Modules.Utilities
{
    public class ConfigWindowEditor : EditorWindow
    {

        private static ValueConfigAsset _Asset;

        private static SerializedObject serializedObject;

        private static SerializedProperty m_ValueCollectionProperty;
        private static SerializedProperty m_NeedUpdateProperty;


        [MenuItem("Utilities/Config Setiing")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow.GetWindow(typeof(ConfigWindowEditor), true, "Config Setting", true);
            LoadData();
            scrollPos = Vector2.zero;
        }


        void OnFocus()
        {

            LoadData();

        }
        private static void LoadData()
        {

            if (_Asset == null)
            {
                _Asset = AssetDatabase.LoadAssetAtPath<ValueConfigAsset>("Assets/Resources/Config/ValueConfig.asset");
                if (_Asset != null)
                {
                    serializedObject = new UnityEditor.SerializedObject(_Asset);
                    m_ValueCollectionProperty = serializedObject.FindProperty("m_ValueCollection");
                    m_NeedUpdateProperty = serializedObject.FindProperty("m_NeedUpdate");
                }

            }
        }
        static Vector2 scrollPos = Vector2.zero;

        static float bottomHeight = 100;
        private const float _DEFAULT_BOTTOM_HEIGHT = 100;
        void OnGUI()
        {



            EditorGUILayout.BeginHorizontal();
            _Asset = EditorGUILayout.ObjectField("Asset", _Asset, typeof(ValueConfigAsset), true) as ValueConfigAsset;

            if (_Asset == null && GUILayout.Button("New"))
            {
                CreateConfigAsset();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (_Asset != null)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(position.height - bottomHeight));
                bottomHeight = _DEFAULT_BOTTOM_HEIGHT;

                serializedObject.UpdateIfRequiredOrScript();

                EditorGUILayout.PropertyField(m_ValueCollectionProperty, true);

                EditorGUILayout.EndScrollView();

                var items = m_ValueCollectionProperty.FindPropertyRelative("m_Items");
                var max = items.arraySize;
                List<string> keyList = new List<string>();
                for (int i = 0; i < max; i++)
                {
                    var value = items.GetArrayElementAtIndex(i);
                    var key = value.FindPropertyRelative("key");
                    if (keyList.Contains(key.stringValue))
                    {
                        EditorGUILayout.HelpBox($"Index:{i} key:{key.stringValue} - This key is duplicate", MessageType.Error);

                        bottomHeight += 34;
                    }

                    keyList.Add(key.stringValue);

                }
            }


            if (GUI.changed)
            {

                if (_Asset != null)
                {

                    if (serializedObject != null)
                    {
                        m_NeedUpdateProperty.boolValue = true;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_Asset);
                    }

                }

            }

            if (_Asset != null)
            {
                if (GUILayout.Button("Froce Update"))
                {
                    m_NeedUpdateProperty.boolValue = false;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_Asset);
                    ValueConfig.SaveJsonFile(_Asset.m_ValueCollection);

                }
                if (_Asset.m_NeedUpdate && GUILayout.Button("Update"))
                {
                    m_NeedUpdateProperty.boolValue = false;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_Asset);
                    ValueConfig.SaveJsonFile(_Asset.m_ValueCollection);

                }

            }




        }

        public void CreateConfigAsset()
        {

            var resourcesFolder = AssetDatabase.GetSubFolders("Assets").Where(x => x.Contains("Assets/Resources")).FirstOrDefault();


            if (resourcesFolder == null)
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var configFolder = AssetDatabase.GetSubFolders("Assets/Resources").Where(x => x.Contains("Assets/Resources/Config")).FirstOrDefault();

            if (configFolder == null)
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            }


            var path = Path.Combine(configFolder, "ValueConfig.asset");

            AssetDatabase.DeleteAsset(path);

            _Asset = ScriptableObject.CreateInstance<ValueConfigAsset>();

            _Asset.m_NeedUpdate = true;
            AssetDatabase.CreateAsset(_Asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
    }
}