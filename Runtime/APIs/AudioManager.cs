using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine.Events;
using System.IO;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _Instance;

        private static AudioSource _CurrentBGMAudio;
        public static bool ResourcesIsLoaded { get; private set; }

        [SerializeField] public List<string> m_RequireAudios = new List<string>();


        [SerializeField] public List<AudioClip> m_Audiolist = new List<AudioClip>();
        private List<AudioSource> m_AudioPlayerList = new List<AudioSource>();

        [SerializeField] private UnityEvent m_OnLoadRequireCompleted;

        private bool _IsPause = false;

        private void OnApplicationPause(bool pauseStatus)
        {
            _IsPause = pauseStatus;


        }

        private async void Awake()
        {

            if (_Instance != null && _Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _Instance = this;



            DontDestroyOnLoad(this);

            await UniTask.Yield();
            if (m_RequireAudios.Count > 0)
            {
                ResourcesIsLoaded = false;
                var audioClips = await ResourceManager.GetAudioClipsAsync(m_RequireAudios.ToArray());

                AddAudioList(audioClips.ToList());
                Debug.Log("Load Audio Completed");
                m_OnLoadRequireCompleted?.Invoke();

            }

            ResourcesIsLoaded = true;
        }


        public IUniTaskAsyncEnumerable<AsyncUnit> OnLoadRequireCompleted(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(m_OnLoadRequireCompleted, _token);
        }

        private static AudioManager GetInstance()
        {
            if (_Instance == null)
            {
                GameObject go = new GameObject("AudioManager", typeof(AudioManager));
                _Instance = go.GetComponent<AudioManager>();
            }

            return _Instance;
        }

        public static void AddAudioList(List<AudioClip> _audioClips)
        {
            GetInstance().m_Audiolist = _audioClips;
        }

        public static void AddAudio(AudioClip _audioClip)
        {
            var instance = GetInstance();

            if (!instance.m_Audiolist.Contains(_audioClip))
                instance.m_Audiolist.Add(_audioClip);
        }


        public static async UniTask PlayBGM(string _name, float _transitionTime = 0f, float _volume = 1f, bool _loop = true, CancellationToken _token = default)
        {
            var instance = GetInstance();
            if (_token == default) _token = instance.GetCancellationTokenOnDestroy();
            AudioSource audioPlayer = null;

            try
            {

                await UniTask.WaitUntil(() => ResourcesIsLoaded, cancellationToken: _token);

                // Debug.Log($"PlayBGM: {_name}");


                var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

                if (clip == null)
                {
                    var resAudio = await ResourceManager.GetAudioClipAsync(_name);

                    if (resAudio == null)
                    {
                        Debug.LogError($"AudioManager: Can't find audio name {_name}");
                        return;
                    }
                    else
                    {
                        clip = resAudio;
                        AddAudio(clip);
                    }

                }


                audioPlayer = instance.GetAudioSource();

                audioPlayer.gameObject.SetActive(true);
                audioPlayer.loop = _loop;
                audioPlayer.volume = _CurrentBGMAudio == null ? 0f : _volume;
                audioPlayer.clip = clip;
                audioPlayer.Play();

                _transitionTime = Mathf.Clamp(_transitionTime, 0, float.PositiveInfinity);
                var miliseconds = Mathf.RoundToInt(_transitionTime * GlobalConstant.SECONDS_TO_MILLISECONDS);

                await LerpThread.FloatLerpAsyncEnumerable(miliseconds, 0, 1).ForEachAsync(_value =>
                    {
                        if (_CurrentBGMAudio != null) _CurrentBGMAudio.volume = 1 - _value;
                        audioPlayer.volume = _value;
                    }, _token);

                if (_CurrentBGMAudio != null) _CurrentBGMAudio.Stop();
                _CurrentBGMAudio = audioPlayer;

                await UniTask.WaitUntil(() => audioPlayer.isPlaying == false, cancellationToken: _token);

            }
            catch (OperationCanceledException)
            {
            }

            if (audioPlayer != null)
                audioPlayer.gameObject.SetActive(false);


        }






        public static void SetBGMVolume(float _volumeTarget, float _transitionTime = 0f)
        {
            if (_CurrentBGMAudio == null) return;

            var start = _CurrentBGMAudio.volume;

            _transitionTime = Mathf.Clamp(_transitionTime, 0, float.PositiveInfinity);

            var miliseconds = Mathf.RoundToInt(_transitionTime * GlobalConstant.SECONDS_TO_MILLISECONDS);
            LerpThread.FloatLerpAsyncEnumerable(miliseconds, start, _volumeTarget).ForEachAsync(_value =>
            {
                _CurrentBGMAudio.volume = _value;
            }).Forget();


            //  .Subscribe(_value =>
            //  {
            //      _CurrentBGMAudio.volume = _value;
            //  }, () =>
            //  {
            //      _CurrentBGMAudio.volume = _volumeTarget;
            //  }).AddTo(GetInstance());
        }

        public static async UniTask PlayFX(string _name, float _volume = 1f, bool _loop = false,CancellationToken _token = default)
        {
            var instance = GetInstance();
            if (_token == default) _token = instance.GetCancellationTokenOnDestroy();
            AudioSource audioPlayer = null;

            try
            {

                await UniTask.WaitUntil(() => ResourcesIsLoaded, cancellationToken: _token);

                var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

                if (clip == null)
                {
                    var resAudio = await ResourceManager.GetAudioClipAsync(_name);

                    if (resAudio == null)
                    {
                        Debug.LogError($"AudioManager: Can't find audio name {_name}");
                        return;
                    }
                    else
                    {
                        clip = resAudio;
                        AddAudio(clip);
                    }

                }

                // Debug.Log($"PlayFX: {_name}");
                audioPlayer = instance.GetAudioSource();
                audioPlayer.volume = _volume;
                audioPlayer.clip = clip;
                audioPlayer.loop = _loop;
                audioPlayer.Play();

                await UniTask.WaitUntil(() => audioPlayer.isPlaying == false, cancellationToken: _token);


            }
            catch (OperationCanceledException)
            {

            }

            if (audioPlayer != null)
                audioPlayer.gameObject.SetActive(false);


        }



        public static bool HasAudio(string _name)
        {
            var instance = GetInstance();
            return instance.m_Audiolist.FirstOrDefault(_ => _.name == _name) != null;

        }
        public static void StopBGM(float _fade = 0)
        {
            if (_CurrentBGMAudio == null) return;
            var instance = GetInstance();

            _fade = Mathf.Clamp(_fade, 0, float.PositiveInfinity);
            var miliseconds = Mathf.RoundToInt(_fade * GlobalConstant.SECONDS_TO_MILLISECONDS);
            var start = _CurrentBGMAudio.volume;
            LerpThread.FloatLerpAsyncEnumerable(miliseconds, start, 0)
                .ForEachAsync(_value =>
                {
                    _CurrentBGMAudio.volume = _value;
                }).ContinueWith(() =>
                {
                    _CurrentBGMAudio.clip = null;
                    _CurrentBGMAudio?.Stop();
                    _CurrentBGMAudio = null;
                }).Forget();
        }


        private AudioSource GetAudioSource()
        {
            AudioSource player = m_AudioPlayerList.FirstOrDefault(_ => !_.gameObject.activeSelf);
            if (player == null)
            {
                var go = new GameObject($"AudioPlayer_{m_AudioPlayerList.Count}", typeof(AudioSource));
                go.transform.SetParent(GetInstance().transform);
                player = go.GetComponent<AudioSource>();
                player.playOnAwake = false;
                m_AudioPlayerList.Add(player);
            }
            else
                player.gameObject.SetActive(true);

            player.volume = 0;
            player.loop = false;
            return player;
        }
    }

}

