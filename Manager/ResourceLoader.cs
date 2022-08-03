using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.Events;

namespace Modules.Utilities
{
    public class ResourceLoader : MonoBehaviour
    {

        public string m_FileName;
        public ResourceManager.ResourceResponse.ResourceType m_Type;
        public UnityEventTexture2D m_OnLoadTextureCompleted;
        public UnityEventAudio m_OnLoadAudioCompleted;
        void Start()
        {
            ResourceManager.GetResource(m_FileName, m_Type).Subscribe(_ =>
            {
                
                if (_ != null)
                {

                    if (_.m_ResourceType == ResourceManager.ResourceResponse.ResourceType.Texture)
                    {
                        m_OnLoadTextureCompleted?.Invoke(_.m_Texture);
                    }
                    else if (_.m_ResourceType == ResourceManager.ResourceResponse.ResourceType.AudioClip)
                    {
                        m_OnLoadAudioCompleted?.Invoke(_.m_AudioClip);
                    }
                }
            }).AddTo(this);
        }


        [System.Serializable]
        public class UnityEventTexture2D : UnityEvent<Texture2D> { }
        [System.Serializable]
        public class UnityEventAudio : UnityEvent<AudioClip> { }
    }

}
