using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;


namespace Modules.Utilities
{

    public class ResourceManager : MonoBehaviour
    {

        private static ResourceManager _Instance;

        [SerializeField] private ResourceSettingAssets m_ResourceSettingAssets;

        [SerializeField] private List<ResourceResponse> m_ResourceResponseList;
        private void Awake()
        {
            if (_Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _Instance = this;
            m_ResourceResponseList = new List<ResourceResponse>();

            if (m_ResourceSettingAssets == null)
            {
                ResourceSettingAssets[] resourceSettingAssets = Resources.LoadAll<ResourceSettingAssets>("");

                if (resourceSettingAssets.Length > 0)
                {
                    m_ResourceSettingAssets = resourceSettingAssets[0];
                }
                else
                {
                    Debug.LogError("Not found ResourceSettingAssets in Resources Folder");
                }
            }

            DontDestroyOnLoad(gameObject);
        }

        private static ResourceManager GetInstance()
        {
            if (_Instance == null)
            {
#if UNITY_2022_3_OR_NEWER
                _Instance = FindFirstObjectByType<ResourceManager>();
#else
                _Instance =  FindObjectOfType<ResourceManager>();
#endif


                if (_Instance != null) return _Instance;

                GameObject go = new GameObject("ResourceManager", typeof(ResourceManager));
                _Instance = go.GetComponent<ResourceManager>();
            }

            return _Instance;
        }
        private void OnDestroy()
        {
            _Instance = null;
        }



        //-------- Public Methods --------//
        public static async UniTask<List<ResourceResponse>> LoadAllResource()
        {
            var instance = GetInstance();

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
                    instance.m_ResourceResponseList.Add(response);
                    downloaded++;
                }
            }

            return instance.m_ResourceResponseList;
        }



        public static void ClearAllResource()
        {
            var instance = GetInstance();
            for (int i = 0; i < instance.m_ResourceResponseList.Count; i++)
            {
                if (instance.m_ResourceResponseList[i].m_Texture != null) DestroyImmediate(instance.m_ResourceResponseList[i].m_Texture, true);
                if (instance.m_ResourceResponseList[i].m_AudioClip != null) DestroyImmediate(instance.m_ResourceResponseList[i].m_AudioClip, true);
            }

            instance.m_ResourceResponseList.Clear();
            instance.m_ResourceResponseList = new List<ResourceResponse>();

        }

        public static void RemoveResource(string _name)
        {
            var instance = GetInstance();
            var response = instance.m_ResourceResponseList.FirstOrDefault(x => x.m_Name == _name);
            if (response != null)
            {
                if (response.m_Texture != null) DestroyImmediate(response.m_Texture, true);
                if (response.m_AudioClip != null) DestroyImmediate(response.m_AudioClip, true);
                instance.m_ResourceResponseList.Remove(response);
            }
        }



        // public static async UniTask<List<ResourceResponse>> GetResources(string[] _name)
        // {
        //     var instance = GetInstance();
        //     List<ResourceResponse> responselist = new List<ResourceResponse>();
        //     Queue<ResourceResponse> loaderList = new Queue<ResourceResponse>();

        //     DirectoryInfo directoryInfo = new DirectoryInfo(GetFolderResourcePath());

        //     for (int i = 0; i < _name.Length; i++)
        //     {
        //         var fileName = _name[i];
        //         var resource = instance.m_ResourceResponseList.FirstOrDefault(x => x.m_Name == fileName);
        //         if (resource != null)
        //         {
        //             //add existing resource
        //             responselist.Add(resource);
        //         }
        //         else
        //         {
        //             if (directoryInfo == null || !Directory.Exists(GetFolderResourcePath()))
        //                 continue;

        //             var extension = new FileInfo(fileName).Extension;
        //             var fileInfo = directoryInfo.GetFiles("*" + extension, SearchOption.AllDirectories).FirstOrDefault(x => x.Name == fileName);
        //             if (fileInfo != null)
        //                 loaderList.Enqueue(CreateResourceResponse(fileInfo.FullName));
        //         }
        //     }

        //     while (loaderList.Count > 0)
        //     {
        //         //load resource
        //         var response = loaderList.Dequeue();
        //         await LoadResourcesAsync(response);
        //         instance.m_ResourceResponseList.Add(response);
        //         responselist.Add(response);
        //     }

        //     return responselist;
        // }




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


