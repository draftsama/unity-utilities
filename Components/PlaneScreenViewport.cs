using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

#if UNITY_EDITOR
using UnityEditor;

#endif
[ExecuteInEditMode]

public class PlaneScreenViewport : MonoBehaviour
{
    
    public enum ControlType
    {
        None, WidthControlHeight, HeightControlWidth
    }
    [SerializeField] public Camera m_Camera;

    [SerializeField] private float m_Distance = 1.0f;
    [SerializeField] private float m_AdditionalScalePer = 0f;
    [SerializeField][HideInInspector] private ControlType m_ControlType = ControlType.None;
    [SerializeField][HideInInspector]private float m_AspectRatio = -1f;
    [SerializeField] private bool m_UpdateAlways = true;

    //TODO: config camera settings at this component

    private Transform _Tr;
    
    



    void Start()
    {
        if (m_UpdateAlways)
        {
            if (Application.isPlaying)
                UniTaskAsyncEnumerable.EveryUpdate().Subscribe(_ =>
                {
                    UpdatePlaneScreen();
                }, cancellationToken: this.GetCancellationTokenOnDestroy());

        }
        else
        {
            UpdatePlaneScreen();
        }
    }

    private void OnValidate()
    {
        UpdatePlaneScreen();
    }

    public void UpdatePlaneScreen()
    {
        if (m_Camera == null)
            return;

        _Tr = transform;
        if(m_AspectRatio < 0)
            m_AspectRatio = m_Camera.aspect;
        
        
        var isPerspectiveMode = m_Camera.orthographic == false;
        var size = 0f;
        if (isPerspectiveMode)
        {//Calculate the size of the screen plane based on the camera's field of view and the distance to the screen plane
            size = 2.0f * m_Distance * Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
        else
        {
            size = m_Camera.orthographicSize * 2.0f;
        }



        var width = size * m_Camera.aspect;
        var height = size;
      
            
        if (m_ControlType == ControlType.WidthControlHeight)
        {
            
            height = width * (1f/ m_AspectRatio);
        }
        else if (m_ControlType == ControlType.HeightControlWidth)
        {
            width = height * m_AspectRatio;
        }
        

        // Debug.Log($"width:{width} height:{height}");

        if (m_AdditionalScalePer < 0) m_AdditionalScalePer = 0;
        width += (width * m_AdditionalScalePer) / 100f;
        height += (height * m_AdditionalScalePer) / 100f;
        
        

        // Set the scale of the screen plane to match the calculated size and aspect ratio
        _Tr.localScale = new Vector3(width, height, 1.0f);


        _Tr.position = m_Camera.transform.position + m_Camera.transform.forward * m_Distance;

        //look at camera reverse
        _Tr.LookAt(_Tr.position + m_Camera.transform.rotation * Vector3.forward,
                m_Camera.transform.rotation * Vector3.up);


    }

   
    public bool SetAspectRatio(float _aspectRatio)
    {
        if (_aspectRatio < 0)
            return false;
        
        m_AspectRatio = _aspectRatio;
        return true;
    }

}

#if UNITY_EDITOR

[CustomEditor(typeof(PlaneScreenViewport))]
public class PlaneScreenViewportEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        var script = (PlaneScreenViewport)target;
        serializedObject.Update();
        var controlType = serializedObject.FindProperty("m_ControlType");
        var ratio = serializedObject.FindProperty("m_AspectRatio");
        
        EditorGUILayout.PropertyField(controlType);
        if(controlType.enumValueIndex != 0)
        {
            EditorGUILayout.PropertyField(ratio);

            if (script.m_Camera && GUILayout.Button("Use Camera Aspect Ratio"))
            {
                script.SetAspectRatio(script.m_Camera.aspect);
                script.UpdatePlaneScreen();
            }
           
        }

        serializedObject.ApplyModifiedProperties();
        
        

        //if camera is null show warning
        if (script.m_Camera == null)
        {
            EditorGUILayout.HelpBox("Camera is null", MessageType.Warning);
           
        }
        else if (GUILayout.Button("Update"))
        {
            script.UpdatePlaneScreen();
        }






    }
}

#endif

