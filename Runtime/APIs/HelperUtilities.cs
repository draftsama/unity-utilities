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
            _screenPosition.y = point.y * Screen.height;

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

        /// <summary>
        /// Calculate the screen space dimensions of a 3D bounding box projected by the camera
        /// </summary>
        /// <param name="_bounds">The 3D bounding box in world space</param>
        /// <param name="_camera">The camera to project the bounds to screen space</param>
        /// <returns>Vector2 containing the width and height of the bounding box in screen pixels</returns>
        public static Vector2 GetBoundingSizeInScreenView(Bounds _bounds, Camera _camera)
        {

            // Get the corners of the bounding box in world space
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(_bounds.center.x - _bounds.extents.x, _bounds.center.y - _bounds.extents.y, _bounds.center.z - _bounds.extents.z);
            corners[1] = new Vector3(_bounds.center.x + _bounds.extents.x, _bounds.center.y - _bounds.extents.y, _bounds.center.z - _bounds.extents.z);
            corners[2] = new Vector3(_bounds.center.x - _bounds.extents.x, _bounds.center.y + _bounds.extents.y, _bounds.center.z - _bounds.extents.z);
            corners[3] = new Vector3(_bounds.center.x + _bounds.extents.x, _bounds.center.y + _bounds.extents.y, _bounds.center.z - _bounds.extents.z);
            corners[4] = new Vector3(_bounds.center.x - _bounds.extents.x, _bounds.center.y - _bounds.extents.y, _bounds.center.z + _bounds.extents.z);
            corners[5] = new Vector3(_bounds.center.x + _bounds.extents.x, _bounds.center.y - _bounds.extents.y, _bounds.center.z + _bounds.extents.z);
            corners[6] = new Vector3(_bounds.center.x - _bounds.extents.x, _bounds.center.y + _bounds.extents.y, _bounds.center.z + _bounds.extents.z);
            corners[7] = new Vector3(_bounds.center.x + _bounds.extents.x, _bounds.center.y + _bounds.extents.y, _bounds.center.z + _bounds.extents.z);


            Vector2[] screenPoints = new Vector2[8];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 screenPoint = _camera.WorldToScreenPoint(corners[i]);
                screenPoints[i] = new Vector2(screenPoint.x, screenPoint.y);
            }
            // Find the min and max screen coordinates
            Vector2 min = screenPoints[0];
            Vector2 max = screenPoints[0];
            for (int i = 1; i < screenPoints.Length; i++)
            {
                min = Vector2.Min(min, screenPoints[i]);
                max = Vector2.Max(max, screenPoints[i]);
            }

            return new Vector2(max.x - min.x, max.y - min.y);
        }



        /// <summary>
    /// Clusters points based on proximity and returns the centroid of each cluster
    /// </summary>
    /// <param name="points">List of points to cluster</param>
    /// <param name="radius">Distance threshold for clustering</param>
    /// <returns>List of centroid points representing each cluster</returns>
    public static List<Vector3> GetClusterCentroids(List<Vector3> points, float radius)
    {
        List<Vector3> centroids = new List<Vector3>();
        List<Vector3> pointsToProcess = new List<Vector3>(points);

        while (pointsToProcess.Count > 0)
        {
            // Start a new cluster with the first remaining point
            List<Vector3> currentCluster = new List<Vector3>();
            Queue<Vector3> queue = new Queue<Vector3>();

            queue.Enqueue(pointsToProcess[0]);
            pointsToProcess.RemoveAt(0);

            while (queue.Count > 0)
            {
                Vector3 currentPoint = queue.Dequeue();
                currentCluster.Add(currentPoint);

                // Loop backwards to avoid index issues when removing elements
                for (int i = pointsToProcess.Count - 1; i >= 0; i--)
                {
                    if (Vector3.Distance(currentPoint, pointsToProcess[i]) < radius)
                    {
                        // If a nearby point is found, move it from pointsToProcess to the queue
                        queue.Enqueue(pointsToProcess[i]);
                        pointsToProcess.RemoveAt(i);
                    }
                }
            }

            // Calculate the centroid of the completed cluster
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 pointInCluster in currentCluster)
            {
                centroid += pointInCluster;
            }
            centroids.Add(centroid / currentCluster.Count);
        }

        return centroids;
    }

    /// <summary>
    /// Clusters points based on proximity and returns the closest point to origin from each cluster
    /// </summary>
    /// <param name="points">List of points to cluster</param>
    /// <param name="radius">Distance threshold for clustering</param>
    /// <returns>List of closest points to origin representing each cluster</returns>
    public static List<Vector3> GetClosestToOrigin(List<Vector3> points, float radius)
    {
        List<Vector3> closestPoints = new List<Vector3>();
        List<Vector3> pointsToProcess = new List<Vector3>(points);

        while (pointsToProcess.Count > 0)
        {
            // Start a new cluster with the first remaining point
            List<Vector3> currentCluster = new List<Vector3>();
            Queue<Vector3> queue = new Queue<Vector3>();

            queue.Enqueue(pointsToProcess[0]);
            pointsToProcess.RemoveAt(0);

            while (queue.Count > 0)
            {
                Vector3 currentPoint = queue.Dequeue();
                currentCluster.Add(currentPoint);

                // Loop backwards to avoid index issues when removing elements
                for (int i = pointsToProcess.Count - 1; i >= 0; i--)
                {
                    if (Vector3.Distance(currentPoint, pointsToProcess[i]) < radius)
                    {
                        // If a nearby point is found, move it from pointsToProcess to the queue
                        queue.Enqueue(pointsToProcess[i]);
                        pointsToProcess.RemoveAt(i);
                    }
                }
            }

            // Find the point closest to origin in the completed cluster
            Vector3 closestToOrigin = currentCluster[0];
            float closestDistance = Vector3.Distance(Vector3.zero, closestToOrigin);
            
            foreach (Vector3 pointInCluster in currentCluster)
            {
                float distance = Vector3.Distance(Vector3.zero, pointInCluster);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestToOrigin = pointInCluster;
                }
            }
            
            closestPoints.Add(closestToOrigin);
        }

        return closestPoints;
    }






    }
}
