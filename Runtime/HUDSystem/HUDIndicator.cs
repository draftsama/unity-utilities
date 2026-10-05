using System;
using System.Collections.Generic;
using UnityEngine;

namespace Modules.Utilities
{
    public class HUDIndicator : MonoBehaviour
    {

        [System.Serializable]

        public class HUDIndicatorData
        {
            [Header("OnScreen")]
            public bool m_UseOnScreen = true;
            public GameObject m_OnScreenPrefab;

            [HelpBox("If you use AutoSize will be require BoxCollider for calculation ui size",1)]
            public bool m_AutoSize = false;
            public BoxCollider m_BoxCollider;

            [Header("OffScreen")]

            public bool m_UseOffScreen = true;
            public GameObject m_OffScreenPrefab;

            public GameObject m_OffScreenArrowPrefab;

            [Tooltip("Keep the off-screen indicator fully visible regardless of the renderer's distance fade")]
            public bool m_OffScreenIgnoreDistanceFade = false;

        }

        [SerializeField] public bool m_Visible = true;
        [Tooltip("Renderers that draw this indicator. Leave empty to use every active HUDRenderer.")]
        [SerializeField] private HUDRenderer[] m_Renderers;

        [SerializeField] public HUDIndicatorData m_IndicatorData;

        [Tooltip("World-space offset from the transform, e.g. to float a label above a character's head")]
        public Vector3 m_WorldOffset = Vector3.zero;

        public Transform m_Transform { get; private set; }

        /// <summary>Raised when a renderer binds a (possibly pooled) view to this indicator.</summary>
        public event Action<HUDIndicatorView> ViewBound;
        /// <summary>Raised when a renderer releases this indicator's view back to its pool.</summary>
        public event Action<HUDIndicatorView> ViewUnbound;

        private static readonly List<HUDIndicator> s_Active = new List<HUDIndicator>();
        public static IReadOnlyList<HUDIndicator> ActiveIndicators => s_Active;

        internal static event Action<HUDIndicator> Registered;
        internal static event Action<HUDIndicator> Unregistered;

        private bool _started;
        private bool _registered;

        public bool IsRegistered => _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Static state survives Play Mode when domain reload is disabled
            s_Active.Clear();
            Registered = null;
            Unregistered = null;
        }

        void Awake()
        {
            m_Transform = transform;
        }

        void OnEnable()
        {
            // The first registration waits for Start so data assigned right after AddComponent is picked up
            if (_started)
            {
                Register();
            }
        }

        void Start()
        {
            _started = true;

            // Auto-assign BoxCollider if using AutoSize
            if (m_IndicatorData.m_AutoSize && m_IndicatorData.m_BoxCollider == null)
            {
                m_IndicatorData.m_BoxCollider = GetComponent<BoxCollider>();
            }

            Register();
        }

        void OnDisable()
        {
            Unregister();
        }

        private void Register()
        {
            if (_registered) return;
            _registered = true;
            s_Active.Add(this);
            Registered?.Invoke(this);
        }

        private void Unregister()
        {
            if (!_registered) return;
            _registered = false;
            s_Active.Remove(this);
            Unregistered?.Invoke(this);
        }

        /// <summary>
        /// World position the HUD tracks. Override to follow something other than this transform.
        /// </summary>
        public virtual Vector3 GetWorldPosition()
        {
            return m_Transform.position + m_WorldOffset;
        }

        /// <summary>
        /// Whether the given renderer should draw this indicator.
        /// </summary>
        public bool UsesRenderer(HUDRenderer renderer)
        {
            if (renderer == null) return false;
            if (m_Renderers == null || m_Renderers.Length == 0) return true;
            return Array.IndexOf(m_Renderers, renderer) >= 0;
        }

        /// <summary>
        /// Recreate the views after changing m_IndicatorData (prefabs or toggles) at runtime.
        /// </summary>
        public void RebuildViews()
        {
            if (!_registered) return;
            Unregister();
            Register();
        }

        public HUDRenderer[] GetRenderers()
        {
            if (m_Renderers != null && m_Renderers.Length > 0)
            {
                return m_Renderers;
            }

            var renderers = HUDRenderer.ActiveRenderers;
            var result = new HUDRenderer[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                result[i] = renderers[i];
            }
            return result;
        }

        public HUDIndicatorView GetView(HUDRenderer renderer)
        {
            if (renderer == null) return null;
            return renderer.GetIndicatorView(this);
        }

        public HUDIndicatorView[] GetAllViews()
        {
            var renderers = GetRenderers();
            var views = new HUDIndicatorView[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                views[i] = GetView(renderers[i]);
            }
            return views;
        }

        internal void NotifyViewBound(HUDIndicatorView view)
        {
            ViewBound?.Invoke(view);
        }

        internal void NotifyViewUnbound(HUDIndicatorView view)
        {
            ViewUnbound?.Invoke(view);
        }

    }
}
