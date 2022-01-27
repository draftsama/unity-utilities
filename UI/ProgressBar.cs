using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Modules.Utilities
{
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField] public RectTransform m_Fill;
        [SerializeField] public Type m_Type = Type.LeftToRight;
        [SerializeField] private float m_Progress;

        public enum Type
        {
            LeftToRight,
            RightToLeft,
            Center
        }

        void Start()
        {
         
        }

        public IObservable<Unit> LerpProgress(int _millisecond,float _progress,Easing.Ease _ease = Easing.Ease.EaseOutQuad)
        {
            return Observable.Create<Unit>(_oberver =>
            {

             IDisposable disposable =   LerpThread
                 .FloatLerp(_millisecond, m_Progress, _progress, _ease)
                 .Subscribe(SetProgress, _oberver.OnError, () =>
                {
                    _oberver.OnNext(default);
                    _oberver.OnCompleted();
                });
                
                return  Disposable.Create(()=>{disposable?.Dispose();});
            });
        }


        public void SetProgress(float _progress)
        {
            m_Progress = Mathf.Clamp01(_progress);
            if (m_Type == Type.LeftToRight)
            {
                var max = m_Progress;
                m_Fill.anchorMin = Vector2.zero;
                m_Fill.anchorMax = new Vector2(max, 1);
                m_Fill.offsetMin = new Vector2(0, m_Fill.offsetMin.y);
                m_Fill.offsetMax = new Vector2(0, m_Fill.offsetMax.y);
            }
            else if (m_Type == Type.RightToLeft)
            {
                var min = m_Progress;
                m_Fill.anchorMin = new Vector2(1 - min, 0);
                m_Fill.anchorMax = Vector2.one;
                m_Fill.offsetMin = new Vector2(0, m_Fill.offsetMin.y);
                m_Fill.offsetMax = new Vector2(0, m_Fill.offsetMax.y);
            }
            else if (m_Type == Type.Center)
            {
                var min = Mathf.Clamp01(0.5f - (m_Progress * 0.5f));
                var max = Mathf.Clamp01(0.5f + (m_Progress * 0.5f));

                m_Fill.anchorMin = new Vector2(min, 0);
                m_Fill.anchorMax = new Vector2(max, 1);
                m_Fill.offsetMin = new Vector2(0, m_Fill.offsetMin.y);
                m_Fill.offsetMax = new Vector2(0, m_Fill.offsetMax.y);
            }
        }
    }
}