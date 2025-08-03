using UnityEngine;
using UnityEditor;

namespace Modules.Utilities
{
    /// <summary>
    /// Property drawer for DisplayName attribute
    /// </summary>
    [CustomPropertyDrawer(typeof(DisplayNameAttribute))]
    public class DisplayNamePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var displayNameAttribute = (DisplayNameAttribute)attribute;
            label.text = displayNameAttribute.DisplayName;
            EditorGUI.PropertyField(position, property, label, true);
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
