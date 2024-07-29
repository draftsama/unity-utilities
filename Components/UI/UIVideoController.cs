using System.Threading;
using System;
using System.IO;
using Cysharp.Threading.Tasks;
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

        [SerializeField] public ContentSizeMode m_ContentSizeMode = ContentSizeMode.None;

        [SerializeField] public bool m_FadeVideo = false;
        [SerializeField] public bool m_FadeAudio = false;

        [SerializeField] public int m_FadeTime = 500;
        [SerializeField] public float m_Progress = 0f;

        private RawImage _Preview;
        private VideoPlayer _VideoPlayer;
        private CanvasGroup _CanvasGroup;
        private UnityEvent _OnEndEventHandler = new UnityEvent();
        private RectTransform _RectTransform;
        private bool _Stoping = false;
        private bool _IgnoreFadeOut = false;


        public VideoPlayer m_VideoPlayer => _VideoPlayer;
        public bool m_IsPlaying => _VideoPlayer != null && _VideoPlayer.isPlaying;
        private AspectRatioFitter _AspectRatioFitter;

        private CancellationTokenSource _Cts = new CancellationTokenSource();


        private void Awake()
        {

            _RectTransform = GetComponent<RectTransform>();
            _Preview = GetComponent<RawImage>();
            _VideoPlayer = GetComponent<VideoPlayer>();
            _CanvasGroup = GetComponent<CanvasGroup>();
            _VideoPlayer.isLooping = false;
            _VideoPlayer.playOnAwake = false;
            _CanvasGroup.SetAlpha(0);

            _VideoPlayer.prepareCompleted += (source) =>
            {
                switch (m_ContentSizeMode)
                {

                    case ContentSizeMode.NativeSize:
                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, source.width);
                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, source.height);
                        break;
                    case ContentSizeMode.WidthControlHeight:
                        _AspectRatioFitter = GetComponent<AspectRatioFitter>();
                        if (_AspectRatioFitter == null) _AspectRatioFitter = gameObject.AddComponent<AspectRatioFitter>();

                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

                        break;
                    case ContentSizeMode.HeightControlWidth:
                        _AspectRatioFitter = GetComponent<AspectRatioFitter>();
                        if (_AspectRatioFitter == null) _AspectRatioFitter = gameObject.AddComponent<AspectRatioFitter>();

                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                        break;


                }

            };



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


        public async UniTask PlayAsync(int _frame = 0, bool _ignoreFadeIn = false, bool _ignoreFadeOut = false, bool _forcePlay = false, CancellationToken _token = default)
        {

            if (_VideoPlayer.isPlaying && !_forcePlay)
                return;

            _Cts?.Cancel();
            _Cts?.Dispose();
            _Cts = new CancellationTokenSource();

            _VideoPlayer.Stop();
            await UniTask.Yield();


            if (_token == default)
                _token = _Cts.Token;
            else
                _Cts.AddTo(_token);


            m_Progress = 0f;
            _IgnoreFadeOut = _ignoreFadeOut;
            float fadeInProgress = 0f;
            float fadeOutProgress = 0f;

            try
            {
                _token.ThrowIfCancellationRequested();


                if (!_VideoPlayer.isPrepared)
                {
                    SetupURL(m_FileName, m_PathType, m_FolderName);


                    _VideoPlayer.Prepare();
                    await UniTask.WaitUntil(() => _VideoPlayer.isPrepared, cancellationToken: _token);

                }
                Debug.Log("Play Video : " + _VideoPlayer.url);

                _VideoPlayer.frame = 0;

                _VideoPlayer.Play();

                await UniTask.WaitUntil(() => _VideoPlayer.isPlaying, cancellationToken: _token);

                while (!_token.IsCancellationRequested)
                {
                    if (_VideoPlayer == null)
                        break;

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
                        {
                            Debug.Log("Video End :" + _VideoPlayer.url);
                            _OnEndEventHandler.Invoke();
                            break;
                        }
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
                        _VideoPlayer.frame = _frame;
                        _VideoPlayer.Play();

                        //skip fade in next loop
                        _ignoreFadeIn = true;
                    }


                    await UniTask.Yield(PlayerLoopTiming.Update);
                }



            }
            catch (System.OperationCanceledException)
            {
                // Debug.Log("Video Canceled.");

            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            finally
            {
                _Stoping = false;
                if (_VideoPlayer != null) _VideoPlayer.Stop();
                if (_CanvasGroup != null) _CanvasGroup.SetAlpha(0);



            }




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

            //  Debug.Log($"Stop Video :{m_FileName} ");
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


        public IUniTaskAsyncEnumerable<AsyncUnit> OnEndAsyncEnumerable(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnEndEventHandler, _token);
        }


    }
}