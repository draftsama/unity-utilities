using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [SerializeField][ReadOnlyField] private bool m_IsPrepared = false;
        [SerializeField][ReadOnlyField] public float m_Progress = 0f;

        [SerializeField] public VideoOutputType m_OutputType = VideoOutputType.RawImage;


        [SerializeField][HideInInspector] private RawImage _RawImage;
        [SerializeField][HideInInspector] private CanvasGroup _CanvasGroup;
        [SerializeField][HideInInspector] private bool _PlayWithParentShow = false;
        [SerializeField][HideInInspector] private CanvasGroup _ParentCanvasGroup;
        [SerializeField][HideInInspector] private float _CanvasGroupThreshold = 0.1f;


        [SerializeField][HideInInspector] private MeshFilter _MeshFilter;
        [SerializeField][HideInInspector] private MeshRenderer _MeshRenderer;
        [SerializeField][HideInInspector] private Material _Material;

        private VideoPlayer _VideoPlayer;
        private UnityEvent _OnEndEventHandler = new UnityEvent();

        private bool _Stoping = false;
        private bool _IgnoreFadeOut = false;


        public VideoPlayer m_VideoPlayer => _VideoPlayer;
        public bool m_IsPlaying => _VideoPlayer != null && _VideoPlayer.isPlaying;

        CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();

        private void OnDisable()
        {
            if (_VideoPlayer != null)
            {
                _VideoPlayer.Stop();
                _VideoPlayer.targetTexture?.Release();
                m_IsPrepared = false;
                _VideoPlayer.targetTexture = null;
            }

            _CancellationTokenSource.Cancel();
            _CancellationTokenSource = new CancellationTokenSource();

        }



        private async void Awake()
        {
            Init();

            if (m_StartMode != VideoStartMode.None)
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
                        cancellationToken: this.GetCancellationTokenOnDestroy());

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



            if (_PlayWithParentShow && _ParentCanvasGroup != null)
            {
                bool isPlaying = false;
                UniTaskAsyncEnumerable.EveryUpdate().ForEachAsync(_ =>
                {

                    if (_ParentCanvasGroup.alpha >= _CanvasGroupThreshold)
                    {
                        if (!isPlaying)
                        {
                            PlayAsync().Forget();
                            isPlaying = true;
                        }
                    }
                    else
                    {
                        if (isPlaying)
                        {
                            isPlaying = false;
                            Stop();
                        }
                    }
                }).Forget();
            }
        }


        void Start()
        {
        }




        public async UniTask PrepareFirstFrame()
        {
            if (!SetupURL(m_FileName, m_PathType, m_FolderName))
                return;


            _VideoPlayer.Stop();
            await UniTask.NextFrame();
            _VideoPlayer.SetDirectAudioVolume(0, 0);
            _VideoPlayer.Play();

            await UniTask.WaitUntil(() => _VideoPlayer.isPlaying,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            _VideoPlayer.frame = 1;
            ApplyTexture(_VideoPlayer.texture);
            ApplyAlpha(1);
            _VideoPlayer.Pause();
        }

        public bool SetupFullURL(string _filePath)
        {
            m_FileName = Path.GetFileName(_filePath);
            m_PathType = PathType.Absolute;
            m_FolderName = Path.GetDirectoryName(_filePath);


            return SetupURL(m_FileName, m_PathType, m_FolderName);
        }

        public bool SetupURL(string _filename, PathType _pathType = PathType.Relative, string _foldername = "Resources")
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

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.Log($"[{name}] Video file not found: {filePath}");
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

            ApplyAlpha(0);
        }

        //------------------------------------ Public Method ----------------------------------

        public async UniTask PlayAsync(int _frame = 0, bool _ignoreFadeIn = false, bool _ignoreFadeOut = false,
            bool _forcePlay = false, CancellationToken _token = default)
        {
            if (_VideoPlayer.isPlaying && !_forcePlay)
                return;


            _VideoPlayer.Stop();

            //after cancel token be must wait for next frame

            _CancellationTokenSource?.Cancel();
            _CancellationTokenSource = new CancellationTokenSource();
            await UniTask.NextFrame();

            if (_token != default)
            {
                _CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_token);
            }
            var token = _CancellationTokenSource.Token;

            _Stoping = false;


            m_Progress = 0f;
            _IgnoreFadeOut = _ignoreFadeOut;
            float fadeInProgress = 0f;
            float fadeOutProgress = 0f;
            try
            {



                if (!_VideoPlayer.isPrepared)
                {
                    SetupURL(m_FileName, m_PathType, m_FolderName);
                    if (string.IsNullOrEmpty(_VideoPlayer.url))
                    {
                        //throw exception if url is empty
                        throw new Exception($"[{gameObject.name}]  Video URL is empty.");
                    }
                    _VideoPlayer.Prepare();
                    await UniTask.WaitUntil(() => _VideoPlayer.isPrepared, cancellationToken: token);
                    m_IsPrepared = true;
                }

                Debug.Log("Play Video : " + _VideoPlayer.url);
                _VideoPlayer.frame = _frame;
                _VideoPlayer.Play();

                await UniTask.WaitUntil(() => _VideoPlayer.isPlaying, cancellationToken: token);


                ApplyTexture(_VideoPlayer.texture);


                while (!token.IsCancellationRequested)
                {
                    if (_VideoPlayer == null)
                        break;

                    m_Progress = (float)_VideoPlayer.time / (float)_VideoPlayer.length;


                    if (_VideoPlayer.time <= (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) && !_Stoping)
                    {
                        fadeInProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        var valueProgress =
                            Mathf.Clamp01(EasingFormula.EasingFloat(Easing.Ease.EaseInOutQuad, 0f, 1f, fadeInProgress));

                        //fade in
                        ApplyAlpha(m_FadeVideo && !_ignoreFadeIn ? valueProgress : 1);
                        _VideoPlayer.SetDirectAudioVolume(0, m_FadeAudio ? valueProgress : 1);
                    }
                    else if (!m_Loop && _VideoPlayer.time >=
                             _VideoPlayer.length - (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS) || _Stoping)
                    {
                        fadeOutProgress += Time.deltaTime / (m_FadeTime * GlobalConstant.MILLISECONDS_TO_SECONDS);
                        var valueProgress =
                            Mathf.Clamp01(EasingFormula.EasingFloat(Easing.Ease.EaseInOutQuad, 1f, 0f,
                                fadeOutProgress));
                        //fade out
                        ApplyAlpha(m_FadeVideo && !_IgnoreFadeOut ? valueProgress : 0);
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
                _Stoping = false;
                if (_VideoPlayer != null) _VideoPlayer.Stop();
                ApplyAlpha(0);
            }


            // _Stoping = false;
            // if(_VideoPlayer != null)_VideoPlayer.Stop();
            // if(_CanvasGroup != null)_CanvasGroup.SetAlpha(0);
            // // Debug.Log("Video End.");

            // _OnEndEventHandler.Invoke();
            // _OnEnd?.Invoke(Unit.Default);
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

        private void ApplyTexture(Texture _texture)
        {
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

#if UNITY_EDITOR

namespace Modules.Utilities
{

    [CustomEditor(typeof(VideoController))]
    public class VideoControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _RawImage;
        private SerializedProperty _CanvasGroup;
        private SerializedProperty _PlayWithParentShow;
        private SerializedProperty _ParentCanvasGroup;
        private SerializedProperty _CanvasGroupThreshold;

        private SerializedProperty _MeshFilter;
        private SerializedProperty _MeshRenderer;
        private SerializedProperty _Material;

        private VideoController instance;

        public void OnEnable()
        {
            instance = (VideoController)target;
            if (!Application.isPlaying) instance.Init();
            serializedObject.Update();
            _RawImage = serializedObject.FindProperty("_RawImage");
            _CanvasGroup = serializedObject.FindProperty("_CanvasGroup");
            _PlayWithParentShow = serializedObject.FindProperty("_PlayWithParentShow");
            _ParentCanvasGroup = serializedObject.FindProperty("_ParentCanvasGroup");
            _CanvasGroupThreshold = serializedObject.FindProperty("_CanvasGroupThreshold");

            _MeshFilter = serializedObject.FindProperty("_MeshFilter");
            _MeshRenderer = serializedObject.FindProperty("_MeshRenderer");
            _Material = serializedObject.FindProperty("_Material");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void DrawObjectProperty(SerializedProperty _property, Type _componentType)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_property);
            GUI.enabled = true;
            GUI.color = Color.green;
            if (_property.objectReferenceValue == null && GUILayout.Button("Add"))
            {
                var componentObject = instance.gameObject.AddComponent(_componentType);
                _property.objectReferenceValue = componentObject;
            }

            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            base.OnInspectorGUI();


            var outputType = serializedObject.FindProperty("m_OutputType");
            var startMode = serializedObject.FindProperty("m_StartMode");

            if (outputType.enumValueIndex == (int)VideoOutputType.Renderer)
            {
                DrawObjectProperty(_MeshFilter, typeof(MeshFilter));
                DrawObjectProperty(_MeshRenderer, typeof(MeshRenderer));
                _PlayWithParentShow.boolValue = false;
                EditorGUILayout.PropertyField(_Material);
            }
            else if (outputType.enumValueIndex == (int)VideoOutputType.RawImage)
            {
                DrawObjectProperty(_RawImage, typeof(RawImage));
                DrawObjectProperty(_CanvasGroup, typeof(CanvasGroup));

                if (startMode.enumValueIndex == (int)VideoStartMode.AutoPlay)
                {

                    EditorGUILayout.PropertyField(_PlayWithParentShow);
                    if (_PlayWithParentShow.boolValue)
                    {
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                            _ParentCanvasGroup.objectReferenceValue = instance.transform.parent.GetComponent<CanvasGroup>();

                        EditorGUILayout.PropertyField(_ParentCanvasGroup);
                        if (_ParentCanvasGroup.objectReferenceValue == null)
                        {
                            //helpbox
                            EditorGUILayout.HelpBox("Parent CanvasGroup is null, please assign it.", MessageType.Error);
                        }

                        EditorGUILayout.Slider(_CanvasGroupThreshold, 0.1f, 1f);
                    }
                }
            }

            if (GUI.changed)
            {
                instance.Init();
                EditorUtility.SetDirty(instance);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
