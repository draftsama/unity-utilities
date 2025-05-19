using System.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

        CancellationTokenSource _cts = new CancellationTokenSource();

        void Start()
        {
            SetProgress(m_StartProgress);
        }
        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }



        public IUniTaskAsyncEnumerable<float> LerpProgressAsyncEnumerable(int _millisecond, float _progress, Easing.Ease _ease = Easing.Ease.Linear, CancellationToken _token = default)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _token);

            var asyncEnumerable = LerpThread.FloatLerpAsyncEnumerable(_millisecond, Progress, _progress, _ease, _token: linkedCts.Token);
            asyncEnumerable.ForEachAsync(_ =>
            {
                SetProgress(_);
            }, linkedCts.Token).Forget();
            return asyncEnumerable;

        }
        public void StopLerp()
        {
            _cts?.Cancel();
        }



        public void SetProgress(float _progress)
        {
            if (m_Fill == null) return;
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

#if UNITY_EDITOR

namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(ProgressBar))]
    public class ProgressBarEditor : UnityEditor.Editor
    {
        private ProgressBar _Instance;
        private SerializedProperty _StartProgressProp;
        private void OnEnable()
        {
            _Instance = target as ProgressBar;

            _StartProgressProp = serializedObject.FindProperty("m_StartProgress");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            if (GUI.changed)
            {
                _StartProgressProp.floatValue = Mathf.Clamp01(_StartProgressProp.floatValue);

                _Instance.SetProgress(_StartProgressProp.floatValue);
                EditorUtility.SetDirty(_Instance);
                serializedObject.ApplyModifiedProperties();
            }
        }

    }
}

#endif
