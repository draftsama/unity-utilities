using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine.Networking;
#if PACKAGE_ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Modules.Utilities
{
    public class ResourceManager : MonoBehaviour
    {
        [SerializeField] public ResourceStoreType m_ResourceStoreType;
        [SerializeField] private bool m_DontDestroyOnLoad = true;
        private static ResourceManager _Instance;


        [SerializeField] private List<ResourceResponse> m_ResourceResponseList;

        private void Awake()
        {
            if (_Instance != null)
            {
                Destroy(gameObject);
                return;
            }


            m_ResourceResponseList?.Clear();
            m_ResourceResponseList = new List<ResourceResponse>();

            //if mobile platform, use Addressable
            if (Application.platform == RuntimePlatform.Android ||
               Application.platform == RuntimePlatform.IPhonePlayer)
            {
                m_ResourceStoreType = ResourceStoreType.Addressable;
            }


            _Instance = this;
            if (m_DontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }



        private void OnDestroy()
        {
            _Instance = null;
        }


        //-------- Public Methods --------//
        public static async UniTask<List<ResourceResponse>> LoadAllResource()
        {


            await UniTask.Yield();


            if (!Directory.Exists(GetFolderResourcePath()))
                throw new Exception($"Not found Resources Folder :{GetFolderResourcePath()}");

            List<ResourceResponse> resourceResponseList = new List<ResourceResponse>();
            var files = Directory.GetFiles(GetFolderResourcePath(), "*.*", SearchOption.AllDirectories)
                .Where(file => new string[] { ".png", ".jpg", ".jpeg", ".wav", ".mp3", ".oog" }
                    .Contains(Path.GetExtension(file)))
                .ToList();

            for (int i = 0; i < files.Count; i++)
            {
                var response = CreateResourceResponse(files[i]);
                if (response != null) resourceResponseList.Add(response);
            }

            int downloaded = 0;

            if (resourceResponseList.Count > 0)
            {
                await UniTask.WhenAll(resourceResponseList.Select(_ => LoadExternalResourcesAsync(_)));

                foreach (var response in resourceResponseList)
                {
                    _Instance.m_ResourceResponseList.Add(response);
                    downloaded++;
                }
            }

            return _Instance.m_ResourceResponseList;
        }


        public static void ClearAllResource()
        {

            for (int i = 0; i < _Instance.m_ResourceResponseList.Count; i++)
            {
                if (_Instance.m_ResourceResponseList[i].m_Texture != null)
                    DestroyImmediate(_Instance.m_ResourceResponseList[i].m_Texture, true);
                if (_Instance.m_ResourceResponseList[i].m_AudioClip != null)
                    DestroyImmediate(_Instance.m_ResourceResponseList[i].m_AudioClip, true);
            }

            _Instance.m_ResourceResponseList.Clear();
            _Instance.m_ResourceResponseList = new List<ResourceResponse>();
        }

        public static void RemoveResource(string _name)
        {
            var response = _Instance.m_ResourceResponseList.FirstOrDefault(x => x.m_Name == _name);
            if (response != null)
            {
                if (response.m_Texture != null) DestroyImmediate(response.m_Texture, true);
                if (response.m_AudioClip != null) DestroyImmediate(response.m_AudioClip, true);
                _Instance.m_ResourceResponseList.Remove(response);
            }
        }


        // using UniTask
        public static async UniTask<Texture2D> GetTextureAsync(string _name)
        {
            var res = await GetResourceAsync(_name);
            return res != null ? res.m_Texture : null;
        }

        public static async UniTask<Texture2D[]> GetTexturesAsync(string[] _names)
        {
            var resArray = await GetResourcesAsync(_names);
            //return texture array from resource response array
            //if response is null then texture is null
            return resArray.Select(_ => _ != null ? _.m_Texture : null).ToArray();
        }

        public static async UniTask<AudioClip> GetAudioClipAsync(string _name)
        {
            var res = await GetResourceAsync(_name);
            return res != null ? res.m_AudioClip : null;
        }

        public static async UniTask<AudioClip[]> GetAudioClipsAsync(string[] _names)
        {
            var resArray = await GetResourcesAsync(_names);
            return resArray.Select(_ => _ != null ? _.m_AudioClip : null).ToArray();
        }

        public static async UniTask<Texture2D> GetTextureByPathAsync(string _path, string _overrideName)
        {
            var res = await GetResourceByPathAsync(_path, _overrideName);
            return res != null ? res.m_Texture : null;
        }

        public static async UniTask<AudioClip> GetAudioClipByPathAsync(string _path, string _overrideName)
        {
            var res = await GetResourceByPathAsync(_path, _overrideName);
            return res != null ? res.m_AudioClip : null;
        }


        public static async UniTask<ResourceResponse> GetResourceAsync(string _name)
        {
            try
            {


                await UniTask.Yield();


                var extension = Path.GetExtension(_name);
                var resourceType = GetResourceType(extension);


                //get from cache
                for (int i = 0; i < _Instance.m_ResourceResponseList.Count; i++)
                {
                    if (_Instance.m_ResourceResponseList[i].m_Name.Equals(_name) &&
                        _Instance.m_ResourceResponseList[i].m_ResourceType == resourceType)
                    {
                        await UniTask.WaitUntil(() => _Instance.m_ResourceResponseList[i].m_IsLoaded);

                        return _Instance.m_ResourceResponseList[i];
                    }
                }


                if (_Instance.m_ResourceStoreType == ResourceStoreType.ExternalResources)
                {
                    if (!Directory.Exists(GetFolderResourcePath()))
                    {
                        //no folder resource
                        // Debug.Log("No folder resource");
                        return null;
                    }


                    DirectoryInfo directoryInfo = new DirectoryInfo(GetFolderResourcePath());


                    var fileInfo = directoryInfo
                        .GetFiles("*" + extension, SearchOption.AllDirectories)
                        .FirstOrDefault(_file => _file.Name.Equals(_name));

                    if (fileInfo != null)
                    {
                        //init loading
                        var response = CreateResourceResponse(fileInfo.FullName);
                        _Instance.m_ResourceResponseList.Add(response);

                        //load and assign texture or audio to response
                        await LoadExternalResourcesAsync(response);
                        return response;
                    }
                }
                else if (_Instance.m_ResourceStoreType == ResourceStoreType.Addressable)
                {
#if PACKAGE_ADDRESSABLES_INSTALLED
                    Debug.Log($"GetResourceAsync: {resourceType} - {_name}");
                    switch (resourceType)
                    {
                        case ResourceResponse.ResourceType.Texture:
                            var texture = await Addressables.LoadAssetAsync<Texture2D>(_name).Task;
                            if (texture != null)
                            {
                                var response = new ResourceResponse
                                {
                                    m_Name = _name,
                                    m_Texture = texture,
                                    m_ResourceType = ResourceResponse.ResourceType.Texture
                                };
                                _Instance.m_ResourceResponseList.Add(response);
                                return response;
                            }
                            break;

                        case ResourceResponse.ResourceType.AudioClip:
                            var audio = await Addressables.LoadAssetAsync<AudioClip>(_name).Task;
                            if (audio != null)
                            {
                                var response = new ResourceResponse
                                {
                                    m_Name = _name,
                                    m_AudioClip = audio,
                                    m_ResourceType = ResourceResponse.ResourceType.AudioClip
                                };
                                _Instance.m_ResourceResponseList.Add(response);
                                return response;
                            }
                            break;
                    }
#else
                    Debug.LogError("Addressables package is not installed. Please install it first.");
                    return null;
#endif
                }

            }
            catch (Exception e)
            {
                throw e;
            }


            return null;
        }

        public static async UniTask<ResourceResponse> GetResourceByPathAsync(string _path, string _overrideName)
        {
            if (!File.Exists(_path))
            {
                Debug.LogError($"Not found file : {_path}");
                return null;
            }


            if (string.IsNullOrEmpty(_overrideName))
            {
                Debug.Log($"Override Name is empty");
                return null;
            }

            var fileName = Path.GetFileName(_path);
            var extension = Path.GetExtension(_path);
            var resourceType = GetResourceType(extension);
            //_overrideName = "test.jpg";

            var overrideExtension = Path.GetExtension(_overrideName);
            if (overrideExtension != extension)
            {
                Debug.LogError($"Override Name Extension is not match with file extension : {_overrideName}");
                return null;
            }

            await UniTask.Yield();
            ResourceResponse response = null;


            //get from cache
            for (int i = 0; i < _Instance.m_ResourceResponseList.Count; i++)
            {
                if (_Instance.m_ResourceResponseList[i].m_FilePath.Equals(_path) &&
                    _Instance.m_ResourceResponseList[i].m_ResourceType == resourceType)
                {
                    await UniTask.WaitUntil(() => _Instance.m_ResourceResponseList[i].m_IsLoaded);

                    return _Instance.m_ResourceResponseList[i];
                }
            }

            try
            {
                //init loading
                response = CreateResourceResponse(_path, _overrideName);
                _Instance.m_ResourceResponseList.Add(response);

                //load and assign texture or audio to response
                await LoadExternalResourcesAsync(response);
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            return null;
        }


        public static async UniTask<ResourceResponse[]> GetResourcesAsync(string[] _fileNames)
        {
            var responselist = new ResourceResponse[_fileNames.Length];
            for (int i = 0; i < _fileNames.Length; i++)
            {
                var fileName = _fileNames[i];

                var res = await GetResourceAsync(fileName);
                responselist[i] = res;
            }

            return responselist;
        }

        public static async UniTask<ResourceResponse[]> GetResourcesByPathAsync(string[] _paths,
            string[] _overrideNames = null)
        {
            var responselist = new ResourceResponse[_paths.Length];
            for (int i = 0; i < _paths.Length; i++)
            {
                var path = _paths[i];
                var overrideName = _overrideNames != null ? _overrideNames[i] : "";
                var res = await GetResourceByPathAsync(path, overrideName);
                responselist[i] = res;
            }

            return responselist;
        }


        //-------- Private Methods --------//
        private static ResourceResponse CreateResourceResponse(string _path, string _overrideName = "")
        {
            var fileInfo = new FileInfo(_path);

            var resourceType = GetResourceType(fileInfo.Extension);
            if (resourceType == ResourceResponse.ResourceType.None) return null;
            // Debug.Log($"===> {fileInfo.FullName} = {resourceType}");

            var audioType = AudioType.UNKNOWN;


            if (resourceType == ResourceResponse.ResourceType.AudioClip)
            {
                if (Regex.Match(fileInfo.Extension, ".ogg").Success)
                {
                    audioType = AudioType.OGGVORBIS;
                }
                else if (Regex.Match(fileInfo.Extension, ".wav").Success)
                {
                    audioType = AudioType.WAV;
                }
            }


            ResourceResponse response = new ResourceResponse
            {
                m_Name = _overrideName != "" ? _overrideName : fileInfo.Name,
                m_FilePath = fileInfo.FullName,
                m_ResourceType = resourceType,
                m_AudioType = audioType
            };

            return response;
        }

        public static string[] GetSearchPattern(ResourceResponse.ResourceType _type)
        {
            if (_type == ResourceResponse.ResourceType.Texture)
            {
                return new string[] { ".png", ".jpg", ".jpeg" };
            }
            else if (_type == ResourceResponse.ResourceType.AudioClip)
            {
                return new string[] { ".wav", ".mp3", ".ogg" };
            }

            return null;
        }

        private static ResourceResponse.ResourceType GetResourceType(string _extension)
        {
            if (Regex.Match(_extension, ".png|.jpg|.jpeg", RegexOptions.IgnoreCase).Success)
            {
                return ResourceResponse.ResourceType.Texture;
            }
            else if (Regex.Match(_extension, ".ogg|.wav|.mp3", RegexOptions.IgnoreCase).Success)
            {
                return ResourceResponse.ResourceType.AudioClip;
            }
            else
                return ResourceResponse.ResourceType.None;
        }


        private static async UniTask<ResourceResponse> LoadExternalResourcesAsync(ResourceResponse _dataInfo)
        {
            var filePath = $"file://{_dataInfo.m_FilePath}";
            if (_dataInfo.m_ResourceType == ResourceResponse.ResourceType.Texture)
            {
                //get texture
                var reqTexture = await UnityWebRequestTexture.GetTexture(filePath).SendWebRequest();
                var texture = DownloadHandlerTexture.GetContent(reqTexture);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = _dataInfo.m_Name;
                texture.Apply();
                _dataInfo.m_Texture = texture;


                return _dataInfo;
            }
            else if (_dataInfo.m_ResourceType == ResourceResponse.ResourceType.AudioClip)
            {
                //get audio
                var req = await UnityWebRequestMultimedia.GetAudioClip(filePath, _dataInfo.m_AudioType)
                    .SendWebRequest();
                var audio = DownloadHandlerAudioClip.GetContent(req);
                audio.name = _dataInfo.m_Name;
                _dataInfo.m_AudioClip = audio;

                return _dataInfo;
            }

            return null;
        }

        public static string GetFolderResourcePath()
        {
            return Path.Combine(Environment.CurrentDirectory, "Resources");
        }

        [Serializable]
        public class ResourceResponse
        {
            public enum ResourceType
            {
                None,
                Texture,
                AudioClip
            }

            public string m_Name;
            public string m_FilePath;
            public AudioClip m_AudioClip;
            public AudioType m_AudioType = AudioType.UNKNOWN;
            public Texture2D m_Texture;
            public ResourceType m_ResourceType = ResourceType.None;

            public bool m_IsLoaded => m_Texture != null || m_AudioClip != null;
        }
    }
}