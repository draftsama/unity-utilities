using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Modules.Utilities
{
    [RequireComponent((typeof(CanvasGroup)))]
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(VideoPlayer))]
    public class UIVideoController : MonoBehaviour
    {
        [SerializeField] public string m_FileName;
        [SerializeField] public string m_FolderName = "Resources";
        [SerializeField] public PathType m_PathType = PathType.Relative;

        [SerializeField] public bool m_PlayOnAwake = true;
        [SerializeField] public bool m_Loop = false;

        private RawImage _Preview;
        private VideoPlayer _VideoPlayer;
        private CanvasGroup _CanvasGroup;

        private Action<Unit> _OnEnd;

        public enum PathType
        {
            StreamAssets,
            Relative,
            Absolute
        }

        private void Awake()
        {
            _Preview = GetComponent<RawImage>();
            _VideoPlayer = GetComponent<VideoPlayer>();
            _CanvasGroup = GetComponent<CanvasGroup>();
            _VideoPlayer.isLooping = false;
            _VideoPlayer.playOnAwake = false;
            _CanvasGroup.SetAlpha(0);
            var filePath = string.Empty;

            if (m_PathType == PathType.StreamAssets)
            {
                if (string.IsNullOrEmpty(m_FolderName))
                    filePath = Path.Combine(Application.streamingAssetsPath, m_FileName);
                else
                    filePath = Path.Combine(Application.streamingAssetsPath, m_FolderName, m_FileName);
            }
            else if (m_PathType == PathType.Relative)
            {
                if (string.IsNullOrEmpty(m_FolderName))
                    filePath = Path.Combine(Environment.CurrentDirectory, m_FileName);
                else
                    filePath = Path.Combine(Environment.CurrentDirectory, m_FolderName, m_FileName);
            }
            else if (m_PathType == PathType.Absolute)
            {
                filePath = Path.Combine(m_FolderName, m_FileName);
            }


            _VideoPlayer.url = filePath;
            _VideoPlayer.renderMode = VideoRenderMode.APIOnly;
        }

        private void OnEnable()
        {
            _VideoPlayer.started += VideoPlayerOnstarted;
            _VideoPlayer.loopPointReached += VideoPlayerOnloopPointReached;
        }

        private void VideoPlayerOnstarted(VideoPlayer _source)
        {
            _CanvasGroup.SetAlpha(1);
        }

        private void VideoPlayerOnloopPointReached(VideoPlayer _source)
        {
            //end video
            if (m_Loop) _VideoPlayer.Play();
            else
            {
                _CanvasGroup.SetAlpha(0);
                _OnEnd?.Invoke(default);
            }
        }


        private void OnDisable()
        {
            _VideoPlayer.started -= VideoPlayerOnstarted;
            _VideoPlayer.loopPointReached -= VideoPlayerOnloopPointReached;
        }

        void Start()
        {
            Observable.EveryUpdate().Where(_ => _VideoPlayer.isPlaying).Subscribe(_ =>
            {
                _Preview.texture = _VideoPlayer.texture;
            }).AddTo(this);
            if (m_PlayOnAwake) _VideoPlayer.Play();
        }

//------------------------------------ Public Method ----------------------------------
        public void Play()
        {
            if (!_VideoPlayer.isPlaying)
                _VideoPlayer.Play();
        }

        public void Pause()
        {
            if (!_VideoPlayer.isPaused)
                _VideoPlayer.Pause();
        }

        public void Stop()
        {
            if (_VideoPlayer.isPlaying)
            {
                _VideoPlayer.Stop();
                _CanvasGroup.SetAlpha(0);
            }
        }

        public void Seek(int _frame)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log("Video Not Prepared.");
                return;
            }

            _VideoPlayer.frame = _frame;
        }

        public void Seek(float _progress)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log("Video Not Prepared.");
                return;
            }

            _progress = Mathf.Clamp(_progress, 0, 1);
            _VideoPlayer.time = _progress * _VideoPlayer.length;
        }

        public IObservable<Unit> OnEndAsObservable()
        {
            return Observable.FromEvent<Unit>(_event => _OnEnd += _event,
                _event => _OnEnd -= _event);
        }
    }
}