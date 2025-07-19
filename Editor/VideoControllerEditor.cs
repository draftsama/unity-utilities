using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace Modules.Utilities.Editor
{

    [CustomEditor(typeof(VideoController))]
    public class VideoControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _RawImage;
        private SerializedProperty _CanvasGroup;
        private SerializedProperty _PlayWithParentShow;
        private SerializedProperty _ParentCanvasGroup;
        private SerializedProperty _CanvasGroupThreshold;

        private SerializedProperty _MeshFilter;
        private SerializedProperty _MeshRenderer;
        private SerializedProperty _Material;


        private VideoController instance;

        public void OnEnable()
        {
            instance = (VideoController)target;
            if (!Application.isPlaying) instance.Init();
            serializedObject.Update();
            _RawImage = serializedObject.FindProperty("_RawImage");
            _CanvasGroup = serializedObject.FindProperty("_CanvasGroup");
            _PlayWithParentShow = serializedObject.FindProperty("_PlayWithParentShow");
            _ParentCanvasGroup = serializedObject.FindProperty("_ParentCanvasGroup");
            _CanvasGroupThreshold = serializedObject.FindProperty("_CanvasGroupThreshold");

            _MeshFilter = serializedObject.FindProperty("_MeshFilter");
            _MeshRenderer = serializedObject.FindProperty("_MeshRenderer");
            _Material = serializedObject.FindProperty("_Material");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        // private void DrawObjectProperty(SerializedProperty _property, Type _componentType)
        // {
        //     EditorGUILayout.BeginHorizontal();
        //     GUI.enabled = false;
        //     EditorGUILayout.PropertyField(_property);
        //     GUI.enabled = true;
        //     GUI.color = Color.green;
        //     if (_property.objectReferenceValue == null && GUILayout.Button("Add"))
        //     {
        //         var componentObject = instance.gameObject.AddComponent(_componentType);
        //         _property.objectReferenceValue = componentObject;
        //     }

        //     GUI.color = Color.white;

        //     EditorGUILayout.EndHorizontal();
        // }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            base.OnInspectorGUI();


            var outputType = serializedObject.FindProperty("m_OutputType");
            var startMode = serializedObject.FindProperty("m_StartMode");

            if (outputType.enumValueIndex == (int)VideoOutputType.Renderer)
            {


                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _MeshFilter, typeof(MeshFilter));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _MeshRenderer, typeof(MeshRenderer));
                _PlayWithParentShow.boolValue = false;
                EditorGUILayout.PropertyField(_Material);
            }
            else if (outputType.enumValueIndex == (int)VideoOutputType.RawImage)
            {
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _RawImage, typeof(RawImage));
                EditorGUIHelper.DrawComponentProperty(instance.gameObject, _CanvasGroup, typeof(CanvasGroup));

                if (startMode.enumValueIndex == (int)VideoStartMode.AutoPlay)
                {

                    EditorGUILayout.PropertyField(_PlayWithParentShow);
                    if (_PlayWithParentShow.boolValue)
                    {
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                            _ParentCanvasGroup.objectReferenceValue = instance.transform.parent.GetComponent<CanvasGroup>();

                        EditorGUILayout.PropertyField(_ParentCanvasGroup);
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                        {
                            //helpbox
                            EditorGUILayout.HelpBox("Parent CanvasGroup is null, please assign it.", MessageType.Error);
                        }

                        EditorGUILayout.Slider(_CanvasGroupThreshold, 0.1f, 1f);
                    }
                }
            }

            if (GUI.changed)
            {
                instance.Init();
                EditorUtility.SetDirty(instance);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

