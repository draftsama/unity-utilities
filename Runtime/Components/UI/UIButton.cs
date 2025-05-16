using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif
namespace Modules.Utilities
{
    public class UIButton : Button, IPointerDownHandler
    {
        [Serializable]
        public class ButtonPointerDownEvent : UnityEvent { }

        [Serializable]
        public class ButtonPointerUpEvent : UnityEvent { }



        [FormerlySerializedAs("onPointerDown")]
        [SerializeField]
        private ButtonPointerDownEvent m_OnPointerDown = new ButtonPointerDownEvent();

        [FormerlySerializedAs("onPointerUp")]
        [SerializeField]
        private ButtonPointerUpEvent m_OnPointerUp = new ButtonPointerUpEvent();


        public ButtonPointerDownEvent onPointerDown
        {
            get
            {
                return m_OnPointerDown;
            }
            set
            {
                m_OnPointerDown = value;
            }

        }
        public ButtonPointerUpEvent onPointerUp
        {
            get
            {
                return m_OnPointerUp;
            }
            set
            {
                m_OnPointerUp = value;
            }
        }

        public bool IsPointerDown {
            private set;
            get;
        }
       


        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (IsActive() && IsInteractable())
                {
                    UISystemProfilerApi.AddMarker("Button.onPointerDown", this);
                    m_OnPointerDown.Invoke();
                    IsPointerDown = true;
                }
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (IsActive() && IsInteractable())
                {
                    UISystemProfilerApi.AddMarker("Button.onPointerUp", this);
                    m_OnPointerUp.Invoke();
                    IsPointerDown = false;
                    
                }

            }
        }

    }
}

#if UNITY_EDITOR
namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(UIButton), true)]
    [CanEditMultipleObjects]
    public class UIButtonEditor : ButtonEditor
    {
        SerializedProperty m_OnPointerDown;
        SerializedProperty m_OnPointerUp;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_OnPointerDown = serializedObject.FindProperty("m_OnPointerDown");
            m_OnPointerUp = serializedObject.FindProperty("m_OnPointerUp");
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(m_OnPointerDown);
             EditorGUILayout.PropertyField(m_OnPointerUp);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

