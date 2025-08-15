using System.Collections.Generic;
using UnityEngine;
namespace Modules.Utilities
{
    public class HUDRenderer : MonoBehaviour
    {

        public Camera m_Camera;

        public bool m_Visible = true;

        public float m_Margin = 20f;
        public float m_ArrowMargin = 20f;

        [Header("Distance Settings")]
        [Tooltip("Limits for fade distance slider")]
        public float m_MinDistanceLimit = 0f;
        public float m_MaxDistanceLimit = 100f;
        
        [Tooltip("X = Start Fade Distance, Y = Full Visible Distance")]
        [MinMaxSlider("m_MinDistanceLimit", "m_MaxDistanceLimit", 0f, 200f)]
        public Vector2 m_FadeDistance = new Vector2(50f, 20f);

        [Header("Rendering Order")]
        [Tooltip("Sort indicators by distance")]
        public bool m_SortByDistance = true;

        [Header("Performance")]
        [Tooltip("Update sorting every N frames (1 = every frame, 2 = every other frame)")]
        [Range(1, 10)]
        public int m_SortUpdateFrequency = 1;

        [Header("Overlap Detection")]
        [Tooltip("Hide indicators that are behind overlapping ones")]
        public bool m_HandleOverlap = true;

        public List<HUDIndicatorView> m_IndicatorViewList = new List<HUDIndicatorView>();

        private RectTransform _RectTransform;
        
        // Performance optimization: reuse collections
        private List<HUDIndicatorView> _activeViews = new List<HUDIndicatorView>();
        private List<float> _viewDistances = new List<float>();
        private List<int> _sortedIndices = new List<int>();
        private List<bool> _hiddenByOverlap = new List<bool>();
        private int _frameCounter = 0;



        void Awake()
        {
            _RectTransform = GetComponent<RectTransform>();
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
            }
        }


        void Start()
        {

        }


        public void RegisterIndicator(HUDIndicator indicator)
        {

            var go = new GameObject(indicator.name + " View", typeof(RectTransform));
            go.transform.SetParent(_RectTransform, false);
            var indicatorView = go.AddComponent<HUDIndicatorView>();

            indicatorView.Initialize(indicator,this);

            m_IndicatorViewList.Add(indicatorView);


        }

        public void UnregisterIndicator(HUDIndicator indicator)
        {
            for (int i = m_IndicatorViewList.Count - 1; i >= 0; i--)
            {
                if (m_IndicatorViewList[i].Indicator == indicator)
                {
                    if (m_IndicatorViewList[i] != null && m_IndicatorViewList[i].gameObject != null)
                    {
                        Destroy(m_IndicatorViewList[i].gameObject);
                    }
                    m_IndicatorViewList.RemoveAt(i);
                    return;
                }
            }
        }

        public HUDIndicatorView GetIndicatorView(HUDIndicator indicator)
        {
            foreach (var view in m_IndicatorViewList)
            {
                if (view.Indicator == indicator)
                {
                    return view;
                }
            }
            return null;
        }

        public float TotalMargin
        {
            get { return m_Margin + m_ArrowMargin; }
        }

        /// <summary>
        /// Calculate alpha value based on distance from camera
        /// </summary>
        /// <param name="distance">Distance from camera to target</param>
        /// <returns>Alpha value between 0 and 1</returns>
        private float CalculateAlphaFromDistance(float distance)
        {
            // If distance values are invalid (0 or negative), don't use fade - always show full alpha
            if (m_FadeDistance.x <= 0f || m_FadeDistance.y <= 0f)
            {
                return 1f;
            }

            // Ensure proper distance settings
            float minDistance = Mathf.Min(m_FadeDistance.y, m_FadeDistance.x);
            float maxDistance = Mathf.Max(m_FadeDistance.y, m_FadeDistance.x);

            // If distances are equal, no fade effect
            if (Mathf.Approximately(minDistance, maxDistance))
            {
                return 1f;
            }
            
            if (distance <= minDistance)
            {
                return 1f; // Fully visible
            }
            else if (distance >= maxDistance)
            {
                return 0f; // Completely transparent
            }
            else
            {
                // Linear interpolation between distances
                float t = (distance - minDistance) / (maxDistance - minDistance);
                return 1f - t; // Fade from 1 to 0
            }
        }

