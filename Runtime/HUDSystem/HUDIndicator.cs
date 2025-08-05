using UnityEngine;

namespace Modules.Utilities
{
    public class HUDIndicator : MonoBehaviour
    {

        [System.Serializable]

        public class HUDIndicatorData
        {
            public bool m_UseOnScreen = true;
            public GameObject m_OnScreenPrefab;

            public bool m_UseOffScreen = true;
            public GameObject m_OffScreenPrefab;

            public GameObject m_OffScreenArrowPrefab;
        }

        [SerializeField] public bool m_Visible = true;
        [SerializeField] private HUDRenderer[] m_Renderers;

        [SerializeField] public HUDIndicatorData m_IndicatorData;
        public Transform m_Transform { get; private set; }
        void Start()
        {
            m_Transform = transform;
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = FindObjectsByType<HUDRenderer>(FindObjectsSortMode.None);

            }

            foreach (var renderer in m_Renderers)
            {
                renderer.RegisterIndicator(this);
            }

        }

        public HUDRenderer[] GetRenderers()
        {
            return m_Renderers;
        }

        public HUDIndicatorView GetView(HUDRenderer renderer)
        {
            foreach (var view in renderer.m_IndicatorViewList)
            {
                if (view.m_Indicator == this)
                {
                    return view;
                }
            }
            return null;
        }

        public HUDIndicatorView[] GetAllViews()
        {
            var views = new HUDIndicatorView[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                views[i] = GetView(m_Renderers[i]);
            }
            return views;
        }


        void OnDestroy()
        {
            foreach (var renderer in m_Renderers)
            {
                renderer.UnregisterIndicator(this);
            }
        }



    }
}
