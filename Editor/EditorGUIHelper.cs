using System;
using UnityEditor;
using UnityEngine;

public class EditorGUIHelper
{
    public static void DrawComponentProperty(GameObject instance, SerializedProperty _property, Type _componentType)
    {
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = false;
        EditorGUILayout.PropertyField(_property);
        GUI.enabled = true;
        GUI.color = Color.green;
        if (_property.objectReferenceValue == null)
        {
            //try to get
            var component = instance.GetComponent(_componentType);

            //if not found, add component
            if (component == null && GUILayout.Button("Add"))
            {
                component = instance.AddComponent(_componentType);
            }

            _property.objectReferenceValue = component;
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
