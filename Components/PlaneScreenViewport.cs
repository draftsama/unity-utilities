using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

[ExecuteInEditMode]
public class PlaneScreenViewport : MonoBehaviour
{
    [SerializeField] private Camera m_Camera;

    [SerializeField] private float m_Distance = 1.0f;
    [SerializeField] private float m_AdditionalScalePer = 0f;

    [SerializeField] private bool m_UpdateAlways = true;

    private Transform _Tr;
    void Start()
    {

        if(m_UpdateAlways){
            UniTaskAsyncEnumerable.EveryUpdate().Subscribe(_ =>
            {
                UpdatePlaneScreen();
            }, cancellationToken: this.GetCancellationTokenOnDestroy());
        }else{
            UpdatePlaneScreen();
        }
    }

    public void UpdatePlaneScreen()
    {
        if (m_Camera == null)
            return;

        _Tr = transform;


        //Calculate the size of the screen plane based on the camera's field of view and the distance to the screen plane
        var size = 2.0f * m_Distance * Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);


        var width = size * m_Camera.aspect;
        var height = size;

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


    



}

