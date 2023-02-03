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
        [SerializeField] public Direction m_Direction = Direction.Horizontal;

        [SerializeField] public Style m_Style = Style.LeftToRight;
        public float Progress { get; private set; }

        [SerializeField] private float m_StartProgress;

        public enum Direction
        {
            Horizontal, Vertical
        }

        public enum Style
        {
            LeftToRight,
            RightToLeft,
            Center
        }

        void Start()
        {
            SetProgress(m_StartProgress);
        }

        public IObservable<Unit> LerpProgress(int _millisecond, float _progress, Easing.Ease _ease = Easing.Ease.EaseOutQuad)
        {
            return Observable.Create<Unit>(_oberver =>
            {

                IDisposable disposable = LerpThread
                    .FloatLerp(_millisecond, Progress, _progress, _ease)
                    .Subscribe(SetProgress, _oberver.OnError, () =>
                   {
                       _oberver.OnNext(default);
                       _oberver.OnCompleted();
                   });

                return Disposable.Create(() => { disposable?.Dispose(); });
            });
        }

        private void OnValidate()
        {
            if (m_Fill != null)
                SetProgress(m_StartProgress);
        }
        public void SetProgress(float _progress)
        {
            Progress = Mathf.Clamp01(_progress);

            if (m_Direction == Direction.Horizontal)
            {
                if (m_Style == Style.LeftToRight)
                {
                    var max = Progress;
                    m_Fill.anchorMin = Vector2.zero;
                    m_Fill.anchorMax = new Vector2(max, 1);

                }
                else if (m_Style == Style.RightToLeft)
                {
                    var min = Progress;
                    m_Fill.anchorMin = new Vector2(1 - min, 0);
                    m_Fill.anchorMax = Vector2.one;

                }
                else if (m_Style == Style.Center)
                {
                    var min = Mathf.Clamp01(0.5f - (Progress * 0.5f));
                    var max = Mathf.Clamp01(0.5f + (Progress * 0.5f));

                    m_Fill.anchorMin = new Vector2(min, 0);
                    m_Fill.anchorMax = new Vector2(max, 1);

                }
                m_Fill.offsetMin = Vector2.up * m_Fill.offsetMin.y;
                m_Fill.offsetMax = Vector2.up * m_Fill.offsetMax.y;
            }
            else
            {
                if (m_Style == Style.LeftToRight)
                {
                    var max = Progress;
                    m_Fill.anchorMin = Vector2.zero;
                    m_Fill.anchorMax = new Vector2(1, max);

                }
                else if (m_Style == Style.RightToLeft)
                {
                    var min = Progress;
                    m_Fill.anchorMin = new Vector2(0, 1 - min);
                    m_Fill.anchorMax = Vector2.one;

                }
                else if (m_Style == Style.Center)
                {
                    var min = Mathf.Clamp01(0.5f - (Progress * 0.5f));
                    var max = Mathf.Clamp01(0.5f + (Progress * 0.5f));

                    m_Fill.anchorMin = new Vector2(0, min);
                    m_Fill.anchorMax = new Vector2(1, max);

                }

                m_Fill.offsetMin = Vector2.right * m_Fill.offsetMin.y;
                m_Fill.offsetMax = Vector2.right * m_Fill.offsetMax.y;
            }
        }
    }
}