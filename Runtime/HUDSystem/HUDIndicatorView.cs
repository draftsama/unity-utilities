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


        RectTransform _OnScreenRectTransform;
        RectTransform _OffScreenRectTransform;
        RectTransform _OffScreenArrowRectTransform;

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
                _OnScreenRectTransform = onScreenView.GetComponent<RectTransform>();
                _OnScreenRectTransform.anchoredPosition = Vector2.zero;
                _OnScreenRectTransform.localRotation = Quaternion.identity;
                _OnScreenRectTransform.localScale = Vector3.one;
                onScreenView.SetActive(false);
            }

            if (data.m_UseOffScreen)
            {
                if (data.m_OffScreenPrefab != null)
                {
                    var offScreenView = Instantiate(data.m_OffScreenPrefab, RectTransform);
                    _OffScreenRectTransform = offScreenView.GetComponent<RectTransform>();
                    _OffScreenRectTransform.anchoredPosition = Vector2.zero;
                    _OffScreenRectTransform.localRotation = Quaternion.identity;
                    _OffScreenRectTransform.localScale = Vector3.one;

                    offScreenView.SetActive(false);
                }

                if (data.m_OffScreenArrowPrefab != null)
                {
                    var offScreenArrowView = Instantiate(data.m_OffScreenArrowPrefab, RectTransform);
                    _OffScreenArrowRectTransform = offScreenArrowView.GetComponent<RectTransform>();
                    _OffScreenArrowRectTransform.anchoredPosition = Vector2.zero;
                    _OffScreenArrowRectTransform.localRotation = Quaternion.identity;
                    _OffScreenArrowRectTransform.localScale = Vector3.one;
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
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.anchoredPosition = position;
            }
        }

        public void UpdateOffScreenPosition(Vector2 position)
        {
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.anchoredPosition = position;
            }
        }

        public void UpdateOffScreenArrowPosition(Vector2 position)
        {
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.anchoredPosition = position;
            }
        }


        public void ShowOnScreen()
        {
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.gameObject.SetActive(true);
            }
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.gameObject.SetActive(false);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void ShowOffScreen()
        {
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.gameObject.SetActive(true);
            }
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.gameObject.SetActive(true);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void ShowOffScreenArrow()
        {
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.gameObject.SetActive(true);
            }
            // Alpha will be set by distance calculation in HUDRenderer
        }

        public void Hide()
        {
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.gameObject.SetActive(false);
            }
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.gameObject.SetActive(false);
            }
            CanvasGroup.alpha = 0f; // Hide the indicator
        }

        public void SetArrowRotation(float angleDegrees)
        {
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.rotation = Quaternion.AngleAxis(angleDegrees, Vector3.forward);
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
            if (_OnScreenRectTransform != null)
            {
                _OnScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                _OnScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
        }
        
        public void SetOffScreenSize(Vector2 size)
        {
            if (_OffScreenRectTransform != null)
            {
                _OffScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                _OffScreenRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
        }







    }


}
