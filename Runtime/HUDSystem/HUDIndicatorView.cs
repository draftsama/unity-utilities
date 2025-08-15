using UnityEngine;
namespace Modules.Utilities
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class HUDIndicatorView : MonoBehaviour
    {

        public RectTransform RectTransform { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField] public HUDIndicator Indicator { get; private set; }

        public HUDRenderer Renderer { get; private set; }


        public RectTransform OnScreenRectTransform { get; private set; }
        public RectTransform OffScreenRectTransform { get; private set; }
        public RectTransform OffScreenArrowRectTransform { get; private set; }

        void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            CanvasGroup = GetComponent<CanvasGroup>();
            CanvasGroup.alpha = 0f; // Start with invisible
        }



        public void Initialize(HUDIndicator indicator, HUDRenderer renderer)
        {
            var data = indicator.m_IndicatorData;
            Renderer = renderer;
            if (data.m_UseOnScreen && data.m_OnScreenPrefab != null)
            {
                var onScreenView = Instantiate(data.m_OnScreenPrefab, RectTransform);
                OnScreenRectTransform = onScreenView.GetComponent<RectTransform>();
                OnScreenRectTransform.anchoredPosition = Vector2.zero;
                OnScreenRectTransform.localRotation = Quaternion.identity;
                OnScreenRectTransform.localScale = Vector3.one;
                onScreenView.SetActive(false);
            }

            if (data.m_UseOffScreen)
            {
                if (data.m_OffScreenPrefab != null)
                {
                    var offScreenView = Instantiate(data.m_OffScreenPrefab, RectTransform);
                    OffScreenRectTransform = offScreenView.GetComponent<RectTransform>();
                    OffScreenRectTransform.anchoredPosition = Vector2.zero;
                    OffScreenRectTransform.localRotation = Quaternion.identity;
                    OffScreenRectTransform.localScale = Vector3.one;

                    offScreenView.SetActive(false);
                }

                if (data.m_OffScreenArrowPrefab != null)
                {
                    var offScreenArrowView = Instantiate(data.m_OffScreenArrowPrefab, RectTransform);
                    OffScreenArrowRectTransform = offScreenArrowView.GetComponent<RectTransform>();
                    OffScreenArrowRectTransform.anchoredPosition = Vector2.zero;
                    OffScreenArrowRectTransform.localRotation = Quaternion.identity;
                    OffScreenArrowRectTransform.localScale = Vector3.one;
                    offScreenArrowView.SetActive(false);

                }
            }

            Indicator = indicator;
            RectTransform.localPosition = Vector3.zero;
            RectTransform.localRotation = Quaternion.identity;
            RectTransform.localScale = Vector3.one;


        }

        public void UpdateOnScreenPosition(Vector2 position)
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.anchoredPosition = position;
            }
        }

        public void UpdateOffScreenPosition(Vector2 position)
        {
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.anchoredPosition = position;
            }
        }

        public void UpdateOffScreenArrowPosition(Vector2 position)
        {
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.anchoredPosition = position;
            }
        }


        public void ShowOnScreen()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(true);
            }
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.gameObject.SetActive(false);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void ShowOffScreen()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.gameObject.SetActive(true);
            }
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.gameObject.SetActive(true);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void ShowOffScreenArrow()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.gameObject.SetActive(true);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void Hide()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.gameObject.SetActive(false);
            }
            CanvasGroup.alpha = 0f; // Hide the indicator
        }

        public void SetArrowRotation(float angleDegrees)
        {
            if (OffScreenArrowRectTransform != null)
            {
                OffScreenArrowRectTransform.rotation = Quaternion.AngleAxis(angleDegrees, Vector3.forward);
            }
        }

        /// <summary>
        /// Set the alpha transparency of the indicator
        /// </summary>
        /// <param name="alpha">Alpha value between 0 and 1</param>
        public void SetAlpha(float alpha)
        {
            CanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetOnScreenSize(Vector2 size)
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                OnScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
        }
        
        public void SetOffScreenSize(Vector2 size)
        {
            if (OffScreenRectTransform != null)
            {
                OffScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                OffScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
        }

        /// <summary>
        /// Hide only the OnScreen part due to overlap (keep CanvasGroup alpha intact)
        /// </summary>
        public void HideOnScreenByOverlap()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show OnScreen part when no longer overlapping
        /// </summary>
        public void ShowOnScreenFromOverlap()
        {
            if (OnScreenRectTransform != null)
            {
                OnScreenRectTransform.gameObject.SetActive(true);
            }
        }







    }


}
