using UnityEditor;
using UnityEngine;

namespace EditorExtension.Editor
{
    [CustomEditor(typeof(CanvasGroup), true)]
    [CanEditMultipleObjects]
    public class CanvasGroupExtensionEditor : UnityEditor.Editor
    {


        private void OnEnable()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();


            var alpha = serializedObject.FindProperty("m_Alpha");

            EditorGUILayout.Slider(alpha, 0, 1);

            var interactable = serializedObject.FindProperty("m_Interactable");
            EditorGUILayout.PropertyField(interactable);

            var blocksRaycasts = serializedObject.FindProperty("m_BlocksRaycasts");
            EditorGUILayout.PropertyField(blocksRaycasts);


            GUI.color = Color.green;
            if (alpha.floatValue == 0f && GUILayout.Button("Show"))
            {
                alpha.floatValue = 1f;   
                blocksRaycasts.boolValue = true;
                interactable.boolValue = true;
            }

            GUI.color = Color.white;
            if (alpha.floatValue >0f && GUILayout.Button("Hide"))
            {
                alpha.floatValue = 0f;
                blocksRaycasts.boolValue = false;
               interactable.boolValue = false;
            }
            if(GUI.changed){
                EditorUtility.SetDirty(target);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
