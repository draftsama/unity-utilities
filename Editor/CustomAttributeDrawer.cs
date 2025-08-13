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
        private Dictionary<string, object[]> methodParameters = new Dictionary<string, object[]>();

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
                    DrawMethodButton(monoBehaviour, method);
                }
            }
        }

        private void DrawMethodButton(MonoBehaviour monoBehaviour, MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string methodKey = $"{monoBehaviour.GetInstanceID()}_{method.Name}";

            // Get button attribute to check for custom button text
            ButtonAttribute buttonAttr = method.GetCustomAttribute<ButtonAttribute>();
            string buttonText = !string.IsNullOrEmpty(buttonAttr.ButtonText) ? buttonAttr.ButtonText : method.Name;

            // Initialize parameter storage if needed
            if (!methodParameters.ContainsKey(methodKey))
            {
                methodParameters[methodKey] = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    methodParameters[methodKey][i] = GetDefaultValue(parameters[i].ParameterType);
                }
            }

            object[] paramValues = methodParameters[methodKey];

            // Draw parameter fields if method has parameters

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(buttonText, EditorStyles.boldLabel);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                paramValues[i] = DrawParameterField(param.Name, param.ParameterType, paramValues[i]);
            }

            if (GUILayout.Button($"Execute"))
            {
                try
                {
                    method.Invoke(monoBehaviour, paramValues);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error invoking method {method.Name}: {e.Message}");
                }
            }

            EditorGUILayout.EndVertical();

        }

        private object DrawParameterField(string paramName, Type paramType, object currentValue)
        {
            if (paramType == typeof(int))
            {
                return EditorGUILayout.IntField(paramName, currentValue != null ? (int)currentValue : 0);
            }
            else if (paramType == typeof(float))
            {
                return EditorGUILayout.FloatField(paramName, currentValue != null ? (float)currentValue : 0f);
            }
            else if (paramType == typeof(double))
            {
                return EditorGUILayout.DoubleField(paramName, currentValue != null ? (double)currentValue : 0.0);
            }
            else if (paramType == typeof(bool))
            {
                return EditorGUILayout.Toggle(paramName, currentValue != null ? (bool)currentValue : false);
            }
            else if (paramType == typeof(string))
            {
                return EditorGUILayout.TextField(paramName, currentValue != null ? (string)currentValue : "");
            }
            else if (paramType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(paramName, currentValue != null ? (Vector2)currentValue : Vector2.zero);
            }
            else if (paramType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(paramName, currentValue != null ? (Vector3)currentValue : Vector3.zero);
            }
            else if (paramType == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(paramName, currentValue != null ? (Vector4)currentValue : Vector4.zero);
            }
            else if (paramType == typeof(Color))
            {
                return EditorGUILayout.ColorField(paramName, currentValue != null ? (Color)currentValue : Color.white);
            }
            else if (paramType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(paramName, currentValue != null ? (System.Enum)currentValue : (System.Enum)System.Enum.GetValues(paramType).GetValue(0));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(paramType))
            {
                return EditorGUILayout.ObjectField(paramName, (UnityEngine.Object)currentValue, paramType, true);
            }
            else
            {
                EditorGUILayout.LabelField(paramName, $"Unsupported type: {paramType.Name}");
                return currentValue;
            }
        }

        private object GetDefaultValue(Type type)
        {
            if (type == typeof(string))
                return "";
            else if (type.IsValueType)
                return System.Activator.CreateInstance(type);
            else
                return null;
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

                Dictionary<string, string> fieldOptions = ((DropdownFieldAttribute)attribute).m_Options;

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


    [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
    public class HelpBoxAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            HelpBoxAttribute helpBoxAttr = (HelpBoxAttribute)attribute;
            
            // Calculate heights
            float helpBoxHeight = GetHelpBoxHeight(helpBoxAttr.Message);
            float propertyHeight = EditorGUI.GetPropertyHeight(property, label);
            
            // Create rects
            Rect helpBoxRect = new Rect(position.x, position.y, position.width, helpBoxHeight);
            Rect propertyRect = new Rect(position.x, position.y + helpBoxHeight + 2, position.width, propertyHeight);
            
            // Draw help box
            EditorGUI.HelpBox(helpBoxRect, helpBoxAttr.Message, (MessageType)helpBoxAttr.MessageType);
            
            // Draw the property field
            EditorGUI.PropertyField(propertyRect, property, label, true);
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            HelpBoxAttribute helpBoxAttr = (HelpBoxAttribute)attribute;
            float helpBoxHeight = GetHelpBoxHeight(helpBoxAttr.Message);
            float propertyHeight = EditorGUI.GetPropertyHeight(property, label);
            
            return helpBoxHeight + propertyHeight + 2; // +2 for spacing
        }
        
        private float GetHelpBoxHeight(string message)
        {
            // Calculate height needed for the help box based on message length
            GUIStyle helpBoxStyle = GUI.skin.GetStyle("helpbox");
            float width = EditorGUIUtility.currentViewWidth - 28; // Account for inspector padding
            return helpBoxStyle.CalcHeight(new GUIContent(message), width);
        }
    }


}