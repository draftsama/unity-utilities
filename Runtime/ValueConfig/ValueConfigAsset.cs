using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    public class ValueConfigAsset : ScriptableObject
    {

        [SerializeField] public ValueCollection m_ValueCollection;
        [SerializeField] public bool m_NeedUpdate;


    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ValueConfigAsset))]
    public class ValueConfigAssetEditor : Editor
    {


        SerializedProperty _NeedUpdateProperty;
        SerializedProperty _ValueCollectionProperty;
        ValueConfigAsset _Instance;
        private void OnEnable()
        {

            _Instance = target as ValueConfigAsset;

            _NeedUpdateProperty = serializedObject.FindProperty("m_NeedUpdate");
            _ValueCollectionProperty = serializedObject.FindProperty("m_ValueCollection");
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();


            var items = _ValueCollectionProperty.FindPropertyRelative("m_Items");
            var max = items.arraySize;
            List<string> keyList = new List<string>();
            for (int i = 0; i < max; i++)
            {
                var value = items.GetArrayElementAtIndex(i);
                var key = value.FindPropertyRelative("key");
                if (keyList.Contains(key.stringValue))
                {
                    EditorGUILayout.HelpBox($"Index:{i} key:{key.stringValue} - This key is duplicate", MessageType.Error);
                }

                keyList.Add(key.stringValue);

            }



            if (GUI.changed)
            {
                _NeedUpdateProperty.boolValue = true;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }
    }

#endif



}
