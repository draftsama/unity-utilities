using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
[ExecuteInEditMode]
public class WorldUIPosition : MonoBehaviour
{
    private Canvas _Canvas;
    private RectTransform _RectTransform;

    private void Awake()
    {
        SetupUI();
    }

    private void OnValidate()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        if (_Canvas == null) _Canvas = GetComponent<Canvas>();
        if (_RectTransform == null) _RectTransform = transform as RectTransform;

        if (_Canvas.worldCamera != null)
        {
            var radians = (_Canvas.worldCamera.fieldOfView / 2f) * Mathf.Deg2Rad;


            var x = Mathf.Cos(radians);
            var y = Mathf.Sin(radians);
            var op = (_RectTransform.rect.height / 2f) / y;
            var distance = op * x;


            _RectTransform.position = _Canvas.worldCamera.transform.position + (Vector3.forward * distance);
        }
        else
        {
            Debug.LogWarning("World Camera is Null");
        }
    }

}
