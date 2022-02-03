using System;
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
        [SerializeField] public bool m_FadeAnimation = false;
        [SerializeField] public int m_FadeTime = 500;

        private RawImage _Preview;
        private VideoPlayer _VideoPlayer;
        private CanvasGroup _CanvasGroup;
        private Action<Unit> _OnEnd;

        private IDisposable _FadeDisposable;

        private bool _Stoping = false;
        public enum PathType
        {
            StreamAssets,
            Relative,
            Absolute
        }

        public VideoPlayer m_VideoPlayer => _VideoPlayer;

        private void Awake()
        {
            _Preview = GetComponent<RawImage>();
            _VideoPlayer = GetComponent<VideoPlayer>();
            _CanvasGroup = GetComponent<CanvasGroup>();
            _VideoPlayer.isLooping = false;
            _VideoPlayer.playOnAwake = false;
            _CanvasGroup.SetAlpha(0);
            
            SetupURL(m_FileName, m_PathType, m_FolderName);

        }

        public void SetupURL(string _filename,PathType _pathType = PathType.Relative,string _foldername = "Resources")
        {
            m_FileName = _filename;
            m_PathType = _pathType;
            m_FolderName = _foldername;
            
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
            SetVideoAlpha(1);
        }

        private void VideoPlayerOnloopPointReached(VideoPlayer _source)
        {
            //end video
            if (m_Loop) _VideoPlayer.Play();
            else
            {
                if(!_Stoping)
                  SetVideoAlpha(0);
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
                _Stoping = true;
                SetVideoAlpha(0,_onCompleted: () =>
                {
                    _VideoPlayer.Stop();
                    _Stoping = false;
                });
            }
        }

        private void  SetVideoAlpha(float _alpha ,bool _force = false,Action _onCompleted = null)
        {
            _FadeDisposable?.Dispose();
            if (m_FadeAnimation && !_force)
                _FadeDisposable =  _CanvasGroup.LerpAlpha(m_FadeTime, _alpha,true,_onCompleted).AddTo(this);
            else
            {
                _CanvasGroup.SetAlpha(_alpha);
                _onCompleted?.Invoke();
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