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
            Indicator = indicator;
            Renderer = renderer;
            
            var data = indicator.m_IndicatorData;
            
            InitializeOnScreenView(data);
            InitializeOffScreenViews(data);
            
            SetupTransform();
        }

        private void InitializeOnScreenView(HUDIndicator.HUDIndicatorData data)
        {
            if (data.m_UseOnScreen && data.m_OnScreenPrefab != null)
            {
                var onScreenView = Instantiate(data.m_OnScreenPrefab, RectTransform);
                OnScreenRectTransform = onScreenView.GetComponent<RectTransform>();
                SetupChildTransform(OnScreenRectTransform);
                onScreenView.SetActive(false);
            }
        }

        private void InitializeOffScreenViews(HUDIndicator.HUDIndicatorData data)
        {
            if (!data.m_UseOffScreen) return;

            if (data.m_OffScreenPrefab != null)
            {
                var offScreenView = Instantiate(data.m_OffScreenPrefab, RectTransform);
                OffScreenRectTransform = offScreenView.GetComponent<RectTransform>();
                SetupChildTransform(OffScreenRectTransform);
                offScreenView.SetActive(false);
            }

            if (data.m_OffScreenArrowPrefab != null)
            {
                var offScreenArrowView = Instantiate(data.m_OffScreenArrowPrefab, RectTransform);
                OffScreenArrowRectTransform = offScreenArrowView.GetComponent<RectTransform>();
                SetupChildTransform(OffScreenArrowRectTransform);
                offScreenArrowView.SetActive(false);
            }
        }

        private void SetupChildTransform(RectTransform childTransform)
        {
            if (childTransform != null)
            {
                childTransform.anchoredPosition = Vector2.zero;
                childTransform.localRotation = Quaternion.identity;
                childTransform.localScale = Vector3.one;
            }
        }

        private void SetupTransform()
        {
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
    }
}
