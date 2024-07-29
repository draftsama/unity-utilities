using System.Collections;
using System.Collections.Generic;
using Modules.Utilities;
using UnityEngine;
using Cysharp.Threading.Tasks.Linq;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{



    [RequireComponent(typeof(UIVideoController))]
    public class UIVideoPlayByCanvas : MonoBehaviour
    {
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private UIVideoController m_UIVideoController;

        [SerializeField] private float m_AlphaThreshold = 0.1f;

        private bool m_IsPlaying = false;
        void Start()
        {

            // m_CanvasGroup alpha greater or equls than 0 will play the video

            UniTaskAsyncEnumerable.EveryUpdate().ForEachAsync(_ =>
            {

                if (m_CanvasGroup.alpha >= m_AlphaThreshold)
                {

                    if (!m_IsPlaying)
                    {
                        m_IsPlaying = true;
                        m_UIVideoController.PlayAsync().Forget();
                    }

                }
                else
                {
                    if (m_IsPlaying)
                    {
                        m_IsPlaying = false;
                        m_UIVideoController.Stop();

                    }
                }
            }).Forget();
        }
    }



#if UNITY_EDITOR

    [CustomEditor(typeof(UIVideoPlayByCanvas))]
    public class UIVideoPlayByCanvasEditor : Editor
    {
        private UIVideoPlayByCanvas m_Target;

        private void OnEnable()
        {
            m_Target = (UIVideoPlayByCanvas)target;


        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            //if m_CanvasGroup is null, will show a warning message
            var canvasGroupSerializedProperty = serializedObject.FindProperty("m_CanvasGroup");
            EditorGUILayout.PropertyField(canvasGroupSerializedProperty);
            var videoControllerProperty = serializedObject.FindProperty("m_UIVideoController");
            EditorGUILayout.PropertyField(videoControllerProperty);
            var alphaThresholdProperty = serializedObject.FindProperty("m_AlphaThreshold");
            //show the alpha threshold slider clamped between 0.1 and 1
            EditorGUILayout.Slider(alphaThresholdProperty, 0.1f, 1f);



            if (videoControllerProperty.objectReferenceValue == null)
            {
                videoControllerProperty.objectReferenceValue = m_Target.GetComponent<UIVideoController>();
            }
            if (canvasGroupSerializedProperty.objectReferenceValue == null)
            {
                canvasGroupSerializedProperty.objectReferenceValue = m_Target.transform.parent.GetComponent<CanvasGroup>();
            }



            if (canvasGroupSerializedProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("CanvasGroup is null", MessageType.Warning);
            }

            if (videoControllerProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("UIVideoController is null", MessageType.Warning);
            }





            serializedObject.ApplyModifiedProperties();

        }
    }

#endif


}