        void Update()
        {
            _frameCounter++;
            bool shouldUpdateSorting = (_frameCounter % m_SortUpdateFrequency) == 0;
            
            // Clear and reuse existing collections to avoid GC allocation
            _activeViews.Clear();
            _viewDistances.Clear();
            _hiddenByOverlap.Clear();
            
            // Only clear sorted indices if we're updating sorting this frame
            if (shouldUpdateSorting)
            {
                _sortedIndices.Clear();
            }
            
            // First pass: collect active views and calculate distances
            for (int i = 0; i < m_IndicatorViewList.Count; i++)
            {
                var view = m_IndicatorViewList[i];
                
                // Check if the indicator GameObject is active
                bool isIndicatorActive = view.Indicator.gameObject.activeInHierarchy;
                // Check if the indicator should be shown based on m_IsShow flag
                bool shouldShow = view.Indicator.m_Visible && m_Visible;

                if (!isIndicatorActive || !shouldShow)
                {
                    view.Hide();
                    continue;
                }

                Vector3 worldPos = view.Indicator.m_Transform.position;
                Vector3 cameraToTarget = worldPos - m_Camera.transform.position;
                float distance = cameraToTarget.magnitude;
                
                _activeViews.Add(view);
                _viewDistances.Add(distance);
                _hiddenByOverlap.Add(false); // Initialize as not hidden
                
                if (shouldUpdateSorting)
                {
                    _sortedIndices.Add(_activeViews.Count - 1);
                }
            }
            
            // Sort indices by distance if enabled and updating this frame
            if (m_SortByDistance && shouldUpdateSorting && _activeViews.Count > 1)
            {
                // Farthest first (lower sibling index = renders behind)
                // Closest last (higher sibling index = renders on top)
                _sortedIndices.Sort((a, b) => _viewDistances[b].CompareTo(_viewDistances[a]));
            }

            // Handle overlap detection if enabled
            if (m_HandleOverlap && _activeViews.Count > 1)
            {
                HandleOverlapDetection();
            }

            // Use existing sorted order if not updating sorting this frame
            int processCount = m_SortByDistance ? _sortedIndices.Count : _activeViews.Count;
            
            // Process views in sorted order
            for (int i = 0; i < processCount; i++)
            {
                int viewIndex = m_SortByDistance ? _sortedIndices[i] : i;
                var view = _activeViews[viewIndex];
                float distance = _viewDistances[viewIndex];
                
                // Update sibling index only when sorting is updated
                if (m_SortByDistance && shouldUpdateSorting && view.RectTransform.GetSiblingIndex() != i)
                {
                    view.RectTransform.SetSiblingIndex(i);
                }
                
                // Calculate alpha based on distance
                float alpha = CalculateAlphaFromDistance(distance);

                // Reuse already calculated world position
                Vector3 worldPos = view.Indicator.m_Transform.position;
                Vector3 cameraToTarget = worldPos - m_Camera.transform.position;

                // Check if the target is in front of the camera
                bool isInFront = Vector3.Dot(cameraToTarget, m_Camera.transform.forward) > 0;

                if (!isInFront || alpha <= 0f)
                {
                    view.Hide(); // Hide when behind camera or too far
                    continue;
                }

                // Convert world position to screen position
                Vector3 screenPos = m_Camera.WorldToScreenPoint(worldPos);
                Vector3 canvasPos = _RectTransform.InverseTransformPoint(screenPos);


                if (view.Indicator.m_IndicatorData.m_AutoSize && view.Indicator.m_IndicatorData.m_BoxCollider != null)
                {
                    // Automatically size the BoxCollider to fit the indicator
                    var size = HelperUtilities.GetBoundingSizeInScreenView(view.Indicator.m_IndicatorData.m_BoxCollider.bounds, m_Camera);
                    view.SetOnScreenSize(size);
                }


                // Get canvas rect bounds
                Rect canvasRect = _RectTransform.rect;

                // Check if the object is within the canvas bounds (with margin)
                bool isOnScreen = canvasPos.x >= (canvasRect.xMin + TotalMargin) && canvasPos.x <= (canvasRect.xMax - TotalMargin) &&
                                 canvasPos.y >= (canvasRect.yMin + TotalMargin) && canvasPos.y <= (canvasRect.yMax - TotalMargin) &&
                                 screenPos.z > 0;

              

                if (isOnScreen)
                {
                    // Show on-screen indicator (but check if hidden by overlap)
                    view.UpdateOnScreenPosition(new Vector2(canvasPos.x, canvasPos.y));
                    
                    // Only show if not hidden by overlap
                    if (!m_HandleOverlap || !_hiddenByOverlap[viewIndex])
                    {
                        view.ShowOnScreen();
                    }
                    else
                    {
                        // Show off-screen parts but keep OnScreen hidden due to overlap
                        if (view.OffScreenRectTransform != null)
                        {
                            view.OffScreenRectTransform.gameObject.SetActive(false);
                        }
                        if (view.OffScreenArrowRectTransform != null)
                        {
                            view.OffScreenArrowRectTransform.gameObject.SetActive(false);
                        }
                    }
                    
                    view.CanvasGroup.alpha = alpha; // Apply distance-based alpha



                }
                else
                {
                    // Calculate off-screen position 
                    Vector2 onscreenPos = ClampToCanvasEdge(canvasPos, canvasRect, TotalMargin);
                    view.UpdateOffScreenPosition(onscreenPos);

                    Vector2 arrowPos = ClampToCanvasEdge(canvasPos, canvasRect, m_ArrowMargin);
                    view.UpdateOffScreenArrowPosition(arrowPos);

                    // Calculate angle for arrow rotation (from view position to actual object position)
                    Vector2 viewToObject = new Vector2(canvasPos.x, canvasPos.y) - onscreenPos;
                    float angle = Mathf.Atan2(viewToObject.y, viewToObject.x) * Mathf.Rad2Deg;
                    view.SetArrowRotation(angle);

                    view.ShowOffScreen();
                    view.CanvasGroup.alpha = alpha; // Apply distance-based alpha
                }
            }
        }

