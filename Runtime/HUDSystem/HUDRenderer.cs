using System.Collections.Generic;
using UnityEngine;
namespace Modules.Utilities
{
    // Run after camera controllers (e.g. Cinemachine) so positions use this frame's camera pose
    [DefaultExecutionOrder(1000)]
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
        
        [Tooltip("Fully visible closer than the smaller value, fading out to hidden at the larger value. Set either to 0 to disable.")]
        [MinMaxSlider("m_MinDistanceLimit", "m_MaxDistanceLimit", 0f, 200f)]
        public Vector2 m_FadeDistance = new Vector2(15f, 20f);

        [Header("Edge Fade Settings")]
        [Tooltip("Enable fade when indicators get close to screen edges (OnScreen only). Only works for indicators with UseOffScreen = false")]
        public bool m_EnableEdgeFade = true;
        
        [Tooltip("Distance from edge where fade starts (in pixels). Fade calculated from effective boundary (canvas edge - margin)")]
        [Range(0f, 200f)]
        public float m_EdgeFadeDistance = 50f;

        [Header("Rendering Order")]
        [Tooltip("Sort indicators by distance")]
        public bool m_SortByDistance = true;

        [Header("Performance")]
        [Tooltip("Update sorting every N frames (1 = every frame, 2 = every other frame)")]
        [Range(1, 10)]
        public int m_SortUpdateFrequency = 1;

        [Tooltip("Released views kept per prefab set for reuse; extras are destroyed")]
        [Range(0, 256)]
        public int m_MaxPooledViewsPerType = 32;



        public List<HUDIndicatorView> m_IndicatorViewList = new List<HUDIndicatorView>();

        private RectTransform _RectTransform;
        
        // Performance optimization: reuse collections
        private List<HUDIndicatorView> _activeViews = new List<HUDIndicatorView>();
        private List<float> _viewDistances = new List<float>();
        private List<Vector3> _worldPositions = new List<Vector3>(); // Cache world positions
        private List<int> _sortedIndices = new List<int>();
        private int _frameCounter = 0;
        private System.Comparison<int> _distanceComparison; // Cached to avoid a delegate allocation per sort

        private Canvas _canvas;
        private readonly Dictionary<HUDIndicator, HUDIndicatorView> _viewByIndicator = new Dictionary<HUDIndicator, HUDIndicatorView>(ReferenceComparer.Instance);
        private readonly Dictionary<HUDViewKey, Stack<HUDIndicatorView>> _pool = new Dictionary<HUDViewKey, Stack<HUDIndicatorView>>();

        // Registry callbacks can fire mid (de)activation where SetActive is illegal, so changes apply in LateUpdate.
        // true = add, false = remove.
        private readonly Dictionary<HUDIndicator, bool> _pendingChanges = new Dictionary<HUDIndicator, bool>(ReferenceComparer.Instance);
        private readonly List<KeyValuePair<HUDIndicator, bool>> _pendingBuffer = new List<KeyValuePair<HUDIndicator, bool>>();

