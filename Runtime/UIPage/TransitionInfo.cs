using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    [Serializable]
    public class TransitionInfo
    {
        public enum TransitionType
        {
            Fade,
            CrossFade,
        }
        [SerializeField] public int m_Duration = 500;
        [SerializeField] public TransitionType m_Type;
        [SerializeField] public Color m_FadeColor = Color.black;
    }

}

#if UNITY_EDITOR
namespace Modules.Utilities.Editor
{

    [CustomPropertyDrawer(typeof(TransitionInfo))]
    public class TransitionInfoEditor : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var duration = property.FindPropertyRelative(nameof(TransitionInfo.m_Duration));
            var type = property.FindPropertyRelative(nameof(TransitionInfo.m_Type));
            var fadeColor = property.FindPropertyRelative(nameof(TransitionInfo.m_FadeColor));

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var currentY = position.y;

            // Create a foldout toggle group
            property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, currentY, position.width, lineHeight),
            property.isExpanded,
            label
            );
            currentY += lineHeight;

            if (property.isExpanded)
            {
            EditorGUI.indentLevel++;

            // Draw Duration
            var durationRect = new Rect(position.x, currentY, position.width, lineHeight);
            EditorGUI.PropertyField(durationRect, duration);
            currentY += lineHeight;

            // Draw Type
            var typeRect = new Rect(position.x, currentY, position.width, lineHeight);
            EditorGUI.PropertyField(typeRect, type);
            currentY += lineHeight;

            // Conditionally draw FadeColor if Type is Fade
            if ((TransitionInfo.TransitionType)type.enumValueIndex == TransitionInfo.TransitionType.Fade)
            {
                var fadeColorRect = new Rect(position.x, currentY, position.width, lineHeight);
                EditorGUI.PropertyField(fadeColorRect, fadeColor);
                currentY += lineHeight;
            }

            EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
            return EditorGUIUtility.singleLineHeight;
            }

            var height = EditorGUIUtility.singleLineHeight * 3; // Duration and Type
            var type = property.FindPropertyRelative(nameof(TransitionInfo.m_Type));

            // Add extra height for FadeColor if Type is Fade
            if ((TransitionInfo.TransitionType)type.enumValueIndex == TransitionInfo.TransitionType.Fade)
            {
            height += EditorGUIUtility.singleLineHeight;
            }

            return height;
        }
    }
}

#endif


