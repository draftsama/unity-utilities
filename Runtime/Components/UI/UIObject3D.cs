using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using UnityEngine.UIElements;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{


    [RequireComponent(typeof(RawImage))]
    public class UIObject3D : MonoBehaviour, ILayoutController
    {
        [SerializeField] private Transform m_Target;
        [SerializeField] private Camera _Camera;

        [SerializeField] private int m_TextureSize = 256;

        [SerializeField] private int m_AxisX = 0;
        [SerializeField] private int m_AxisY = 0;
        [SerializeField] private float m_Distance = 3f;
        [SerializeField] private float m_LeftDistance = 0f;
        [SerializeField] private float m_Height = 0f;

        [SerializeField] private bool m_LookAt = false;

        [SerializeField] private LayerMask m_LayerMask = -1;

        //light

        [SerializeField] private bool m_EnableLight = false;
        [SerializeField] private Color m_LightColor = Color.white;
        [SerializeField] private float m_LightIntensity = 1f;
        [SerializeField] private Vector3 m_LightAngle = new Vector3(50, -30, 0);

        [SerializeField] private Light m_Light;







        private RawImage _RawImage;
        private RenderTexture _RenderTexture;

        private RectTransform _RectTransform;

        public void SetLayoutHorizontal()
        {
            UpdateRenderTexture();
            UpdateCameraProperties();

        }

        public void SetLayoutVertical()
        {
            UpdateRenderTexture();
            UpdateCameraProperties();

        }

        private void Awake()
        {
            if (_RawImage == null) _RawImage = GetComponent<RawImage>();
            if (_RectTransform != null) _RectTransform = GetComponent<RectTransform>();
        }
        void Start()
        {
            UpdateRenderTexture();
            UpdateCameraProperties();

        }

        public void SetTarget(Transform target, string layerName)
        {
            m_Target = target;
            m_LayerMask = LayerMask.GetMask(layerName);

        }




        public void UpdateRenderTexture()
        {
            if (_RawImage == null) _RawImage = GetComponent<RawImage>();
            if (_RectTransform == null) _RectTransform = GetComponent<RectTransform>();

            if (m_Target == null)
            {

                _RawImage.texture = null;
                return;
            }

            InitCamera();

            var size = CalculateTextureSize();

            if (_RenderTexture == null)
            {
                _RenderTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            }
            else
            {

                _RenderTexture.Release();
                _RenderTexture.width = size.x;
                _RenderTexture.height = size.y;
            }

            _RenderTexture.Create();
            _Camera.Render();

            _Camera.targetTexture = _RenderTexture;
            _RawImage.texture = _RenderTexture;




        }

        void InitCamera()
        {
            if (_Camera == null)
            {
                _Camera = new GameObject("Camera").AddComponent<Camera>();
                _Camera.transform.SetParent(transform);
                _Camera.transform.localPosition = Vector3.zero;
                _Camera.transform.localRotation = Quaternion.identity;
                _Camera.transform.localScale = Vector3.one;
            }
            _Camera.cullingMask = m_LayerMask;
            _Camera.clearFlags = CameraClearFlags.SolidColor;
            _Camera.backgroundColor = Color.clear;

            if (m_EnableLight)
                InitLight();
            else
                if (m_Light != null) m_Light.gameObject.SetActive(false);
        }

        void InitLight()
        {
            if (m_Light == null)
            {
                m_Light = new GameObject("Light").AddComponent<Light>();
                m_Light.transform.SetParent(transform);
                m_Light.transform.localPosition = Vector3.zero;
                m_Light.transform.localScale = Vector3.one;
            }
            m_Light.gameObject.SetActive(true);
            m_Light.color = m_LightColor;
            m_Light.intensity = m_LightIntensity;
            m_Light.transform.localEulerAngles = m_LightAngle;
            m_Light.cullingMask = m_LayerMask;
            m_Light.type = LightType.Directional;

            m_Light.transform.localRotation = Quaternion.Euler(m_LightAngle);

        }


        public void UpdateCameraProperties()
        {
            if (m_Target == null) return;

            InitCamera();

            var targetPos = m_Target.position;

            m_AxisX = Mathf.Clamp(m_AxisX, -90, 90);

            var rotation = Quaternion.Euler(m_AxisX, m_AxisY, 0);
            var position = rotation * new Vector3(m_LeftDistance, m_Height, -m_Distance) + targetPos;
            _Camera.transform.position = position;

            if (m_LookAt)
                _Camera.transform.LookAt(targetPos);
            else
                _Camera.transform.rotation = rotation;


        }


        Vector2Int CalculateTextureSize()
        {
            if (m_TextureSize < 128) m_TextureSize = 128;
            float ratio = _RectTransform.rect.width / _RectTransform.rect.height;
            var width = ratio > 1 ? m_TextureSize : ratio < 1 ? (int)(m_TextureSize * ratio) : m_TextureSize;
            var height = ratio > 1 ? (int)(m_TextureSize / ratio) : ratio < 1 ? m_TextureSize : m_TextureSize;
            return new Vector2Int(width, height);
        }


    }
}
#if UNITY_EDITOR

namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(UIObject3D))]
    public class UIObject3DEditor : UnityEditor.Editor
    {
        SerializedProperty _LayerMaskProp;
        int lastLayerMask = 0;
        private void OnEnable()
        {

            _LayerMaskProp = serializedObject.FindProperty("m_LayerMask");
            lastLayerMask = _LayerMaskProp.intValue;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var instance = target as UIObject3D;

            var targetProp = serializedObject.FindProperty("m_Target");
            var textureSizeProp = serializedObject.FindProperty("m_TextureSize");

            var axisXProp = serializedObject.FindProperty("m_AxisX");
            var axisYProp = serializedObject.FindProperty("m_AxisY");
            var distanceProp = serializedObject.FindProperty("m_Distance");
            var leftDistanceProp = serializedObject.FindProperty("m_LeftDistance");
            var heightProp = serializedObject.FindProperty("m_Height");
            var lookAtProp = serializedObject.FindProperty("m_LookAt");


            var enableLightProp = serializedObject.FindProperty("m_EnableLight");
            var lightColorProp = serializedObject.FindProperty("m_LightColor");
            var lightIntensityProp = serializedObject.FindProperty("m_LightIntensity");
            var lightAngleProp = serializedObject.FindProperty("m_LightAngle");






            EditorGUILayout.PropertyField(targetProp);
            EditorGUILayout.PropertyField(textureSizeProp);

            //camera settings lable
            EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(axisXProp);
            EditorGUILayout.PropertyField(axisYProp);
            EditorGUILayout.PropertyField(distanceProp);
            EditorGUILayout.PropertyField(leftDistanceProp);
            EditorGUILayout.PropertyField(heightProp);
            EditorGUILayout.PropertyField(lookAtProp);

            EditorGUILayout.PropertyField(_LayerMaskProp);

            EditorGUILayout.LabelField("Light Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableLightProp);
            if (enableLightProp.boolValue)
            {
                EditorGUILayout.PropertyField(lightColorProp);
                EditorGUILayout.PropertyField(lightIntensityProp);
                EditorGUILayout.PropertyField(lightAngleProp);
            }


            //when change layer mask then update camera properties
            if (lastLayerMask != _LayerMaskProp.intValue)
            {

                GUI.changed = true;
            }

            //force update
            if (GUILayout.Button("Update"))
            {
                UpdateUI();
            }


            //helpbox show if camera and target is null 
            if (targetProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Require Target Object", MessageType.Error);
            }


            if (GUI.changed)
            {
                UpdateUI();
            }
            void UpdateUI()
            {
                serializedObject.ApplyModifiedProperties();

                instance.UpdateRenderTexture();
                instance.UpdateCameraProperties();
                EditorUtility.SetDirty(instance);
            }

        }
    }
}

#endif
