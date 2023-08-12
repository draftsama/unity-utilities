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
        public static Vector2 CalculateViewportSize(Vector3 target, Camera camera = null)
        {
            // If camera is not provided, try getting the main camera
            if (camera == null && !Camera.main.TryGetComponent(out camera))
            {
                Debug.LogWarning("No Camera");
                return Vector2.zero;
            }

            // Calculate the distance once
            float distance = Vector3.Distance(camera.transform.position, target);

            // Calculate viewport points
            Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
            Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1, 1, distance));

            // Calculate and return size
            return (Vector2)(topRight - bottomLeft);

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

        public static int RandomIndexWithoutCurrent(int _currentIndex, int _max)
        {
            var index = UnityEngine.Random.Range(0, _max);

            if (index == _currentIndex) return RandomIndexWithoutCurrent(_currentIndex, _max);
            else return index;
        }

    }
}