        private Vector2 ClampToCanvasEdge(Vector2 canvasPos, Rect canvasRect, float margin)
        {
            // Calculate the center of the canvas
            Vector2 center = new Vector2(canvasRect.center.x, canvasRect.center.y);

            // Calculate direction from center to target
            Vector2 direction = (canvasPos - center).normalized;

            // Calculate canvas bounds with margin
            float left = canvasRect.xMin + margin;
            float right = canvasRect.xMax - margin;
            float bottom = canvasRect.yMin + margin;
            float top = canvasRect.yMax - margin;

            // Calculate intersection with canvas edges
            Vector2 clampedPos = center;

            // Find which edge the ray hits first
            float t = float.MaxValue;

            // Check intersection with right edge
            if (direction.x > 0)
            {
                float tRight = (right - center.x) / direction.x;
                if (tRight > 0) t = Mathf.Min(t, tRight);
            }
            // Check intersection with left edge
            else if (direction.x < 0)
            {
                float tLeft = (left - center.x) / direction.x;
                if (tLeft > 0) t = Mathf.Min(t, tLeft);
            }

            // Check intersection with top edge
            if (direction.y > 0)
            {
                float tTop = (top - center.y) / direction.y;
                if (tTop > 0) t = Mathf.Min(t, tTop);
            }
            // Check intersection with bottom edge
            else if (direction.y < 0)
            {
                float tBottom = (bottom - center.y) / direction.y;
                if (tBottom > 0) t = Mathf.Min(t, tBottom);
            }

            // Calculate final position
            if (t != float.MaxValue)
            {
                clampedPos = center + direction * t;
            }

            return clampedPos;
        }

        /// <summary>
        /// Handle overlap detection between UI indicators
        /// </summary>
        private void HandleOverlapDetection()
        {
            // First, reset all views to not hidden by overlap (allow them to show again)
            for (int i = 0; i < _activeViews.Count; i++)
            {
                _hiddenByOverlap[i] = false;
                // Reset OnScreen visibility if it exists
                var view = _activeViews[i];
                if (view.OnScreenRectTransform != null && !view.OnScreenRectTransform.gameObject.activeSelf)
                {
                    view.ShowOnScreenFromOverlap();
                }
            }

            // Then check for overlaps and hide as needed
            for (int i = 0; i < _activeViews.Count; i++)
            {
                if (_hiddenByOverlap[i]) continue; // Skip if already hidden
                
                var view = _activeViews[i];
                var rt = view.OnScreenRectTransform;
                
                // Skip if no OnScreen component or not active
                if (rt == null || !rt.gameObject.activeSelf) continue;
                
                int siblingIndex = view.RectTransform.GetSiblingIndex();

                for (int j = i + 1; j < _activeViews.Count; j++)
                {
                    if (_hiddenByOverlap[j]) continue; // Skip if already hidden
                    
                    var otherView = _activeViews[j];
                    var otherRt = otherView.OnScreenRectTransform;

                    // Skip if no OnScreen component or not active
                    if (otherRt == null || !otherRt.gameObject.activeSelf) continue;

                    if (HelperUtilities.IsUIOverlapping(rt, otherRt))
                    {
                        int otherSiblingIndex = otherView.RectTransform.GetSiblingIndex();

                        // Hide the one that's behind (lower sibling index)
                        if (siblingIndex < otherSiblingIndex)
                        {
                            _hiddenByOverlap[i] = true;
                            view.HideOnScreenByOverlap();
                            break; // This view is hidden, no need to check further
                        }
                        else
                        {
                            _hiddenByOverlap[j] = true;
                            otherView.HideOnScreenByOverlap();
                        }
                    }
                }
            }
        }


    }
}