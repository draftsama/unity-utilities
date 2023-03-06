using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Modules.Utilities;
using UniRx;
using UnityEngine;

namespace Modules.Utilities
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _Instance;
        private static AudioSource _CurrentBGMAudio;

        [SerializeField] public List<string> m_ResourceNames = new List<string>();


        [SerializeField] public List<AudioClip> m_Audiolist = new List<AudioClip>();
        private List<AudioSource> m_AudioPlayerlist = new List<AudioSource>();

        private void Awake()
        {
            _Instance = this;
            DontDestroyOnLoad(this);

            if (m_ResourceNames.Count > 0)
                ResourceManager.GetResources(m_ResourceNames.ToArray()).Subscribe(_res =>
                {
                    if (_res != null && _res.Count > 0)
                    {
                        AddAudioList(_res.Select(x => x.m_AudioClip).ToList());
                    }

                }).AddTo(this);

            Observable.EveryUpdate().Subscribe(_ =>
            {
                foreach (var player in m_AudioPlayerlist)
                {
                    player.gameObject.SetActive(player.isPlaying);
                }
            }).AddTo(this);


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

        public static void PlayBGM(string _name, float _transitionTime = 0f, float _volume = 1f, bool _loop = true)
        {
            var instance = GetInstance();
            var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

            if (clip == null) return;

            var audioPlayer = instance.GetAudioSource();
            audioPlayer.loop = _loop;
            audioPlayer.volume = _CurrentBGMAudio == null ? 0f : _volume;

            audioPlayer.PlayOneShot(clip);


            _transitionTime = Mathf.Clamp(_transitionTime, 0, float.PositiveInfinity);
            LerpThread.FloatLerp(Mathf.RoundToInt(_transitionTime * GlobalConstant.SECONDS_TO_MILLISECONDS), 0, 1)
                .Subscribe(_value =>
                {
                    if (_CurrentBGMAudio != null) _CurrentBGMAudio.volume = 1 - _value;
                    audioPlayer.volume = _value;
                }, () =>
                {
                    if (_CurrentBGMAudio != null) _CurrentBGMAudio.Stop();
                    _CurrentBGMAudio = audioPlayer;

                    Debug.Log("PlayBGM :" + _CurrentBGMAudio);
                }).AddTo(instance);
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

        public static void PlayFX(string _name, float _volume = 1f)
        {
            var instance = GetInstance();
            var clip = instance.m_Audiolist.FirstOrDefault(_ => _.name == _name);

            if (clip == null) return;

            var audioPlayer = instance.GetAudioSource();
            audioPlayer.volume = _volume;
            audioPlayer.PlayOneShot(clip);
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
                    _CurrentBGMAudio?.Stop();
                    _CurrentBGMAudio = null;
                }).AddTo(instance);
        }


        private AudioSource GetAudioSource()
        {
            AudioSource player = m_AudioPlayerlist.FirstOrDefault(_ => !_.gameObject.activeSelf);
            if (player == null)
            {
                var go = new GameObject($"AudioPlayer_{m_AudioPlayerlist.Count}", typeof(AudioSource));
                go.transform.SetParent(GetInstance().transform);
                player = go.GetComponent<AudioSource>();
                player.playOnAwake = false;
                m_AudioPlayerlist.Add(player);
            }
            else
                player.gameObject.SetActive(true);

            player.volume = 0;
            player.loop = false;
            return player;
        }
    }
}