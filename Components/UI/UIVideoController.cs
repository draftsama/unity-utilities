using System.Threading;
using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] public bool m_PrepareOnAwake = true;
        [SerializeField] public bool m_Loop = false;
        [SerializeField] public bool m_FadeVideo = false;
        [SerializeField] public bool m_FadeAudio = false;

        [SerializeField] public int m_FadeTime = 500;
        [SerializeField] public float m_Progress = 0f;

        private RawImage _Preview;
        private VideoPlayer _VideoPlayer;
        private CanvasGroup _CanvasGroup;
        private Action<Unit> _OnEnd;
        private UnityEvent _OnEndEventHandler = new UnityEvent();

        private bool _Stoping = false;
        private bool _IgnoreFadeOut = false;
        public enum PathType
        {
            StreamAssets,
            Relative,
            Absolute
        }

        public VideoPlayer m_VideoPlayer => _VideoPlayer;
        public bool m_IsPlaying => _VideoPlayer != null && _VideoPlayer.isPlaying;


        private CancellationTokenSource _Cts = new CancellationTokenSource();

        private void Awake()
        {
            _Preview = GetComponent<RawImage>();
            _VideoPlayer = GetComponent<VideoPlayer>();
            _CanvasGroup = GetComponent<CanvasGroup>();
            _VideoPlayer.isLooping = false;
            _VideoPlayer.playOnAwake = false;
            _CanvasGroup.SetAlpha(0);

            if (m_PrepareOnAwake)
            {
                SetupURL(m_FileName, m_PathType, m_FolderName);
                _VideoPlayer.Prepare();
            }
        }

        public void SetupURL(string _filename, PathType _pathType = PathType.Relative, string _foldername = "Resources")
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



        private void OnDisable()
        {

            if (_Cts != null)
            {
                _Cts.Cancel();
                _Cts.Dispose();
                _Cts = null;
            }
        }

        void Start()
        {

            if (m_PlayOnAwake) PlayAsync().Forget();
        }

        //------------------------------------ Public Method ----------------------------------

        public async UniTaskVoid PlayAsync(bool _ignoreFadeIn = false, bool _ignoreFadeOut = false, CancellationToken _token = default)
        {

            if (_VideoPlayer.isPlaying)
                return;

            // Debug.Log("Play Video");

            if (_token == default)
            {
                if (_Cts != null)
                {
                    _Cts.Cancel();
                    _Cts.Dispose();
                }
                _Cts = new CancellationTokenSource();
                _token = _Cts.Token;
            }
            m_Progress = 0f;
            _IgnoreFadeOut = _ignoreFadeOut;
            float fadeInProgress = 0f;
            float fadeOutProgress = 0f;

            try
            {

                if (!_VideoPlayer.isPrepared)
                {
                    SetupURL(m_FileName, m_PathType, m_FolderName);
                    _VideoPlayer.Prepare();
                    await UniTask.WaitUntil(() => _VideoPlayer.isPrepared, cancellationToken: _token);

                }
                _VideoPlayer.frame = 0;
                _VideoPlayer.Play();

                await UniTask.WaitUntil(() => _VideoPlayer.isPlaying, cancellationToken: _token);

                while (!_token.IsCancellationRequested && (_VideoPlayer.isPlaying || m_Loop))
                {

                    m_Progress = (float)_VideoPlayer.time / (float)_VideoPlayer.length;
                    _Preview.texture = _VideoPlayer.texture;


                    if (_VideoPlayer.time <= (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) && !_Stoping)
                    {
                        fadeInProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        var valueProgress = Mathf.Clamp01(EasingFormula.EasingFloat(Easing.Ease.EaseInOutQuad, 0f, 1f, fadeInProgress));

                        //fade in
                        _CanvasGroup.SetAlpha(m_FadeVideo && !_ignoreFadeIn ? valueProgress : 1);
                        _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? valueProgress : 1);

                    }
                    else if (!m_Loop && _VideoPlayer.time >= _VideoPlayer.length - (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) || _Stoping)
                    {
                        fadeOutProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        var valueProgress = Mathf.Clamp01(EasingFormula.EasingFloat(Easing.Ease.EaseInOutQuad, 1f, 0f, fadeOutProgress));
                        //fade out
                        _CanvasGroup.SetAlpha(m_FadeVideo && !_IgnoreFadeOut ? valueProgress : 0);
                        _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? valueProgress : 0);

                        if (fadeOutProgress >= 1f)
                            break;
                    }
                    else
                    {
                        //playing
                        _CanvasGroup.SetAlpha(1);
                        _VideoPlayer.SetDirectAudioVolume(0, 1);
                    }


                    if (!_VideoPlayer.isPlaying && m_Loop)
                    {

                        // Debug.Log("Video Loop.");
                        _VideoPlayer.frame = 0;
                        _VideoPlayer.Play();

                        //skip fade in next loop
                        _ignoreFadeIn = true;
                    }


                    await UniTask.Yield(PlayerLoopTiming.Update, _token);
                }



            }
            catch (System.OperationCanceledException)
            {
                // Debug.Log("Video Canceled.");

            }


            _Stoping = false;
            _VideoPlayer.Stop();
            _CanvasGroup.SetAlpha(0);
            // Debug.Log("Video End.");

        }

        public void Pause()
        {
            if (_VideoPlayer && !_VideoPlayer.isPaused)
                _VideoPlayer.Pause();
        }
        public void UnPause()
        {
            if (_VideoPlayer && _VideoPlayer.isPaused)
                _VideoPlayer.Play();
        }

        public void Stop(bool _ignoreFadeOut = false)
        {

            if (!_VideoPlayer || !_VideoPlayer.isPlaying)
                return;

            // Debug.Log("Stop Video");
            _IgnoreFadeOut = _ignoreFadeOut;
            _Stoping = true;

        }


        public void Seek(int _frame)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log("Video Not Prepared.");
                return;
            }
            if (!_VideoPlayer.isPlaying)
                _VideoPlayer.Play();

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

        public IUniTaskAsyncEnumerable<AsyncUnit> OnEndAsyncEnumerable(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnEndEventHandler, _token);
        }


    }
}