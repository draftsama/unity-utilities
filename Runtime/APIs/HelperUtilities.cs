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

        public static bool BoxCollider2DInScreenPointView(Vector3 _position, BoxCollider2D _boxCollider, Camera _camera, out Vector2 _screenPosition)
        {
            _screenPosition = Vector2.zero;
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning($"No Camera");
                return false;
            }

            var point = _camera.WorldToViewportPoint(_position);

            var minX = _position.x - _boxCollider.size.x / 2f;
            var maxX = _position.x + _boxCollider.size.x / 2f;
            var minY = _position.y - _boxCollider.size.y / 2f;
            var maxY = _position.y + _boxCollider.size.y / 2f;

            var min = _camera.WorldToScreenPoint(new Vector3(minX, minY, 0));
            var max = _camera.WorldToScreenPoint(new Vector3(maxX, maxY, 0));

            _screenPosition.x = point.x * Screen.width;
            _screenPosition.y = point.y * Screen.width;

            return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;


        }



        /// <summary>
        /// Check if position is in camera view
        /// </summary>

        public static bool IsPositionInCameraView(Vector3 _position, Camera _camera)
        {
            //work with Orthographic camera only
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning($"Camera is null");
                return false;
            }
            var z = _position.z - _camera.transform.position.z;
            var screenSize = GetScreenSizeInWorldSpace(_camera, z);
            var halfHeight = screenSize.y / 2f;
            var halfWidth = screenSize.x / 2f;

            return _position.x <= halfWidth && _position.x >= -halfWidth && _position.y <= halfHeight && _position.y >= -halfHeight;
        }


        /// <summary>
        /// Check if position is in camera view and return screen position
        /// </summary>
        public static bool PositionInScreenPointView(Vector3 _position, Camera _camera, out Vector2 _screenPosition)
        {
            _screenPosition = Vector2.zero;
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning($"No Camera");
                return false;
            }

            var point = _camera.WorldToViewportPoint(_position);

            _screenPosition.x = point.x * Screen.width;
            _screenPosition.y = point.y * Screen.height;

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

        public static int RandomIndexWithoutCurrent(int _currentIndex, int _max, int _limitRepeat = 20)
        {
            // var index = UnityEngine.Random.Range(0, _max);

            // if (index == _currentIndex && _limitRepeat > 0) return RandomIndexWithoutCurrent(_currentIndex, _max, _limitRepeat - 1);
            // else return index;

            int randomIndex;
            int count = 0;
            do
            {
                randomIndex = Random.Range(0, _max);
                count++;
                if (count > _limitRepeat) break;
            } while (randomIndex == _currentIndex);
            return randomIndex;
        }

        public static Bounds CalculateBoxColliderFromMeshs(Transform _target, bool _isIncludeChildren = false)
        {
            if (_target == null)
            {
                Debug.LogWarning($"Target is null");
                return new Bounds();
            }

            var renderers = _isIncludeChildren ? _target.GetComponentsInChildren<Renderer>() : new Renderer[1] { _target.GetComponent<Renderer>() };

            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning($"No Renderer");
                return new Bounds();
            }

            var bounds = new Bounds();

            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;


        }


        public static Vector2 GetScreenSizeInWorldSpace(Camera _camera, float distance)
        {

            var isPerspectiveMode = _camera.orthographic == false;
            var height = _camera.orthographicSize * 2.0f;

            if (isPerspectiveMode)
                height = 2.0f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            var width = height * _camera.aspect;

            return new Vector2(width, height);

        }

        #region UI Utilities
        public static bool IsUIElementInView(RectTransform _rectTransform)
        {
            var rect = RectTransformToRect(_rectTransform);
            return IsRectInView(rect);
        }
        public static bool IsRectInView(Rect _rect)
        {
            return _rect.Overlaps(new Rect(0, 0, Screen.width, Screen.height));
        }

        public static Rect RectTransformToRect(RectTransform _rectTransform)
        {

            var width = _rectTransform.rect.width;
            var height = _rectTransform.rect.height;

            var x = _rectTransform.position.x - (width * _rectTransform.pivot.x);
            var y = _rectTransform.position.y - (height * _rectTransform.pivot.y);
            return new Rect(x, y, width, height);
        }
        
        #endregion


        
        /// <summary>
        /// Generate unique ID with custom character sets
        /// </summary>
        /// <param name="length">Length of generated ID</param>
        /// <param name="includeUppercase">Include uppercase letters (A-Z)</param>
        /// <param name="includeLowercase">Include lowercase letters (a-z)</param>
        /// <param name="includeNumbers">Include numbers (0-9)</param>
        /// <param name="includeSpecialChars">Include special characters</param>
        /// <param name="customSpecialChars">Custom special characters to use</param>
        /// <returns>Generated unique ID</returns>
        public static string GenerateUniqueId(int length = 8, 
            bool includeUppercase = true, 
            bool includeLowercase = true, 
            bool includeNumbers = true, 
            bool includeSpecialChars = false,
            string customSpecialChars = "!@#$%^&*")
        {
            string chars = "";
            
            if (includeUppercase) chars += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (includeLowercase) chars += "abcdefghijklmnopqrstuvwxyz";
            if (includeNumbers) chars += "0123456789";
            if (includeSpecialChars) chars += customSpecialChars;
            
            if (string.IsNullOrEmpty(chars))
                chars = "0123456789"; // Fallback to numbers only
            
            var result = new System.Text.StringBuilder(length);
            var random = new System.Random();
            
            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }
            
            return result.ToString();
        }
       
       
      


    }
}
