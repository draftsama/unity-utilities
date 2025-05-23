using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Modules.Utilities;
using UnityEditor;
using UnityEngine;
namespace Modules.Utilities.Editor
{


    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    public class ReadOnlyFieldAttributeDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }

    }

    [CustomEditor(typeof(MonoBehaviour), true)]
    public class ButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // Get the target object (the script instance)
            MonoBehaviour monoBehaviour = (MonoBehaviour)target;
            MethodInfo[] methods = monoBehaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                // Check if the method has the [Button] attribute
                if (method.GetCustomAttribute<ButtonAttribute>() != null)
                {
                    if (GUILayout.Button(method.Name))
                    {
                        // Call the method
                        method.Invoke(monoBehaviour, null);
                    }
                }
            }
        }
    }

    [CustomPropertyDrawer(typeof(DropdownFieldAttribute))]
    public class DropdownFieldAttributeDrawer : PropertyDrawer
    {

        int selectedIndex = 0;
       

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                // Create a dropdown with the predefined strings
                // Debug.Log("m_Options: " + m_Options.Length);

                Dictionary<string,string> fieldOptions = ((DropdownFieldAttribute)attribute).m_Options;

                string currentValue = property.stringValue;

                // Find the index of the current value in the options
                foreach (var kvp in fieldOptions)
                {
                    if (kvp.Value == currentValue)
                    {
                        selectedIndex = Array.IndexOf(fieldOptions.Keys.ToArray(), kvp.Key);
                        break;
                    }
                }

                // Create a dropdown with the predefined strings
                GUIContent[] options = new GUIContent[fieldOptions.Count];
                int i = 0;
                foreach (var kvp in fieldOptions)
                {
                    options[i] = new GUIContent()
                    {
                        text = kvp.Key,
                        tooltip = $"\"{kvp.Value}\""
                    };
                    i++;
                }
                label.tooltip = $"Value: \"{fieldOptions.Values.ElementAt(selectedIndex)}\"";
                selectedIndex = EditorGUI.Popup(position, label, selectedIndex, options);

                // Set the property value to the selected option
                property.stringValue = fieldOptions.Values.ElementAt(selectedIndex);


            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }


}