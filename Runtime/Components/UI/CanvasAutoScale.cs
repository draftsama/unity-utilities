using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasAutoScale : MonoBehaviour
    {
        private CanvasScaler m_CanvasScaler;
        void OnEnable()
        {
#if UNITY_EDITOR
            Application.onBeforeRender += UpdateCanvasScaler;
#endif

        }
        private void Start()
        {
            m_CanvasScaler = GetComponent<CanvasScaler>();
            UpdateCanvasScaler();


        }


        void OnDisable()
        {
#if UNITY_EDITOR

            Application.onBeforeRender -= UpdateCanvasScaler;
#endif
        }


        private void UpdateCanvasScaler()
        {
            if (m_CanvasScaler == null)
                m_CanvasScaler = GetComponent<CanvasScaler>();

            if (m_CanvasScaler == null)
                return;


            Vector2 referenceResolution = new Vector2(Screen.width, Screen.height);
            m_CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            m_CanvasScaler.referenceResolution = referenceResolution;

        }


    }
}