        public static async UniTask<ResourceResponse> GetResourceAsync(string _name)
        {

            var instance = GetInstance();
            await UniTask.Yield();


            ResourceResponse response = null;



            var extension = new FileInfo(_name).Extension;
            var resourceType = GetResourceType(extension);


            //get from cache
            for (int i = 0; i < instance.m_ResourceResponseList.Count; i++)
            {
                if (instance.m_ResourceResponseList[i].m_Name.Equals(_name) && instance.m_ResourceResponseList[i].m_ResourceType == resourceType)
                {
                    await UniTask.WaitUntil(() => instance.m_ResourceResponseList[i].m_IsLoaded);

                    return instance.m_ResourceResponseList[i];

                }
            }


            try
            {

                if (instance.m_ResourceSettingAssets.m_ResourceStoreType == ResourceStoreType.ExternalResources)
                {

                    if (!Directory.Exists(GetFolderResourcePath()))
                    {
                        //no folder resource
                        // Debug.Log("No folder resource");
                        return null;
                    }

                    DirectoryInfo directoryInfo = new DirectoryInfo(GetFolderResourcePath());
                    // string searchPattern = GetSearchPattern(resourceType);

                    if (directoryInfo == null)
                    {
                        //no file
                        // Debug.Log("No file");
                        return null;
                    }



                    var fileInfo = directoryInfo.GetFiles("*" + extension, SearchOption.AllDirectories)
                    .Where(_file => _file.Name.Equals(_name))
                    .FirstOrDefault();

                    if (fileInfo != null)
                    {
                        //init loading
                        response = CreateResourceResponse(fileInfo.FullName);
                        instance.m_ResourceResponseList.Add(response);

                        //load and assign texture or audio to response
                        await LoadExternalResourcesAsync(response);
                        return response;
                    }


                }
                else if (instance.m_ResourceSettingAssets.m_ResourceStoreType == ResourceStoreType.Addressable)
                {
                    switch (resourceType)
                    {
                        case ResourceResponse.ResourceType.Texture:
                            var texture = await Addressables.LoadAssetAsync<Texture2D>(_name).Task;
                            if (texture != null)
                            {
                                response = new ResourceResponse
                                {
                                    m_Name = _name,
                                    m_Texture = texture,
                                    m_ResourceType = ResourceResponse.ResourceType.Texture
                                };
                                instance.m_ResourceResponseList.Add(response);
                                return response;
                            }
                            break;

                        case ResourceResponse.ResourceType.AudioClip:
                            var audio = await Addressables.LoadAssetAsync<AudioClip>(_name).Task;
                            if (audio != null)
                            {
                                response = new ResourceResponse
                                {
                                    m_Name = _name,
                                    m_AudioClip = audio,
                                    m_ResourceType = ResourceResponse.ResourceType.AudioClip
                                };
                                instance.m_ResourceResponseList.Add(response);
                                return response;
                            }
                        break;
                    }



                }

            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }




            return null;




        }
        public static async UniTask<ResourceResponse> GetResourceFromPathAsync(string _path, string _resourceName = "")
        {

            if (!File.Exists(_path))
            {
                return null;
            }

            if (string.IsNullOrEmpty(_resourceName))
            {
                _resourceName = Path.GetFileName(_path);
            }

            //get from cache
            var instance = GetInstance();
            for (int i = 0; i < instance.m_ResourceResponseList.Count; i++)
            {
                //match by name or path
                if (instance.m_ResourceResponseList[i].m_Name.Equals(_resourceName) || instance.m_ResourceResponseList[i].m_FilePath.Equals(_path))
                {

                    await UniTask.WaitUntil(() => instance.m_ResourceResponseList[i].m_IsLoaded);
                    return instance.m_ResourceResponseList[i];
                }
            }





            var response = CreateResourceResponse(_path);


            if (response != null)
            {
                await LoadExternalResourcesAsync(response);
                return response;
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



        //-------- Private Methods --------//
        private static ResourceResponse CreateResourceResponse(string _path)
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
                else
                if (Regex.Match(fileInfo.Extension, ".wav").Success)
                {
                    audioType = AudioType.WAV;

                }
            }





            ResourceResponse response = new ResourceResponse
            {
                m_Name = fileInfo.Name,
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
                var req = await UnityWebRequestMultimedia.GetAudioClip(filePath, _dataInfo.m_AudioType).SendWebRequest();
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
                None, Texture, AudioClip
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