        private static readonly List<HUDRenderer> s_Active = new List<HUDRenderer>();
        public static IReadOnlyList<HUDRenderer> ActiveRenderers => s_Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active.Clear();
        }

        // UnityEngine.Object equality treats any two destroyed objects as equal; keys must stay distinct
        private sealed class ReferenceComparer : IEqualityComparer<HUDIndicator>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(HUDIndicator x, HUDIndicator y) => ReferenceEquals(x, y);
            public int GetHashCode(HUDIndicator obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }


        void Awake()
        {
            _RectTransform = GetComponent<RectTransform>();
            _distanceComparison = CompareByDistanceDescending;
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
            }
        }

        void OnEnable()
        {
            s_Active.Add(this);
            HUDIndicator.Registered += OnIndicatorRegistered;
            HUDIndicator.Unregistered += OnIndicatorUnregistered;

            // Catch up on indicators that came or went while this renderer was disabled or not yet created
            var indicators = HUDIndicator.ActiveIndicators;
            for (int i = 0; i < indicators.Count; i++)
            {
                OnIndicatorRegistered(indicators[i]);
            }
            for (int i = 0; i < m_IndicatorViewList.Count; i++)
            {
                var view = m_IndicatorViewList[i];
                if (view != null && view.Indicator != null && !view.Indicator.IsRegistered)
                {
                    _pendingChanges[view.Indicator] = false;
                }
            }
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            HUDIndicator.Registered -= OnIndicatorRegistered;
            HUDIndicator.Unregistered -= OnIndicatorUnregistered;
            // Views stay bound; OnEnable reconciles them
        }

        private void OnIndicatorRegistered(HUDIndicator indicator)
        {
            if (indicator.UsesRenderer(this))
            {
                _pendingChanges[indicator] = true;
            }
        }

        private void OnIndicatorUnregistered(HUDIndicator indicator)
        {
            _pendingChanges[indicator] = false;
        }

        private void ApplyPendingChanges()
        {
            if (_pendingChanges.Count == 0) return;

            // Copy first: handlers invoked by Register/Unregister may queue further changes
            _pendingBuffer.Clear();
            foreach (var change in _pendingChanges)
            {
                _pendingBuffer.Add(change);
            }
            _pendingChanges.Clear();

            for (int i = 0; i < _pendingBuffer.Count; i++)
            {
                var change = _pendingBuffer[i];
                if (change.Value)
                {
                    if (change.Key != null && change.Key.IsRegistered)
                    {
                        RegisterIndicator(change.Key);
                    }
                }
                else
                {
                    UnregisterIndicator(change.Key);
                }
            }
            _pendingBuffer.Clear();
        }

        private Camera GetCanvasCamera()
        {
            if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }
            return _canvas.worldCamera != null ? _canvas.worldCamera : m_Camera;
        }

        private float GetCanvasScaleFactor()
        {
            return _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        }

        private int CompareByDistanceDescending(int a, int b)
        {
            return _viewDistances[b].CompareTo(_viewDistances[a]);
        }

        /// <summary>
        /// Bind a view to the indicator immediately. Indicators register themselves; call this only to
        /// drive a renderer manually, and not from OnEnable/OnDisable.
        /// </summary>
        public void RegisterIndicator(HUDIndicator indicator)
        {
            if (indicator == null || GetIndicatorView(indicator) != null)
            {
                return;
            }

            if (_RectTransform == null)
            {
                _RectTransform = GetComponent<RectTransform>();
            }

            var indicatorView = AcquireView(indicator);
            m_IndicatorViewList.Add(indicatorView);
            _viewByIndicator[indicator] = indicatorView;
        }

        /// <summary>
        /// Release the indicator's view to the pool immediately. Same caveats as RegisterIndicator.
        /// </summary>
        public void UnregisterIndicator(HUDIndicator indicator)
        {
            if (ReferenceEquals(indicator, null)) return;

            _viewByIndicator.Remove(indicator);
            for (int i = m_IndicatorViewList.Count - 1; i >= 0; i--)
            {
                var view = m_IndicatorViewList[i];
                if (view != null && ReferenceEquals(view.Indicator, indicator))
                {
                    m_IndicatorViewList.RemoveAt(i);
                    ReleaseView(view);
                    return;
                }
            }
        }

        public HUDIndicatorView GetIndicatorView(HUDIndicator indicator)
        {
            if (ReferenceEquals(indicator, null)) return null;
            if (_viewByIndicator.TryGetValue(indicator, out var view) && view != null)
            {
                return view;
            }
            return null;
        }

        private HUDIndicatorView AcquireView(HUDIndicator indicator)
        {
            var key = HUDViewKey.From(indicator.m_IndicatorData);
            if (_pool.TryGetValue(key, out var stack))
            {
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled != null)
                    {
                        pooled.Bind(indicator);
                        return pooled;
                    }
                }
            }

            var go = new GameObject(indicator.name + " View", typeof(RectTransform));
            go.transform.SetParent(_RectTransform, false);
            var indicatorView = go.AddComponent<HUDIndicatorView>();
            indicatorView.Initialize(indicator, this);
            return indicatorView;
        }

        private void ReleaseView(HUDIndicatorView view)
        {
            view.Unbind();

            if (!_pool.TryGetValue(view.Key, out var stack))
            {
                stack = new Stack<HUDIndicatorView>();
                _pool.Add(view.Key, stack);
            }

            if (stack.Count < m_MaxPooledViewsPerType)
            {
                stack.Push(view);
            }
            else
            {
                Destroy(view.gameObject);
            }
        }

        // Drop views whose indicator was destroyed without unregistering (e.g. manual registration)
        private void ReleaseOrphanedViews()
        {
            for (int i = m_IndicatorViewList.Count - 1; i >= 0; i--)
            {
                var view = m_IndicatorViewList[i];
                if (view == null)
                {
                    m_IndicatorViewList.RemoveAt(i);
                }
                else if (view.Indicator == null)
                {
                    if (!ReferenceEquals(view.Indicator, null))
                    {
                        _viewByIndicator.Remove(view.Indicator);
                    }
                    m_IndicatorViewList.RemoveAt(i);
                    ReleaseView(view);
                }
            }
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

        /// <summary>
        /// Calculate alpha value based on distance from screen edges
        /// </summary>
        /// <param name="canvasPos">Position on canvas</param>
        /// <param name="canvasRect">Canvas rectangle bounds</param>
        /// <param name="margin">Margin to consider as the effective edge</param>
        /// <returns>Alpha value between 0 and 1</returns>
        private float CalculateAlphaFromEdge(Vector2 canvasPos, Rect canvasRect, float margin)
        {
            if (!m_EnableEdgeFade || m_EdgeFadeDistance <= 0f)
            {
                return 1f;
            }

            // Calculate effective boundaries (considering margin as the real edge)
            float leftEdge = canvasRect.xMin + margin;
            float rightEdge = canvasRect.xMax - margin;
            float topEdge = canvasRect.yMax - margin;
            float bottomEdge = canvasRect.yMin + margin;

            // Calculate distance to each effective edge
            float leftDistance = canvasPos.x - leftEdge;
            float rightDistance = rightEdge - canvasPos.x;
            float topDistance = topEdge - canvasPos.y;
            float bottomDistance = canvasPos.y - bottomEdge;

            // Find the minimum distance to any edge
            float minEdgeDistance = Mathf.Min(Mathf.Min(leftDistance, rightDistance), Mathf.Min(topDistance, bottomDistance));

            // Calculate fade based on edge distance
            if (minEdgeDistance >= m_EdgeFadeDistance)
            {
                return 1f; // Fully visible
            }
            else if (minEdgeDistance <= 0f)
            {
                return 0f; // Completely transparent (at or beyond edge)
            }
            else
            {
                // Linear interpolation
                return minEdgeDistance / m_EdgeFadeDistance;
            }
        }

        void LateUpdate()
        {
            ApplyPendingChanges();
            ReleaseOrphanedViews();

            if (_canvas == null)
            {
                var parentCanvas = GetComponentInParent<Canvas>();
                _canvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
            }

            // Camera may spawn after Awake
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
                if (m_Camera == null)
                {
                    for (int i = 0; i < m_IndicatorViewList.Count; i++)
                    {
                        var hiddenView = m_IndicatorViewList[i];
                        if (hiddenView != null)
                        {
                            hiddenView.Hide();
                        }
                    }
                    return;
                }
            }

            _frameCounter++;
            bool shouldUpdateSorting = (_frameCounter % Mathf.Max(1, m_SortUpdateFrequency)) == 0;

            // Clear and reuse existing collections to avoid GC allocation
            _activeViews.Clear();
            _viewDistances.Clear();
            _worldPositions.Clear();
            _sortedIndices.Clear();

            // First pass: collect active views and calculate distances
            for (int i = 0; i < m_IndicatorViewList.Count; i++)
            {
                var view = m_IndicatorViewList[i];

                // Skip destroyed views or views whose indicator is gone
                if (view == null || view.Indicator == null)
                {
                    continue;
                }

                // Check if the indicator GameObject is active
                bool isIndicatorActive = view.Indicator.gameObject.activeInHierarchy;
                // Check if the indicator should be shown based on m_IsShow flag
                bool shouldShow = view.Indicator.m_Visible && m_Visible;

                if (!isIndicatorActive || !shouldShow)
                {
                    view.Hide();
                    continue;
                }

                Vector3 worldPos = view.Indicator.GetWorldPosition();
                Vector3 cameraToTarget = worldPos - m_Camera.transform.position;
                float distance = cameraToTarget.magnitude;
                
                _activeViews.Add(view);
                _viewDistances.Add(distance);
                _worldPositions.Add(worldPos);

                // Rebuilt every frame so indices always match this frame's active list
                _sortedIndices.Add(_activeViews.Count - 1);
            }

            // Sort indices by distance if enabled and updating this frame
            if (m_SortByDistance && shouldUpdateSorting && _activeViews.Count > 1)
            {
                // Farthest first (lower sibling index = renders behind)
                // Closest last (higher sibling index = renders on top)
                _sortedIndices.Sort(_distanceComparison);
            }

            int processCount = _activeViews.Count;

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
                float offScreenAlpha = view.Indicator.m_IndicatorData.m_OffScreenIgnoreDistanceFade ? 1f : alpha;

                // Use cached world position
                Vector3 worldPos = _worldPositions[viewIndex];
                Vector3 cameraToTarget = worldPos - m_Camera.transform.position;

                // Check if the target is in front of the camera
                bool isInFront = Vector3.Dot(cameraToTarget, m_Camera.transform.forward) > 0;

                bool useOffScreen = view.Indicator.m_IndicatorData.m_UseOffScreen;

                if (!isInFront && (!useOffScreen || offScreenAlpha <= 0f))
                {
                    view.Hide(); // Behind the camera with no visible off-screen view
                    continue;
                }

                // Get canvas rect bounds
                Rect canvasRect = _RectTransform.rect;

                if (!isInFront)
                {
                    // WorldToScreenPoint is unstable behind the camera, so aim along the camera-local direction instead
                    Vector3 local = m_Camera.transform.InverseTransformPoint(worldPos);
                    Vector2 dir = new Vector2(local.x, local.y);
                    dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;

                    Vector2 farCanvasPos = (Vector2)canvasRect.center + dir * (canvasRect.width + canvasRect.height);
                    ShowOffScreenIndicator(view, farCanvasPos, canvasRect, offScreenAlpha);
                    continue;
                }

                // Convert world position to screen position, then into this rect's local space (works for every canvas mode)
                Vector3 screenPos = m_Camera.WorldToScreenPoint(worldPos);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_RectTransform, screenPos, GetCanvasCamera(), out Vector2 canvasPos2D))
                {
                    view.Hide();
                    continue;
                }

                if (view.Indicator.m_IndicatorData.m_AutoSize && view.Indicator.m_IndicatorData.m_BoxCollider != null)
                {
                    // Screen pixels to canvas units, so CanvasScaler doesn't inflate the box
                    var size = HelperUtilities.GetBoundingSizeInScreenView(view.Indicator.m_IndicatorData.m_BoxCollider.bounds, m_Camera);
                    view.SetOnScreenSize(size / GetCanvasScaleFactor());
                }

                // Check if the object is within the canvas bounds (without margin for visibility check)
                bool isOnScreen = canvasPos2D.x >= canvasRect.xMin && canvasPos2D.x <= canvasRect.xMax &&
                                 canvasPos2D.y >= canvasRect.yMin && canvasPos2D.y <= canvasRect.yMax;

                // Check if the object is within the display area (with margin for positioning)
                bool isInDisplayArea = canvasPos2D.x >= (canvasRect.xMin + TotalMargin) && canvasPos2D.x <= (canvasRect.xMax - TotalMargin) &&
                                      canvasPos2D.y >= (canvasRect.yMin + TotalMargin) && canvasPos2D.y <= (canvasRect.yMax - TotalMargin);

                if (isInDisplayArea || (isOnScreen && !useOffScreen))
                {
                    if (alpha <= 0f)
                    {
                        view.Hide(); // Too far
                        continue;
                    }

                    // Show on-screen indicator
                    view.UpdateOnScreenPosition(canvasPos2D);
                    view.ShowOnScreen();
                    
                    // Only apply edge fade if indicator doesn't use offscreen
                    float edgeAlpha = useOffScreen ? 1f : 
                                     CalculateAlphaFromEdge(canvasPos2D, canvasRect, TotalMargin);
                    
                    // Use the minimum alpha (most restrictive)
                    view.CanvasGroup.alpha = Mathf.Min(alpha, edgeAlpha);
                }
                else if (useOffScreen && offScreenAlpha > 0f)
                {
                    ShowOffScreenIndicator(view, canvasPos2D, canvasRect, offScreenAlpha);
                }
                else
                {
                    view.Hide();
                }
            }
        }

        private void ShowOffScreenIndicator(HUDIndicatorView view, Vector2 canvasPos2D, Rect canvasRect, float alpha)
        {
            // Inset by the view's own extents so the whole box stays on screen, not just its pivot
            Rect boxRect = view.OffScreenRectTransform != null ? view.OffScreenRectTransform.rect : default;
            Vector2 onscreenPos = ClampToCanvasEdge(canvasPos2D, canvasRect,
                TotalMargin - boxRect.xMin, TotalMargin + boxRect.xMax,
                TotalMargin - boxRect.yMin, TotalMargin + boxRect.yMax);
            view.UpdateOffScreenPosition(onscreenPos);

            Vector2 arrowPos = ClampToCanvasEdge(canvasPos2D, canvasRect, m_ArrowMargin);
            view.UpdateOffScreenArrowPosition(arrowPos);

            // Calculate angle for arrow rotation (from view position to actual object position)
            Vector2 viewToObject = canvasPos2D - onscreenPos;
            float angle = Mathf.Atan2(viewToObject.y, viewToObject.x) * Mathf.Rad2Deg;
            view.SetArrowRotation(angle);

            view.ShowOffScreen();
            view.CanvasGroup.alpha = alpha;
        }

        private Vector2 ClampToCanvasEdge(Vector2 canvasPos, Rect canvasRect, float margin)
        {
            return ClampToCanvasEdge(canvasPos, canvasRect, margin, margin, margin, margin);
        }

        private Vector2 ClampToCanvasEdge(Vector2 canvasPos, Rect canvasRect,
            float leftInset, float rightInset, float bottomInset, float topInset)
        {
            // Calculate the center of the canvas
            Vector2 center = new Vector2(canvasRect.center.x, canvasRect.center.y);

            // Calculate direction from center to target
            Vector2 direction = (canvasPos - center).normalized;

            // Calculate canvas bounds with insets
            float left = canvasRect.xMin + leftInset;
            float right = canvasRect.xMax - rightInset;
            float bottom = canvasRect.yMin + bottomInset;
            float top = canvasRect.yMax - topInset;

            // A view larger than the canvas can't fit; pin that axis to the center
            if (left > right) left = right = center.x;
            if (bottom > top) bottom = top = center.y;

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

    }
}