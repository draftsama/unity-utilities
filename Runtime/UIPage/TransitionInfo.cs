#if ENABLE_UIPAGE_OLD
using System;
using UnityEngine;

namespace Modules.Utilities
{
    [Serializable]
    public class TransitionInfo
    {
        public enum TransitionType
        {
            Fade,
            CrossFade,
            Slide,
        }



        [SerializeField] public int m_Duration = 500;
        [SerializeField] public TransitionType m_Type;
        [SerializeField] public Color m_FadeColor = Color.black;

        [SerializeField] public Vector2 m_StartPosition;
        [SerializeField] public Vector2 m_EndPosition;
        
        [SerializeField] public Easing.Ease m_Ease = Easing.Ease.EaseInOutQuad;
    }

}




#endif