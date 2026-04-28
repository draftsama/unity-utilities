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
    public class VideoControllerV2 : MonoBehaviour
    {
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private const string ShaderGraphTransparent = "Shader Graphs/TransparentTextureWithColor";
        private const string UnlitTransparent = "Unlit/TransparentTextureWithColor";

        // ---- Serialized public ----
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

        // ---- Serialized hidden ----
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

        // ---- State machine ----
        private enum VideoState { Idle, Preparing, Playing, Paused, Finished }
        private VideoState _State = VideoState.Idle;
        private bool _StopRequested = false;   // true during manual-stop fade-out
        private bool _IgnoreFadeOut = false;
        private bool _FinishedNaturally = false;  // AutoPlay+ParentShow: prevent restart after natural end
        private bool _ManualStop = false;          // AutoPlay+ParentShow: prevent restart after Stop()
        private bool _BeCameResume = false;

        // ---- VideoPlayer ----
        private VideoPlayer _VideoPlayer;
        private bool _IsPrepared = false;
        private int _PlayGeneration = 0;  // prevent finally from clobbering a newer PlayAsync

        // ---- Cancellation ----
        private CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource _LoopCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource _LinkedCTS;

        // ---- Animation ----
        private float _FadeInProgress = 0f;
        private float _FadeOutProgress = 0f;

        // ---- Misc ----
        private RectTransform _RectTransform;
        private UnityEvent _OnEndEventHandler = new UnityEvent();

        // ---- Public properties ----
        public VideoPlayer m_VideoPlayer => _VideoPlayer;
        public bool m_IsPlaying => _State == VideoState.Playing;
        public bool m_IsPrepared => _IsPrepared;

        //------------------------------------------------------------
        // Unity Lifecycle
        //------------------------------------------------------------

        private void OnDisable()
        {
            if (_VideoPlayer != null)
            {
                _BeCameResume = m_IsPlaying || _State == VideoState.Preparing;
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

            _State = VideoState.Idle;
            _StopRequested = false;
        }

        private void OnEnable()
        {
            Init();
            switch (m_StartMode)
            {
                case VideoStartMode.None:
                    if (_BeCameResume) PlayAsync().Forget();
                    break;
                case VideoStartMode.Prepare:
                    Prepare().Forget();
                    break;
                case VideoStartMode.FirstFrameReady:
                    Prepare(firstFrame: true).Forget();
                    break;
                case VideoStartMode.AutoPlay:
                    if (!_PlayWithParentShow)
                    {
                        PlayAsync().Forget();
                    }
                    else if (_ParentCanvasGroup != null)
                    {
                        StartParentVisibilityMonitoring(_LoopCancellationTokenSource.Token);
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            _OnEndEventHandler.RemoveAllListeners();

            _CancellationTokenSource?.Cancel();
            _CancellationTokenSource?.Dispose();
            _CancellationTokenSource = null;

            _LoopCancellationTokenSource?.Cancel();
            _LoopCancellationTokenSource?.Dispose();
            _LoopCancellationTokenSource = null;

            _LinkedCTS?.Dispose();
            _LinkedCTS = null;

            if (_MeshFilter != null && _MeshFilter.sharedMesh != null)
                Destroy(_MeshFilter.sharedMesh);
        }

        //------------------------------------------------------------
        // Init
        //------------------------------------------------------------

        public void Init()
        {
            if (_VideoPlayer == null) _VideoPlayer = GetComponent<VideoPlayer>();
            _RectTransform = GetComponent<RectTransform>();

            if (m_OutputType == VideoOutputType.RawImage)
            {
                if (_MeshFilter != null) Destroy(_MeshFilter);
                if (_MeshRenderer != null) Destroy(_MeshRenderer);
                if (_Material != null) Destroy(_Material);

                if (_RawImage == null) _RawImage = GetComponent<RawImage>();
                if (_CanvasGroup == null) _CanvasGroup = GetComponent<CanvasGroup>();

                _VideoPlayer.isLooping = false;
                _VideoPlayer.playOnAwake = false;
                _VideoPlayer.renderMode = VideoRenderMode.APIOnly;
            }
            else if (m_OutputType == VideoOutputType.Renderer)
            {
                if (_RawImage != null) Destroy(_RawImage);
                if (_CanvasGroup != null) Destroy(_CanvasGroup);

                if (_Material == null)
                {
                    _Material = Shader.Find(ShaderGraphTransparent) != null
                        ? new Material(Shader.Find(ShaderGraphTransparent))
                        : new Material(Shader.Find(UnlitTransparent));
                }

                if (_MeshFilter == null) _MeshFilter = GetComponent<MeshFilter>();
                if (_MeshFilter != null && _MeshFilter.sharedMesh == null)
                {
                    _MeshFilter.sharedMesh = new Mesh
                    {
                        vertices = new[]
                        {
                            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                            new Vector3(0.5f,  0.5f, 0f),  new Vector3(-0.5f, 0.5f, 0f)
                        },
                        triangles = new[] { 2, 1, 0, 3, 2, 0 },
                        uv = new[]
                        {
                            new Vector2(0, 0), new Vector2(1, 0),
                            new Vector2(1, 1), new Vector2(0, 1)
                        }
                    };
                }

                if (_MeshRenderer == null) _MeshRenderer = GetComponent<MeshRenderer>();
                if (_MeshRenderer != null) _MeshRenderer.material = _Material;

                _VideoPlayer.isLooping = false;
                _VideoPlayer.playOnAwake = false;
                _VideoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            }

            if (Application.isPlaying) ApplyAlpha(0);
        }

        //------------------------------------------------------------
        // Public API
        //------------------------------------------------------------

        public async UniTask<bool> Prepare(bool firstFrame = false, CancellationToken token = default)
        {
            if (!_VideoPlayer.isPrepared || !_IsPrepared)
            {
                SetupURL(m_FileName, m_PathType, m_FolderName);
                if (string.IsNullOrEmpty(_VideoPlayer.url))
                {
                    Debug.LogWarning($"[{name}] Video URL is empty.");
                    return false;
                }

                _VideoPlayer.Prepare();
                await UniTask.WaitUntil(() => _VideoPlayer.isPrepared, cancellationToken: token);
                _IsPrepared = true;
            }

            if (firstFrame)
            {
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

            return true;
        }

        public async UniTask PrepareFirstFrame() => await Prepare(firstFrame: true);

        public async UniTask PlayAsync(int frame = 0, bool ignoreFadeIn = false, bool ignoreFadeOut = false,
            bool forcePlay = false, CancellationToken token = default)
        {
            if (_VideoPlayer == null) _VideoPlayer = GetComponent<VideoPlayer>();
            if ((m_IsPlaying || _State == VideoState.Preparing) && !forcePlay) return;

            // Resume instead of restart when paused (unless explicitly forced)
            if (_State == VideoState.Paused && !forcePlay)
            {
                UnPause();
                return;
            }

            _FinishedNaturally = false;
            _ManualStop = false;
            int myGeneration = ++_PlayGeneration;
            var debugName = name; // capture before any await — object may be destroyed by the time catch/finally runs
            _State = VideoState.Preparing;
            _StopRequested = false;
            _FadeInProgress = 0f;
            _FadeOutProgress = 0f;
            _IgnoreFadeOut = ignoreFadeOut;

            // Skip Stop() if already prepared to avoid re-prepare flicker (e.g. FirstFrameReady → Play)
            if (!_IsPrepared || !_VideoPlayer.isPrepared)
                _VideoPlayer.Stop();

            if (m_FadeVideo && !ignoreFadeIn) ApplyAlpha(0);

            _CancellationTokenSource.Cancel();
            _CancellationTokenSource.Dispose();
            _CancellationTokenSource = new CancellationTokenSource();

            _LinkedCTS?.Dispose();
            _LinkedCTS = CancellationTokenSource.CreateLinkedTokenSource(_CancellationTokenSource.Token, token);
            var linkedToken = _LinkedCTS.Token;

            try
            {
                if (!await Prepare(token: linkedToken))
                {
                    Debug.LogWarning($"[{debugName}] Video not prepared: {_VideoPlayer.url}");
                    _State = VideoState.Idle;
                    return;
                }

                Debug.Log($"[{debugName}] Play Video: {_VideoPlayer.url}");
                _VideoPlayer.frame = frame;
                _VideoPlayer.Play();

                await UniTask.WaitUntil(() => _VideoPlayer.isPlaying, cancellationToken: linkedToken);
                _State = VideoState.Playing;

                m_Progress = frame / (float)_VideoPlayer.frameCount;
                m_FadeTime = Mathf.Clamp(m_FadeTime, 100, int.MaxValue);

                await RunPlaybackLoop(ignoreFadeIn, debugName, linkedToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{debugName}] Video canceled.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                // Only clean up if we are still the active play session and object is alive
                if (_PlayGeneration == myGeneration && this != null)
                {
                    bool wasStop = _StopRequested;
                    _State = VideoState.Idle;
                    _StopRequested = false;
                    _IsPrepared = false;

                    _LinkedCTS?.Dispose();
                    _LinkedCTS = null;

                    if (!m_KeepLastframe || wasStop)
                    {
                        _VideoPlayer?.Stop();
                        ApplyAlpha(0);
                    }
                }
            }
        }

        public void Pause()
        {
            if (_VideoPlayer && !_VideoPlayer.isPaused)
            {
                _State = VideoState.Paused;
                _VideoPlayer.Pause();
            }
        }

        public void UnPause()
        {
            if (_VideoPlayer && _VideoPlayer.isPaused)
            {
                _State = VideoState.Playing;
                _VideoPlayer.Play();
            }
        }

        public void Stop(bool ignoreFadeOut = false)
        {
            _ManualStop = true;
            StopCore(ignoreFadeOut);
        }

        public void StopAndHide()
        {
            _CancellationTokenSource?.Cancel();
            _State = VideoState.Idle;
            _StopRequested = false;
            _IsPrepared = false;
            ApplyAlpha(0);
        }

        public void SetAlpha(float alpha) => ApplyAlpha(alpha);

        public void Seek(int seekFrame)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.LogWarning($"[{name}] Video not prepared.");
                return;
            }
            bool wasPaused = _VideoPlayer.isPaused;
            if (!_VideoPlayer.isPlaying) _VideoPlayer.Play();
            _VideoPlayer.frame = seekFrame;
            m_Progress = seekFrame / (float)_VideoPlayer.frameCount;
            if (wasPaused) _VideoPlayer.Pause();
        }

        public void Seek(float progress)
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.LogWarning($"[{name}] Video not prepared.");
                return;
            }
            progress = Mathf.Clamp01(progress);
            _VideoPlayer.time = progress * _VideoPlayer.length;
            m_Progress = progress;
        }

        public int GetCurrentFrame()
        {
            if (!_VideoPlayer.isPrepared)
            {
                Debug.LogWarning($"[{name}] Video not prepared.");
                return 0;
            }
            return (int)_VideoPlayer.frame;
        }

        public bool SetupURL(string filename, PathType pathType = PathType.Relative, string folderName = "Resources")
        {
            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogWarning($"[{name}] Video filename is empty.");
                return false;
            }

            m_FileName = filename;
            m_PathType = pathType;
            m_FolderName = folderName;

            if (_VideoPlayer == null) _VideoPlayer = GetComponent<VideoPlayer>();

            if (m_PathType == PathType.URL)
            {
                _VideoPlayer.url = m_FileName;
                return true;
            }

            var filePath = BuildFilePath(filename, pathType, folderName);
            bool skipExistCheck = pathType == PathType.StreamingAssets;

            if (string.IsNullOrEmpty(filePath) || (!skipExistCheck && !File.Exists(filePath)))
            {
                Debug.LogWarning($"[{name}] Video file not found: {filePath}");
                return false;
            }

            _VideoPlayer.url = filePath;
            return true;
        }

        public bool SetupFullURL(string filePath)
        {
            if (filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                m_PathType = PathType.URL;
                m_FolderName = string.Empty;
                return SetupURL(filePath, PathType.URL, string.Empty);
            }

            m_FileName = Path.GetFileName(filePath);
            m_PathType = PathType.Absolute;
            m_FolderName = Path.GetDirectoryName(filePath);
            return SetupURL(m_FileName, m_PathType, m_FolderName);
        }

        public IUniTaskAsyncEnumerable<AsyncUnit> OnEndAsyncEnumerable(CancellationToken token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnEndEventHandler, token);
        }

        //------------------------------------------------------------
        // Playback Loop
        //------------------------------------------------------------

        private async UniTask RunPlaybackLoop(bool ignoreFadeIn, string debugName, CancellationToken token)
        {
            float fadeSeconds = m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS;

            while (!token.IsCancellationRequested)
            {
                if (_VideoPlayer == null) break;

                m_Progress = (float)(_VideoPlayer.time / _VideoPlayer.length);

                bool inFadeIn = _VideoPlayer.time <= fadeSeconds && !_StopRequested;
                bool inFadeOut = (!m_Loop && _VideoPlayer.time >= _VideoPlayer.length - fadeSeconds) || _StopRequested;

                if (inFadeIn)
                {
                    TickFadeIn(ignoreFadeIn, fadeSeconds);
                }
                else if (inFadeOut)
                {
                    // If Stop() called while still fading in, continue fade-out from current alpha level
                    if (_FadeOutProgress == 0f && _FadeInProgress < 1f && _StopRequested)
                        _FadeOutProgress = 1f - _FadeInProgress;

                    // Nothing to animate — skip fade duration and end immediately
                    if (!m_FadeVideo && !m_FadeAudio)
                        _FadeOutProgress = 1f;

                    if (TickFadeOut(fadeSeconds, debugName))
                        break;
                }
                else
                {
                    ApplyAlpha(1f);
                    _VideoPlayer.SetDirectAudioVolume(0, 1f);
                }

                if (m_Loop && _VideoPlayer.isPlaying && !_VideoPlayer.isPaused &&
                    !_StopRequested && _VideoPlayer.frame >= _VideoPlayer.frameCount - 1f)
                {
                    _FadeInProgress = 0f;
                    _VideoPlayer.Pause();
                    await UniTask.NextFrame(token);
                    _VideoPlayer.frame = 0;
                    await UniTask.NextFrame(token);
                    _VideoPlayer.Play();
                }

                ApplyTexture(_VideoPlayer.texture);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private void TickFadeIn(bool ignoreFadeIn, float fadeSeconds)
        {
            _FadeInProgress += Time.deltaTime / fadeSeconds;
            _FadeInProgress = Mathf.Clamp01(_FadeInProgress);
            float v = EasingFormula.EasingFloat(Easing.Ease.EaseInQuad, 0f, 1f, _FadeInProgress);
            ApplyAlpha(m_FadeVideo && !ignoreFadeIn ? v : 1f);
            _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? v : 1f);
        }

        // Returns true when fade-out is complete and playback should end.
        private bool TickFadeOut(float fadeSeconds, string debugName)
        {
            _FadeOutProgress += Time.deltaTime / fadeSeconds;
            _FadeOutProgress = Mathf.Clamp01(_FadeOutProgress);
            float v = EasingFormula.EasingFloat(Easing.Ease.EaseOutQuad, 1f, 0f, _FadeOutProgress);

            if (m_KeepLastframe && !_StopRequested)
            {
                _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? v : 0f);
                if (_FadeOutProgress >= 1f)
                {
                    Seek((int)(_VideoPlayer.frameCount - 1));
                    Debug.Log($"[{debugName}] Video End: {_VideoPlayer.url}");
                    _FinishedNaturally = !m_Loop;
                    _OnEndEventHandler.Invoke();
                    return true;
                }
            }
            else
            {
                ApplyAlpha(m_FadeVideo && !_IgnoreFadeOut ? v : 1f);
                _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? v : 0f);
                if (_FadeOutProgress >= 1f)
                {
                    Debug.Log($"[{debugName}] Video End: {_VideoPlayer.url}");
                    _FinishedNaturally = !m_Loop;
                    _OnEndEventHandler.Invoke();
                    return true;
                }
            }

            return false;
        }

        //------------------------------------------------------------
        // Parent Visibility Monitoring
        //------------------------------------------------------------

        private void StartParentVisibilityMonitoring(CancellationToken token)
        {
            try
            {
                UniTaskAsyncEnumerable.EveryUpdate()
                    .ForEachAsync(_ => TickParentVisibility(), token)
                    .Forget();
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{name}] Parent monitoring canceled.");
            }
            catch (Exception)
            {
                Debug.Log($"[{name}] Parent monitoring error — likely missing reference.");
            }
        }

        private void TickParentVisibility()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_ParentCanvasGroup.alpha >= _CanvasGroupThreshold)
                TryAutoPlay();
            else
                TryAutoStop();
        }

        private void TryAutoPlay()
        {
            if (!m_IsPlaying && _State != VideoState.Preparing && !_ManualStop && !_FinishedNaturally)
                PlayAsync().Forget();
        }

        private void TryAutoStop()
        {
            _FinishedNaturally = false;
            _ManualStop = false;
            if (_State == VideoState.Paused) _State = VideoState.Idle;
            if (m_IsPlaying && !_StopRequested) StopCore();
        }

        //------------------------------------------------------------
        // Stop Core
        //------------------------------------------------------------

        private void StopCore(bool ignoreFadeOut = false)
        {
            if (!_VideoPlayer || (!m_IsPlaying && _State != VideoState.Preparing))
            {
                _CancellationTokenSource?.Cancel();  // kill any suspended loop (e.g. paused state)
                ApplyAlpha(0);
                _IsPrepared = false;
                _State = VideoState.Idle;
                return;
            }

            if (_State == VideoState.Preparing)
            {
                _CancellationTokenSource?.Cancel();
                return;
            }

            if (_StopRequested) return;

            _IgnoreFadeOut = ignoreFadeOut;
            _StopRequested = true;
        }

        //------------------------------------------------------------
        // Render
        //------------------------------------------------------------

        private void ApplyAlpha(float alpha)
        {
            if (_Material != null) _Material.SetFloat(Alpha, alpha);
            if (_CanvasGroup != null) _CanvasGroup.SetAlpha(alpha);
        }

        private void ApplyTexture(Texture texture)
        {
            if (texture == null) return;

            if (_RectTransform == null)
                _RectTransform = GetComponent<RectTransform>();

            float ratio = (float)texture.width / texture.height;

            if (_AspectRatioFitter != null)
            {
                switch (_ContentSizeMode)
                {
                    case ContentSizeMode.NativeSize:
                        _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, texture.width);
                        _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, texture.height);
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
            }

            if (_RawImage != null) _RawImage.texture = texture;
            if (_Material != null) _Material.SetTexture(BaseMap, texture);
        }

        private static string BuildFilePath(string filename, PathType pathType, string folderName)
        {
            return pathType switch
            {
                PathType.StreamingAssets => string.IsNullOrEmpty(folderName)
                    ? "file://" + Path.Combine(Application.streamingAssetsPath, filename)
                    : "file://" + Path.Combine(Application.streamingAssetsPath, folderName, filename),
                PathType.Relative => string.IsNullOrEmpty(folderName)
                    ? Path.Combine(Environment.CurrentDirectory, filename)
                    : Path.Combine(Environment.CurrentDirectory, folderName, filename),
                PathType.Absolute => Path.Combine(folderName, filename),
                PathType.ExternalResources => Path.Combine(ResourceManager.GetResourceFolderPath(), folderName, filename),
                _ => string.Empty
            };
        }
    }
}
