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

    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Vector2)
            {
                MinMaxSliderAttribute minMaxAttr = (MinMaxSliderAttribute)attribute;
                
                Vector2 range = property.vector2Value;
                float minValue = range.x;
                float maxValue = range.y;

                // Get actual min/max limits
                float actualMinLimit = minMaxAttr.MinValue;
                float actualMaxLimit = minMaxAttr.MaxValue;

                if (minMaxAttr.UseInspectorLimits)
                {
                    // Try to get limits from inspector fields
                    SerializedProperty minLimitProp = property.serializedObject.FindProperty(minMaxAttr.MinLimitFieldName);
                    SerializedProperty maxLimitProp = property.serializedObject.FindProperty(minMaxAttr.MaxLimitFieldName);

                    if (minLimitProp != null && minLimitProp.propertyType == SerializedPropertyType.Float)
                    {
                        actualMinLimit = minLimitProp.floatValue;
                    }
                    
                    if (maxLimitProp != null && maxLimitProp.propertyType == SerializedPropertyType.Float)
                    {
                        actualMaxLimit = maxLimitProp.floatValue;
                    }

                    // Ensure min limit is not greater than max limit
                    if (actualMinLimit > actualMaxLimit)
                    {
                        float temp = actualMinLimit;
                        actualMinLimit = actualMaxLimit;
                        actualMaxLimit = temp;
                    }
                }

                // Ensure values are within bounds
                minValue = Mathf.Clamp(minValue, actualMinLimit, actualMaxLimit);
                maxValue = Mathf.Clamp(maxValue, actualMinLimit, actualMaxLimit);

                EditorGUI.BeginProperty(position, label, property);

                // Calculate rects
                Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
                Rect minFieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, 50, position.height);
                Rect sliderRect = new Rect(minFieldRect.xMax + 5, position.y, position.width - EditorGUIUtility.labelWidth - 110, position.height);
                Rect maxFieldRect = new Rect(sliderRect.xMax + 5, position.y, 50, position.height);

                // Update label to show current limits
                string labelText = label.text;
                if (minMaxAttr.UseInspectorLimits)
                {
                    labelText += $" [{actualMinLimit:F1}-{actualMaxLimit:F1}]";
                }
                else
                {
                    labelText += $" [{actualMinLimit:F1}-{actualMaxLimit:F1}]";
                }
                
                // Draw label
                EditorGUI.LabelField(labelRect, new GUIContent(labelText, label.tooltip));

                // Draw min value field
                EditorGUI.BeginChangeCheck();
                minValue = EditorGUI.FloatField(minFieldRect, minValue);
                if (EditorGUI.EndChangeCheck())
                {
                    minValue = Mathf.Clamp(minValue, actualMinLimit, maxValue);
                }

                // Draw max value field
                EditorGUI.BeginChangeCheck();
                maxValue = EditorGUI.FloatField(maxFieldRect, maxValue);
                if (EditorGUI.EndChangeCheck())
                {
                    maxValue = Mathf.Clamp(maxValue, minValue, actualMaxLimit);
                }

                // Draw min-max slider
                EditorGUI.BeginChangeCheck();
                EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, actualMinLimit, actualMaxLimit);
                if (EditorGUI.EndChangeCheck())
                {
                    // Ensure min is not greater than max
                    if (minValue > maxValue)
                    {
                        minValue = maxValue;
                    }
                }

                // Apply values back to property
                property.vector2Value = new Vector2(minValue, maxValue);

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use MinMaxSlider with Vector2.");
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }


}