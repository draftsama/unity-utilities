using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;


namespace Modules.Utilities
{
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoController : MonoBehaviour
    {
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        [SerializeField] public string m_FileName;
        [SerializeField] public string m_FolderName = "Resources";
        [SerializeField] public PathType m_PathType = PathType.Relative;


        [SerializeField] public VideoStartMode m_StartMode = VideoStartMode.None;
        [SerializeField] public bool m_Loop = false;
        [SerializeField] public bool m_FadeVideo = false;
        [SerializeField] public bool m_FadeAudio = false;

        [SerializeField] public int m_FadeTime = 500;

        [SerializeField][ReadOnlyField] public float m_Progress = 0f;
        [SerializeField] public bool m_KeepLastframe = false;

        [SerializeField] public VideoOutputType m_OutputType = VideoOutputType.RawImage;


        [SerializeField][HideInInspector] private RawImage _RawImage;
        [SerializeField][HideInInspector] private CanvasGroup _CanvasGroup;
        [SerializeField][HideInInspector] private AspectRatioFitter _AspectRatioFitter;
        [SerializeField][HideInInspector] private bool _PlayWithParentShow = false;
        [SerializeField][HideInInspector] private CanvasGroup _ParentCanvasGroup;
        [SerializeField][HideInInspector] private float _CanvasGroupThreshold = 0.1f;


        [SerializeField][HideInInspector] private MeshFilter _MeshFilter;
        [SerializeField][HideInInspector] private MeshRenderer _MeshRenderer;
        [SerializeField][HideInInspector] private Material _Material;

        [SerializeField][HideInInspector] private ContentSizeMode _ContentSizeMode = ContentSizeMode.None;


        private VideoPlayer _VideoPlayer;
        private UnityEvent _OnEndEventHandler = new UnityEvent();

        private bool _Stopping = false;
        private bool _IgnoreFadeOut = false;
        private bool _IsPlayStarting = false;


        public VideoPlayer m_VideoPlayer => _VideoPlayer;
        public bool m_IsPlaying { private set; get; } = false;
        public bool m_IsPrepared { private set; get; } = false;

        CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource _LoopCancellationTokenSource = new CancellationTokenSource();

        RectTransform _RectTransform;
        Transform _Transform;

        private bool _IsResume = false;

        private float _FadeInProgress = 0f;
        private float _FadeOutProgress = 0f;
        private void OnDisable()
        {
            if (_VideoPlayer != null)
            {
                _IsResume = m_IsPlaying;
                if (_VideoPlayer.isPlaying) _VideoPlayer.Pause();
                _VideoPlayer.targetTexture?.Release();
                _VideoPlayer.targetTexture = null;
            }

            _CancellationTokenSource.Cancel();
            _CancellationTokenSource.Dispose();
            _CancellationTokenSource = new CancellationTokenSource();

            _LoopCancellationTokenSource.Cancel();
            _LoopCancellationTokenSource.Dispose();
            _LoopCancellationTokenSource = new CancellationTokenSource();

        }



        private async void OnEnable()
        {
            Init();
            if (_IsResume)
            {
                PlayAsync(_resume: true).Forget();
            }
            else if (m_StartMode != VideoStartMode.None)
            {
                if (SetupURL(m_FileName, m_PathType, m_FolderName))
                {

                    if (string.IsNullOrEmpty(_VideoPlayer.url))
                    {
                        //throw exception if url is empty
                        throw new Exception($"[{gameObject.name}]  Video URL is empty.");
                    }

                    _VideoPlayer.Prepare();
                    await UniTask.WaitUntil(() => _VideoPlayer.isPrepared,
                        cancellationToken: _CancellationTokenSource.Token);

                    m_IsPrepared = true;
                    if (m_StartMode == VideoStartMode.AutoPlay)
                    {
                        if (!_PlayWithParentShow)
                        {
                            PlayAsync().Forget();
                        }
                    }
                    else if (m_StartMode == VideoStartMode.FirstFrameReady)
                    {
                        await PrepareFirstFrame();
                    }
                }
            }


            if (m_StartMode == VideoStartMode.AutoPlay && _PlayWithParentShow && _ParentCanvasGroup != null)
            {
                // Use _CancellationTokenSource.Token so this loop is cancelled on OnDisable,
                // preventing multiple loops from accumulating across enable/disable cycles.
                var loopToken = _LoopCancellationTokenSource.Token;
                try
                {
                    UniTaskAsyncEnumerable.EveryUpdate().ForEachAsync(_ =>
                    {


                        if (gameObject.activeInHierarchy == false)
                            return;


                        if (_ParentCanvasGroup.alpha >= _CanvasGroupThreshold)
                        {
                            // Use m_IsPlaying (actual state) instead of local flag to avoid
                            // desync when video ends naturally or stop fade-out is in progress.
                            if (!m_IsPlaying && !_Stopping && !_IsPlayStarting)
                            {
                                PlayAsync().Forget();
                            }
                        }
                        else
                        {
                            if (m_IsPlaying && !_Stopping)
                            {
                                Stop();
                            }
                        }
                    }, loopToken).Forget();
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Parent CanvasGroup monitoring canceled.");
                }
                catch (Exception)
                {
                    Debug.Log("Parent CanvasGroup monitoring error, likely due to missing reference. Stopping monitoring.");
                }
            }
        }


        private void OnDestroy()
        {
            _CancellationTokenSource?.Cancel();
            _CancellationTokenSource?.Dispose();
            _CancellationTokenSource = null;

            _LoopCancellationTokenSource?.Cancel();
            _LoopCancellationTokenSource?.Dispose();
            _LoopCancellationTokenSource = null;
        }

        void Start()
        {
        }


        public async UniTask<bool> Prepare(CancellationToken _token = default)
        {
            if (!_VideoPlayer.isPrepared || !m_IsPrepared)
            {
                SetupURL(m_FileName, m_PathType, m_FolderName);
                if (string.IsNullOrEmpty(_VideoPlayer.url))
                {
                    Debug.Log("Video URL is empty, please check the file path.");
                    return false;
                }

                _VideoPlayer.Prepare();
                await UniTask.WaitUntil(() => _VideoPlayer.isPrepared, cancellationToken: _token);
                m_IsPrepared = true;

            }
            return true;

        }

        public async UniTask PrepareFirstFrame()
        {
            await Prepare();
            if (_VideoPlayer == null || !_VideoPlayer.isPrepared)
            {
                return;
            }

            _VideoPlayer.Stop();
            await UniTask.NextFrame();
            _VideoPlayer.SetDirectAudioVolume(0, 0);
            _VideoPlayer.Play();

            await UniTask.WaitUntil(() => _VideoPlayer.isPlaying,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            _VideoPlayer.frame = 1;
            await UniTask.WaitUntil(() => _VideoPlayer.texture != null,
             cancellationToken: this.GetCancellationTokenOnDestroy());
            ApplyTexture(_VideoPlayer.texture);
            ApplyAlpha(1);
            _VideoPlayer.Pause();
        }

        public bool SetupFullURL(string _filePath)
        {
            if (_filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                _filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                // Auto-detect URL by prefix
                m_PathType = PathType.URL;
                m_FolderName = string.Empty;
                return SetupURL(_filePath, PathType.URL, string.Empty);
            }

            m_FileName = Path.GetFileName(_filePath);
            m_PathType = PathType.Absolute;
            m_FolderName = Path.GetDirectoryName(_filePath);


            return SetupURL(m_FileName, m_PathType, m_FolderName);
        }

        public bool SetupURL(string _filename, PathType _pathType = PathType.Relative, string _foldername = "Resources")
        {

            if (string.IsNullOrEmpty(_filename))
            {
                Debug.Log($"[{name}] Video filename is empty.");
                return false;
            }


            m_FileName = _filename;
            m_PathType = _pathType;
            m_FolderName = _foldername;

            // Handle URL type — assign directly, no file-system check
            if (m_PathType == PathType.URL)
            {
                if (_VideoPlayer == null)
                    _VideoPlayer = GetComponent<VideoPlayer>();
                _VideoPlayer.url = m_FileName;
                return true;
            }

            var filePath = string.Empty;


            if (m_PathType == PathType.StreamingAssets)
            {
                if (string.IsNullOrEmpty(m_FolderName))
                    filePath = "file://" + Path.Combine(Application.streamingAssetsPath, m_FileName);
                else
                    filePath = "file://" + Path.Combine(Application.streamingAssetsPath, m_FolderName, m_FileName);
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
            else if (m_PathType == PathType.ExternalResources)
            {
                var externalResourcesPath = ResourceManager.GetResourceFolderPath();

                filePath = Path.Combine(externalResourcesPath, m_FolderName, m_FileName);
            }

            var skipExistCheck = m_PathType == PathType.URL || m_PathType == PathType.StreamingAssets;
            if (string.IsNullOrEmpty(filePath) || (!skipExistCheck && !File.Exists(filePath)))
            {
                Debug.LogWarning($"[{name}] Video file not found: {filePath}");
                return false;
            }

            if (_VideoPlayer == null)
                _VideoPlayer = GetComponent<VideoPlayer>();

            _VideoPlayer.url = filePath;
            return true;
        }


        public void Init()
        {
            if (_VideoPlayer == null) _VideoPlayer = GetComponent<VideoPlayer>();

            _RectTransform = GetComponent<RectTransform>();
            _Transform = transform;

            if (m_OutputType == VideoOutputType.RawImage)
            {
                //remove unused component
                if (_MeshFilter != null) DestroyImmediate(_MeshFilter);
                if (_MeshRenderer != null) DestroyImmediate(_MeshRenderer);
                if (_Material != null) DestroyImmediate(_Material);

                if (_RawImage == null) _RawImage = GetComponent<RawImage>();
                if (_CanvasGroup == null) _CanvasGroup = GetComponent<CanvasGroup>();


                _VideoPlayer.isLooping = false;
                _VideoPlayer.playOnAwake = false;
                _VideoPlayer.renderMode = VideoRenderMode.APIOnly;
            }
            else if (m_OutputType == VideoOutputType.Renderer)
            {
                //remove unused component
                if (_RawImage != null) DestroyImmediate(_RawImage);
                if (_CanvasGroup != null) DestroyImmediate(_CanvasGroup);


                if (_Material == null)
                {
                    _Material = Shader.Find($"Shader Graphs/TransparentTextureWithColor") == null
                        ? new Material(Shader.Find("Unlit/TransparentTextureWithColor"))
                        : new Material(Shader.Find($"Shader Graphs/TransparentTextureWithColor"));
                }


                if (_MeshFilter == null) _MeshFilter = GetComponent<MeshFilter>();

                if (_MeshFilter != null)
                {
                    if (_MeshFilter.sharedMesh == null)

                        _MeshFilter.sharedMesh = new Mesh()
                        {
                            vertices = new Vector3[]
                            {
                                new Vector3(-0.5f, -0.5f, 0.0f),
                                new Vector3(0.5f, -0.5f, 0.0f),
                                new Vector3(0.5f, 0.5f, 0.0f),
                                new Vector3(-0.5f, 0.5f, 0.0f)
                            },
                            triangles = new int[] { 2, 1, 0, 3, 2, 0 },
                            uv = new Vector2[]
                            {
                                new Vector2(0, 0),
                                new Vector2(1, 0),
                                new Vector2(1, 1),
                                new Vector2(0, 1)
                            }
                        };
                }


                if (_MeshRenderer == null) _MeshRenderer = GetComponent<MeshRenderer>();

                if (_MeshRenderer != null)
                {
                    _MeshRenderer.material = _Material;
                }


                _VideoPlayer.isLooping = false;
                _VideoPlayer.playOnAwake = false;
                _VideoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            }

            if (Application.isPlaying) ApplyAlpha(0);
        }

        //------------------------------------ Public Method ----------------------------------

        public async UniTask PlayAsync(int _frame = 0, bool _ignoreFadeIn = false, bool _ignoreFadeOut = false,
            bool _forcePlay = false, bool _resume = false, CancellationToken _token = default)
        {
            if (_VideoPlayer == null)
                _VideoPlayer = GetComponent<VideoPlayer>();

            if ((_VideoPlayer.isPlaying || _IsPlayStarting) && !_forcePlay)
                return;

            _IsPlayStarting = true;

            if (!_resume)
            {
                _VideoPlayer.Stop();
                _Stopping = false;
                _FadeInProgress = 0f;
                _FadeOutProgress = 0f;
                _CancellationTokenSource?.Cancel();
                _CancellationTokenSource?.Dispose();
                _CancellationTokenSource = new CancellationTokenSource();

                //after cancel token be must wait for next frame
                await UniTask.NextFrame();

                if (_token != default)
                {
                    var linked = CancellationTokenSource.CreateLinkedTokenSource(_token, _CancellationTokenSource.Token);
                    _CancellationTokenSource.Dispose();
                    _CancellationTokenSource = linked;
                }
            }

            var token = _CancellationTokenSource.Token;

            _IgnoreFadeOut = _ignoreFadeOut;

            try
            {

                var isPrepared = await Prepare(token);
                if (!isPrepared)
                {
                    Debug.LogWarning($"[{name}] Video not prepared with invalid url: " + _VideoPlayer.url);
                    return;
                }

                Debug.Log($"[{name}] Play Video : " + _VideoPlayer.url);
                _VideoPlayer.frame = _frame;
                _VideoPlayer.Play();

                await UniTask.WaitUntil(() => _VideoPlayer.isPlaying, cancellationToken: token);
                m_IsPlaying = true;

                m_Progress = _frame / (float)_VideoPlayer.frameCount;

                m_FadeTime = Mathf.Clamp(m_FadeTime, 100, int.MaxValue);

                while (!token.IsCancellationRequested)
                {

                    if (_VideoPlayer == null)
                        break;

                    m_Progress = (float)_VideoPlayer.time / (float)_VideoPlayer.length;


                    if (_VideoPlayer.time <= (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) && !_Stopping)
                    {
                        _FadeInProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);

                        _FadeInProgress = Mathf.Clamp01(_FadeInProgress);
                        var valueProgress = EasingFormula.EasingFloat(Easing.Ease.EaseInQuad, 0f, 1f, _FadeInProgress);

                        //fade in
                        ApplyAlpha(m_FadeVideo && !_ignoreFadeIn ? valueProgress : 1);
                        _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? valueProgress : 1);
                    }
                    else if (!m_Loop && _VideoPlayer.time >=
                             _VideoPlayer.length - (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) || _Stopping)
                    {

                        _FadeOutProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        _FadeOutProgress = Mathf.Clamp01(_FadeOutProgress);
                        var valueProgress = EasingFormula.EasingFloat(Easing.Ease.EaseOutQuad, 1f, 0f,
                                _FadeOutProgress);

                        //fade out
                        if (m_KeepLastframe)
                        {
                            //keep last frame
                            if (_FadeOutProgress >= 1f)
                            {
                                Seek(_VideoPlayer.frameCount - 1);
                                Debug.Log($"[{name}] Video End :" + _VideoPlayer.url);

                                _OnEndEventHandler.Invoke();
                                break;
                            }

                        }
                        else
                        {
                            ApplyAlpha(m_FadeVideo && !_IgnoreFadeOut ? valueProgress : 0);
                            _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? valueProgress : 0);
                            if (_FadeOutProgress >= 1f)
                            {
                                Debug.Log($"[{name}] Video End :" + _VideoPlayer.url);
                                _OnEndEventHandler.Invoke();
                                break;
                            }
                        }


                    }
                    else
                    {
                        //playing
                        ApplyAlpha(1);
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

                    ApplyTexture(_VideoPlayer.texture);

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("Video Canceled.");

            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            finally
            {
                _Stopping = false;
                _IsPlayStarting = false;
                m_IsPlaying = false;
                m_IsPrepared = false;
                if (!m_KeepLastframe)
                {
                    if (_VideoPlayer != null) _VideoPlayer.Stop();
                    ApplyAlpha(0);
                }
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

            if (!_VideoPlayer || !_VideoPlayer.isPlaying || !m_IsPlaying)
            {
                ApplyAlpha(0);
                m_IsPlaying = false;
                m_IsPrepared = false;
                _IsPlayStarting = false;
                return;
            }
            if (_IsPlayStarting && !m_IsPlaying)
            {
                _CancellationTokenSource?.Cancel();
                return;
            }

            if(_Stopping)
                return;

            // Debug.Log($"Stop Video :{m_FileName} ");
            _IgnoreFadeOut = _ignoreFadeOut;
            _Stopping = true;
        }

        public void StopAndHide()
        {
            m_IsPlaying = false;
            m_IsPrepared = false;
            _IgnoreFadeOut = true;
            _Stopping = true;
            ApplyAlpha(0);
        }

        public void SetAlpha(float _alpha)
        {
            ApplyAlpha(_alpha);
        }

        private void ApplyTexture(Texture _texture)
        {
            if (_texture == null) return;
            if (_RectTransform == null)
            {
                _RectTransform = GetComponent<RectTransform>();
            }

            if (_Transform == null)
            {
                _Transform = transform;
            }
            var ratio = (float)_texture.width / (float)_texture.height;

            if (_AspectRatioFitter != null)
                switch (_ContentSizeMode)
                {

                    case ContentSizeMode.NativeSize:
                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;

                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _texture.width);
                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _texture.height);
                        break;
                    case ContentSizeMode.WidthControlHeight:
                        _AspectRatioFitter.aspectRatio = ratio;
                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                        break;
                    case ContentSizeMode.HeightControlWidth:
                        _AspectRatioFitter.aspectRatio = ratio;
                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                        break;
                    case ContentSizeMode.None:

                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                        break;

                }


            if (_RawImage != null)
            {
                _RawImage.texture = _texture;
            }

            if (_Material != null)
            {
                _Material.SetTexture(BaseMap, _texture);
            }
        }


        private void ApplyAlpha(float _alpha)
        {
            if (_Material != null)
            {
                _Material.SetFloat(Alpha, _alpha);
            }

            if (_CanvasGroup != null)
            {
                _CanvasGroup.SetAlpha(_alpha);
            }
        }

        public void Seek(int _frame)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log($"[{name}] Video Not Prepared.");
                return;
            }

            bool wasPaused = _VideoPlayer.isPaused;
            if (!_VideoPlayer.isPlaying)
                _VideoPlayer.Play();

            _VideoPlayer.frame = _frame;
            m_Progress = _frame / (float)_VideoPlayer.frameCount;

            if (wasPaused)
                _VideoPlayer.Pause();
        }

        public void Seek(float _progress)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log($"[{name}] Video Not Prepared.");
                return;
            }

            _progress = Mathf.Clamp(_progress, 0, 1);
            _VideoPlayer.time = _progress * _VideoPlayer.length;
            m_Progress = _progress;
        }

        public int GetCurrentFrame()
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.Log($"[{name}] Video Not Prepared.");
                return 0;
            }

            return (int)_VideoPlayer.frame;
        }




        public IUniTaskAsyncEnumerable<AsyncUnit> OnEndAsyncEnumerable(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnEndEventHandler, _token);
        }
    }

}


