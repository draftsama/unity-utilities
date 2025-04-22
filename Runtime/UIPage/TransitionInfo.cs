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
            Slide,
        }



        [SerializeField] public int m_Duration = 500;
        [SerializeField] public TransitionType m_Type;
        [SerializeField] public Color m_FadeColor = Color.black;

        [SerializeField] public Vector2 m_StartPosition;
        [SerializeField] public Vector2 m_EndPosition;
        
        [SerializeField] public Easing.Ease m_Ease = Easing.Ease.EaseInOutQuad;
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
            var startPos = property.FindPropertyRelative(nameof(TransitionInfo.m_StartPosition));
            var endPos = property.FindPropertyRelative(nameof(TransitionInfo.m_EndPosition));
            var ease = property.FindPropertyRelative(nameof(TransitionInfo.m_Ease));

            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
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
                else if ((TransitionInfo.TransitionType)type.enumValueIndex == TransitionInfo.TransitionType.Slide)
                {
                    // Draw Start Position
                    var startPosRect = new Rect(position.x, currentY, position.width, lineHeight);
                    EditorGUI.PropertyField(startPosRect, startPos);

                    var endPosRect = new Rect(position.x, currentY + lineHeight, position.width, lineHeight);
                    EditorGUI.PropertyField(endPosRect, endPos);

                    var easeRect = new Rect(position.x, currentY + lineHeight * 2, position.width, lineHeight);
                    EditorGUI.PropertyField(easeRect, ease);


                    
                    
                    currentY += lineHeight * 3;
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
                height += EditorGUIUtility.singleLineHeight + 2;
            }
            else if ((TransitionInfo.TransitionType)type.enumValueIndex == TransitionInfo.TransitionType.Slide)
            {
                // Add extra height for SlideDirection
                height += EditorGUIUtility.singleLineHeight + 2;
                height += EditorGUIUtility.singleLineHeight + 2;
                height += EditorGUIUtility.singleLineHeight + 2;
            }

            return height;
        }
    }
}

#endif


