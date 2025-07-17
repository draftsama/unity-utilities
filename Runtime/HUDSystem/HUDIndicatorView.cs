using UnityEngine;
namespace Modules.Utilities
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class HUDIndicatorView : MonoBehaviour
    {


        public RectTransform m_RectTransform { get; private set; }
        public CanvasGroup m_CanvasGroup { get; private set; }

        public HUDIndicator m_Indicator { get; private set; }


        RectTransform _OnScreenRectTransform;
        RectTransform _OffScreenRectTransform;
        RectTransform _OffScreenArrowRectTransform;

        void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_CanvasGroup = GetComponent<CanvasGroup>();
            m_CanvasGroup.alpha = 0f; // Start with invisible
        }

        public void Initialize(HUDIndicator indicator)
        {
            var data = indicator.m_IndicatorData;

            if (data.m_UseOnScreen && data.m_OnScreenPrefab != null)
            {
                var onScreenView = Instantiate(data.m_OnScreenPrefab, m_RectTransform);
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
                    var offScreenView = Instantiate(data.m_OffScreenPrefab, m_RectTransform);
                    _OffScreenRectTransform = offScreenView.GetComponent<RectTransform>();
                    _OffScreenRectTransform.anchoredPosition = Vector2.zero;
                    _OffScreenRectTransform.localRotation = Quaternion.identity;
                    _OffScreenRectTransform.localScale = Vector3.one;

                    offScreenView.SetActive(false);
                }

                if (data.m_OffScreenArrowPrefab != null)
                {
                    var offScreenArrowView = Instantiate(data.m_OffScreenArrowPrefab, m_RectTransform);
                    _OffScreenArrowRectTransform = offScreenArrowView.GetComponent<RectTransform>();
                    _OffScreenArrowRectTransform.anchoredPosition = Vector2.zero;
                    _OffScreenArrowRectTransform.localRotation = Quaternion.identity;
                    _OffScreenArrowRectTransform.localScale = Vector3.one;
                    offScreenArrowView.SetActive(false);

                }
            }

            m_Indicator = indicator;
            m_RectTransform.localPosition = Vector3.zero;
            m_RectTransform.localRotation = Quaternion.identity;
            m_RectTransform.localScale = Vector3.one;


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
            m_CanvasGroup.alpha = 1f; // Show the indicator
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
            m_CanvasGroup.alpha = 1f; // Show the indicator
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
            m_CanvasGroup.alpha = 1f; // Show the indicator
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
            m_CanvasGroup.alpha = 0f; // Hide the indicator
        }

        public void SetArrowRotation(float angleDegrees)
        {
            if (_OffScreenArrowRectTransform != null)
            {
                _OffScreenArrowRectTransform.rotation = Quaternion.AngleAxis(angleDegrees, Vector3.forward);
            }
        }







    }


}
