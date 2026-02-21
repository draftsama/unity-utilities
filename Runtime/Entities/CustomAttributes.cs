using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Modules.Utilities
{


    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class ReadOnlyFieldAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class HelpBoxAttribute : PropertyAttribute
    {      
          public string Message { get; private set; }
          public int MessageType { get; private set; }

        public HelpBoxAttribute(string message, int messageType = 0)
        {
            Message = message;
            MessageType = messageType;
        }
       
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ButtonAttribute : Attribute
    {
        public string ButtonText { get; private set; }

        public ButtonAttribute()
        {
            ButtonText = null; // Will use method name
        }

        public ButtonAttribute(string buttonText)
        {
            ButtonText = buttonText;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class DropdownFieldAttribute : PropertyAttribute
    {
        public Dictionary<string, string> m_Options;


        public DropdownFieldAttribute(Type type)
        {
            m_Options = GetStringConstants(type);

        }

        private Dictionary<string, string> GetStringConstants(Type type)
        {
            // Get all the fields in the class
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            Dictionary<string, string> constants = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                // Check if the field is a string constant
                if (field.FieldType == typeof(string) && field.IsLiteral && !field.IsInitOnly)
                {
                    constants.Add(field.Name, (string)field.GetValue(null));
                }
            }

            return constants;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }
        public bool UseInspectorLimits { get; private set; }
        public string MinLimitFieldName { get; private set; }
        public string MaxLimitFieldName { get; private set; }

        // Constructor 1: Fixed limits (original behavior)
        public MinMaxSliderAttribute(float minValue, float maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            UseInspectorLimits = false;
        }

        // Constructor 2: Use inspector fields for limits
        public MinMaxSliderAttribute(string minLimitFieldName, string maxLimitFieldName)
        {
            MinLimitFieldName = minLimitFieldName;
            MaxLimitFieldName = maxLimitFieldName;
            UseInspectorLimits = true;
            MinValue = 0f; // Default fallback
            MaxValue = 100f; // Default fallback
        }

        // Constructor 3: Mixed - use inspector fields with fallback values
        public MinMaxSliderAttribute(string minLimitFieldName, string maxLimitFieldName, float fallbackMin, float fallbackMax)
        {
            MinLimitFieldName = minLimitFieldName;
            MaxLimitFieldName = maxLimitFieldName;
            UseInspectorLimits = true;
            MinValue = fallbackMin;
            MaxValue = fallbackMax;
        }
    }



}

#if UNITY_EDITOR
namespace Modules.Utilities.Editor
{
    using UnityEditor;

    /// <summary>
    /// Utility class for drawing custom attributes in custom editors
    /// </summary>
    public static class CustomAttributeDrawer
    {
        /// <summary>
        /// Draw buttons for all methods with [Button] attribute
        /// Call this in your custom editor's OnInspectorGUI()
        /// </summary>
        public static void DrawButtonMethods(UnityEngine.Object target)
        {
            var targetType = target.GetType();
            var methods = targetType.GetMethods(BindingFlags.Instance | 
                                                BindingFlags.Static | 
                                                BindingFlags.Public | 
                                                BindingFlags.NonPublic);

            var hasButtons = false;
            foreach (var method in methods)
            {
                var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                if (buttonAttribute != null)
                {
                    if (!hasButtons)
                    {
                        EditorGUILayout.Space(5);
                        hasButtons = true;
                    }

                    var buttonText = string.IsNullOrEmpty(buttonAttribute.ButtonText) 
                        ? ObjectNames.NicifyVariableName(method.Name) 
                        : buttonAttribute.ButtonText;

                    if (GUILayout.Button(buttonText))
                    {
                        method.Invoke(target, null);
                    }
                }
            }
        }
    }
}
#endif
