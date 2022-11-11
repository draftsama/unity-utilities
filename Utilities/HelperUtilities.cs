using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Modules.Utilities
{

    public static class HelperUtilities
    {


        public static bool BoxCollider2DInCameraView(Vector3 _position, BoxCollider2D _boxCollider, Camera _camera)
        {
            //work with Orthographic camera only
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning($"Camera is null");
                return false;
            }
            var halfHeight = _camera.orthographicSize;
            var screenWidth = (halfHeight * 2f) * _camera.aspect;
            var halfWidth = screenWidth / 2f;

            var objectOffset = _boxCollider.offset + _boxCollider.size / 2f;

            var right = halfWidth + objectOffset.x;
            var top = halfHeight + objectOffset.y;

            return _position.x <= right && _position.x >= -right && _position.y <= top && _position.y >= -top;
        }

        public static bool PositionInCameraView(Vector3 _position, Camera _camera, out Vector2 _screenPosition)
        {
            _screenPosition = Vector2.zero;
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning($"No Camera");
                return false;
            }

            var point = Camera.main.WorldToViewportPoint(_position);

            _screenPosition.x = point.x * Screen.width;
            _screenPosition.y = point.y * Screen.width;

            return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        public static float VectorToDegree(Vector2 _vector)
        {
            return Mathf.Atan2(_vector.y, _vector.x) * Mathf.Rad2Deg;
        }
        public static Vector2 DegreeToVector(float _degree)
        {
            //degree to radian
            float radians = _degree * (Mathf.PI / 180f);
            Vector2 degreeVector = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            if (degreeVector.x > degreeVector.y)
            {
                var ratio = 1f / degreeVector.x;
                return degreeVector * ratio;
            }
            else
            {
                var ratio = 1f / degreeVector.y;
                return degreeVector * ratio;
            }

        }



    }
}
