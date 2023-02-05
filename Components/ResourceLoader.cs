using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Modules.Utilities
{
    public class ResourceLoader : MonoBehaviour
    {

        public string m_FileName;
        public ResourceManager.ResourceResponse.ResourceType m_Type;

        public UnityEventTexture2D m_OnLoadTextureCompleted;
        public UnityEventAudio m_OnLoadAudioCompleted;


        private void Awake()
        {

        }
        void Start()
        {
            ResourceManager.GetResource(m_FileName, m_Type).Subscribe(_ =>
            {

                if (_ != null)
                {
                    ApplyResource(_);
                }

            }).AddTo(this);
        }

        void ApplyResource(ResourceManager.ResourceResponse res)
        {
            if (res.m_ResourceType == ResourceManager.ResourceResponse.ResourceType.Texture)
            {
                m_OnLoadTextureCompleted?.Invoke(res.m_Texture);
            }
            else if (res.m_ResourceType == ResourceManager.ResourceResponse.ResourceType.AudioClip)
            {
                m_OnLoadAudioCompleted?.Invoke(res.m_AudioClip);
            }
        }


        [System.Serializable]
        public class UnityEventTexture2D : UnityEvent<Texture2D> { }
        [System.Serializable]
        public class UnityEventAudio : UnityEvent<AudioClip> { }
    }

}
