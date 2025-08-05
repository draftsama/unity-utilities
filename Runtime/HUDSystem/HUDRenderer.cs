using System.Collections.Generic;
using UnityEngine;
namespace Modules.Utilities
{
    public class HUDRenderer : MonoBehaviour
    {

        public Camera m_Camera;

        public bool m_IsShow = true;

        public float m_Margin = 20f;
        public float m_ArrowMargin = 20f;


        public List<HUDIndicatorView> m_IndicatorViewList = new List<HUDIndicatorView>();

        private RectTransform _RectTransform;



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

            indicatorView.Initialize(indicator);

            m_IndicatorViewList.Add(indicatorView);


        }

        public void UnregisterIndicator(HUDIndicator indicator)
        {
            for (int i = m_IndicatorViewList.Count - 1; i >= 0; i--)
            {
                if (m_IndicatorViewList[i].m_Indicator == indicator)
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
                if (view.m_Indicator == indicator)
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

        void Update()
        {
            foreach (var view in m_IndicatorViewList)
            {
                // Check if the indicator GameObject is active
                bool isIndicatorActive = view.m_Indicator.gameObject.activeInHierarchy;

                // Check if the indicator should be shown based on m_IsShow flag
                bool shouldShow = view.m_Indicator.m_IsShow && m_IsShow;

                if (!isIndicatorActive || !shouldShow)
                {
                    view.Hide();
                    continue;
                }

                Vector3 worldPos = view.m_Indicator.m_Transform.position;
                Vector3 cameraToTarget = worldPos - m_Camera.transform.position;

                // Check if the target is in front of the camera
                bool isInFront = Vector3.Dot(cameraToTarget, m_Camera.transform.forward) > 0;

                if (!isInFront)
                {
                    view.Hide(); // Hide when behind camera
                    continue;
                }

                // Convert world position to screen position
                Vector3 screenPos = m_Camera.WorldToScreenPoint(worldPos);
                Vector3 canvasPos = _RectTransform.InverseTransformPoint(screenPos);

                // Get canvas rect bounds
                Rect canvasRect = _RectTransform.rect;

                // Check if the object is within the canvas bounds (with margin)
                bool isOnScreen = canvasPos.x >= (canvasRect.xMin + TotalMargin) && canvasPos.x <= (canvasRect.xMax - TotalMargin) &&
                                 canvasPos.y >= (canvasRect.yMin + TotalMargin) && canvasPos.y <= (canvasRect.yMax - TotalMargin) &&
                                 screenPos.z > 0;

                if (isOnScreen)
                {
                    // Show on-screen indicator
                    view.UpdateOnScreenPosition(new Vector2(canvasPos.x, canvasPos.y));
                    view.ShowOnScreen();
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


    }
}