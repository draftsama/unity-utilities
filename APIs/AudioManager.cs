using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Modules.Utilities;
using UniRx;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine.Events;


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

        [SerializeField] private UnityEvent _OnLoadRequireCompleted;

        private bool _IsPause = false;

        private void OnApplicationPause(bool pauseStatus)
        {
            _IsPause = pauseStatus;


        }

        private async void Awake()
        {
            _Instance = this;
            DontDestroyOnLoad(this);
            await UniTask.Yield();
            if (m_RequireAudios.Count > 0)
            {
                var audioClips = await ResourceManager.GetAudioClipsAsync(m_RequireAudios.ToArray());

                AddAudioList(audioClips.ToList());
                Debug.Log("Load Audio Completed");
                _OnLoadRequireCompleted?.Invoke();

            }

            ResourcesIsLoaded = true;
        }


        public IUniTaskAsyncEnumerable<AsyncUnit> OnLoadRequireCompleted(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnLoadRequireCompleted, _token);
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


        public static void PlayBGM(string _name, float _transitionTime = 0f, float _volume = 1f, bool _loop = true, CancellationToken _token = default)
        {
            var instance = GetInstance();
            if (_token == default) _token = instance.GetCancellationTokenOnDestroy();

            UniTask.Create(async () =>
            {
                await UniTask.WaitUntil(() => ResourcesIsLoaded, cancellationToken: _token);
                Debug.Log($"PlayBGM: {_name}");


                var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

                if (clip == null)
                {
                    Debug.LogWarning($"AudioManager: {_name}: clip is null");
                    return;
                }


                var audioPlayer = instance.GetAudioSource();
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

            })
            .AttachExternalCancellation(_token).Forget();

        }




        static IDisposable SetBGMVolumeDisposable;
        public static void SetBGMVolume(float _volumeTarget, float _transitionTime = 0f)
        {
            if (_CurrentBGMAudio == null) return;
            SetBGMVolumeDisposable?.Dispose();
            var start = _CurrentBGMAudio.volume;

            _transitionTime = Mathf.Clamp(_transitionTime, 0, float.PositiveInfinity);
            SetBGMVolumeDisposable = LerpThread.FloatLerp(Mathf.RoundToInt(_transitionTime * GlobalConstant.SECONDS_TO_MILLISECONDS), start, _volumeTarget)
                 .Subscribe(_value =>
                 {
                     _CurrentBGMAudio.volume = _value;
                 }, () =>
                 {
                     _CurrentBGMAudio.volume = _volumeTarget;
                 }).AddTo(GetInstance());
        }

        public static AudioSource PlayFX(string _name, float _volume = 1f)
        {
            var instance = GetInstance();
            var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

            if (clip == null) return null;

            var audioPlayer = instance.GetAudioSource();
            audioPlayer.volume = _volume;
            audioPlayer.clip = clip;
            audioPlayer.Play();
            return audioPlayer;
        }



        public static bool HasAudio(string _name)
        {
            var instance = GetInstance();
            return instance.m_Audiolist.FirstOrDefault(_ => _.name == _name) != null;

        }
        public static void StopBGM(float _fade = 0)
        {
            var instance = GetInstance();

            _fade = Mathf.Clamp(_fade, 0, float.PositiveInfinity);
            LerpThread.FloatLerp(Mathf.RoundToInt(_fade * GlobalConstant.SECONDS_TO_MILLISECONDS), 0, 1)
                .Subscribe(_value =>
                {
                    if (_CurrentBGMAudio != null) _CurrentBGMAudio.volume = 1 - _value;
                }, () =>
                {
                    _CurrentBGMAudio.clip = null;
                    _CurrentBGMAudio?.Stop();
                    _CurrentBGMAudio = null;
                }).AddTo(instance);
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