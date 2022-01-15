using UnityEditor;
using UnityEngine;

namespace EditorExtension.Editor
{
    [CustomEditor(typeof(CanvasGroup), true)]
    [CanEditMultipleObjects]
    public class CanvasGroupExtensionEditor : UnityEditor.Editor
    {
        private CanvasGroup _CanvasGroup;

        private bool _IsShow = true;

        private void OnEnable()
        {
            _CanvasGroup = target as CanvasGroup;
            if (_CanvasGroup != null) _IsShow = _CanvasGroup.alpha > 0.9999f || _CanvasGroup.interactable;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            serializedObject.Update();
            GUI.color = Color.green;
            if (!_IsShow && GUILayout.Button("Show"))
            {
                _IsShow = true;
                _CanvasGroup.alpha = 1;
                _CanvasGroup.blocksRaycasts = true;
                _CanvasGroup.interactable = true;
            }

            GUI.color = Color.white;
            if (_IsShow && GUILayout.Button("Hide"))
            {
                _IsShow = false;
                _CanvasGroup.alpha = 0;
                _CanvasGroup.blocksRaycasts = false;
                _CanvasGroup.interactable = false;
            }
            if(GUI.changed){
                EditorUtility.SetDirty(target);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