#if UNITY_EDITOR

namespace Modules.Utilities.Editor
{

    [CustomEditor(typeof(AudioManager))]
    public class AudioManagerEditor : UnityEditor.Editor
    {

        AudioManagerEditor _Instance;

        SerializedProperty m_RequireAudios;
        SerializedProperty m_Audiolist;
        SerializedProperty m_OnLoadRequireCompleted;


        private void OnEnable()
        {


            _Instance = target as AudioManagerEditor;


            m_RequireAudios = serializedObject.FindProperty("m_RequireAudios");
            m_Audiolist = serializedObject.FindProperty("m_Audiolist");
            m_OnLoadRequireCompleted = serializedObject.FindProperty("m_OnLoadRequireCompleted");
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();



            ResourceSettingAssets[] resourceSettingAssets = Resources.LoadAll<ResourceSettingAssets>("");
            if (resourceSettingAssets.Length == 0)
            {
                EditorGUILayout.HelpBox("ResourceSettingAssets is null", MessageType.Error);

                if (GUILayout.Button("Create ResourceSettingAssets"))
                {
                    // Ensure the "Resources" folder exists
                    string resourcesFolderPath = "Assets/Resources";
                    if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
                    {
                        AssetDatabase.CreateFolder("Assets", "Resources");
                    }



                    // Create the asset in the "Resources" folder
                    ResourceSettingAssets asset = ScriptableObject.CreateInstance<ResourceSettingAssets>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(resourcesFolderPath + "/NewResourceSetting.asset");

                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.SaveAssets();




                }

            }
            else
            {


                EditorGUILayout.PropertyField(m_RequireAudios, true);

                if (GUILayout.Button("Get All Audio Name From Resource"))
                {
                    var dir = new DirectoryInfo(ResourceManager.GetFolderResourcePath());

                    string[] extensions = ResourceManager.GetSearchPattern(ResourceManager.ResourceResponse.ResourceType.AudioClip);

                    var files = dir.GetFiles("*.*", SearchOption.AllDirectories).Where(file => extensions.Contains(file.Extension)).ToArray();
                    var audioNames = files.Select(_ => _.Name).ToList();

                    m_RequireAudios.ClearArray();
                    m_RequireAudios.arraySize = audioNames.Count;

                    for (int i = 0; i < audioNames.Count; i++)
                    {
                        m_RequireAudios.GetArrayElementAtIndex(i).stringValue = audioNames[i];
                    }

                }





            }


            EditorGUILayout.PropertyField(m_Audiolist, true);
            EditorGUILayout.PropertyField(m_OnLoadRequireCompleted, true);


            if (GUI.changed)
                EditorUtility.SetDirty(target);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
