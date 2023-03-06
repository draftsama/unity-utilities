using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UniRx;
using UnityEngine;


namespace Modules.Utilities
{

    public class ResourceManager : MonoBehaviour
    {

        private static ResourceManager _Instance;

        [SerializeField] private List<ResourceResponse> m_ResourceResponseList;
        private void Awake()
        {
            _Instance = this;
            m_ResourceResponseList = new List<ResourceResponse>();

            DontDestroyOnLoad(gameObject);
        }
        void Start()
        {
        }
        private static ResourceManager GetInstance()
        {
            if (_Instance == null)
            {
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
        public static IObservable<List<ResourceResponse>> LoadAllResource()
        {
            return Observable.Create<List<ResourceResponse>>(_observer =>
            {
                var instance = GetInstance();
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

                IDisposable disposable = null;
                int downloaded = 0;

                if (resourceResponseList.Count > 0)
                {

                    disposable = resourceResponseList
                        .Select(_ => LoadResources(_))
                        .ToList()
                        .Concat()
                        .Subscribe(_ =>
                        {
                            instance.m_ResourceResponseList.Add(_);
                            downloaded++;
                            if (downloaded >= resourceResponseList.Count)
                            {
                                _observer.OnNext(instance.m_ResourceResponseList);
                                _observer.OnCompleted();
                            }
                        }, _observer.OnError);
                }
                else
                {
                    //no file
                    _observer.OnNext(instance.m_ResourceResponseList);
                    _observer.OnCompleted();
                }

                return Disposable.Create(() => { disposable?.Dispose(); });
            });
        }


        public static IObservable<List<ResourceResponse>> LoadResourceByPaths(string[] _paths)
        {
            return Observable.Create<List<ResourceResponse>>(_observer =>
            {
                var instance = GetInstance();

                IDisposable disposable = null;
                List<ResourceResponse> responselist = new List<ResourceResponse>();

                for (int i = 0; i < _paths.Length; i++)
                {
                    var path = _paths[i];
                    if (!File.Exists(path)) continue;

                    var exists = instance.m_ResourceResponseList.Where(_ => _.m_FilePath == path).FirstOrDefault();
                    if (exists == null)
                    {
                        FileInfo fileInfo = new FileInfo(path);

                        if (fileInfo != null)
                        {
                            responselist.Add(CreateResourceResponse(fileInfo.FullName));
                        }
                    }
                }

                int downloaded = 0;

                if (responselist.Count > 0)
                {

                    disposable = responselist
                        .Select(_ => LoadResources(_))
                        .ToList()
                        .Concat()
                        .Subscribe(_ =>
                        {
                            instance.m_ResourceResponseList.Add(_);
                            downloaded++;
                            if (downloaded >= responselist.Count)
                            {
                                _observer.OnNext(responselist);
                                _observer.OnCompleted();
                            }
                        }, _observer.OnError);
                }
                else
                {
                    //no file
                    _observer.OnNext(null);
                    _observer.OnCompleted();
                }

                return Disposable.Create(() => { disposable?.Dispose(); });
            });
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


        public static IObservable<List<ResourceResponse>> GetResources(string[] _name)
        {
            return Observable.Create<List<ResourceResponse>>(_observer =>
            {
                var instance = GetInstance();
                IDisposable disposable = null;
                List<ResourceResponse> responselist = new List<ResourceResponse>();
                List<ResourceResponse> loaderList = new List<ResourceResponse>();


                DirectoryInfo directoryInfo = new DirectoryInfo(GetFolderResourcePath());


                for (int i = 0; i < _name.Length; i++)
                {
                    var fileName = _name[i];
                    var resource = instance.m_ResourceResponseList.FirstOrDefault(x => x.m_Name == fileName);
                    if (resource != null)
                    {
                        responselist.Add(resource);
                    }
                    else
                    {
                        if (directoryInfo == null || !Directory.Exists(GetFolderResourcePath()))
                            continue;


                        var extension = new FileInfo(fileName).Extension;
                        var fileInfo = directoryInfo.GetFiles("*" + extension, SearchOption.AllDirectories).FirstOrDefault(x => x.Name == fileName);
                        Debug.Log($"Loaded file '{fileInfo.FullName}'");
                        if (fileInfo != null)
                            loaderList.Add(CreateResourceResponse(fileInfo.FullName));
                    }
                }
                var downloaded = 0;
                if (loaderList.Count > 0)
                {
                    disposable = loaderList
                                           .Select(_ => LoadResources(_))
                                           .ToList()
                                           .Concat()
                                           .Subscribe(_ =>
                                           {
                                               instance.m_ResourceResponseList.Add(_);
                                               downloaded++;
                                               if (downloaded >= loaderList.Count)
                                               {

                                                   responselist.AddRange(loaderList);
                                                   _observer.OnNext(responselist);
                                                   _observer.OnCompleted();
                                               }
                                           }, _observer.OnError);


                }
                else
                {
                    _observer.OnNext(responselist);
                    _observer.OnCompleted();
                }


                return Disposable.Create(() => { disposable?.Dispose(); });

            });
        }


        public static IObservable<ResourceResponse> GetResource(string _name, ResourceResponse.ResourceType _type)
        {
            return Observable.Create<ResourceResponse>(_observer =>
            {
                var instance = GetInstance();
                ResourceResponse response = null;
                IDisposable disposable = null;

                for (int i = 0; i < instance.m_ResourceResponseList.Count; i++)
                {


                    if (instance.m_ResourceResponseList[i].m_Name.Equals(_name) && instance.m_ResourceResponseList[i].m_ResourceType == _type)
                    {
                        response = instance.m_ResourceResponseList[i];
                        break;
                    }
                }

                if (response != null)
                {

                    _observer.OnNext(response);
                    _observer.OnCompleted();
                }
                else
                {
                    if (!Directory.Exists(GetFolderResourcePath()))
                    {
                        //no folder resource
                        _observer.OnNext(null);
                        _observer.OnCompleted();
                        return Disposable.Create(() => { disposable?.Dispose(); });

                    }

                    DirectoryInfo directoryInfo = new DirectoryInfo(GetFolderResourcePath());

                    string searchPattern = GetSearchPattern(_type);

                    if (directoryInfo == null)
                    {
                        //no file
                        _observer.OnNext(null);
                        _observer.OnCompleted();
                        return Disposable.Create(() => { disposable?.Dispose(); });

                    }



                    var parts = Regex.Split(_name, @"\.");
                    if (parts.Length < 2)
                    {
                        _observer.OnNext(null);
                        _observer.OnCompleted();
                        return Disposable.Create(() => { disposable?.Dispose(); });
                    }
                    var extension = parts.Last();
                    var fileInfo = directoryInfo.GetFiles("*." + extension, SearchOption.AllDirectories)
                    .Where(_file => _file.Name.Equals(_name))
                    .FirstOrDefault();

                    if (fileInfo != null)
                    {

                        disposable = LoadResources(CreateResourceResponse(fileInfo.FullName)).Subscribe(_ =>
                         {
                             instance.m_ResourceResponseList.Add(_);
                             _observer.OnNext(_);
                             _observer.OnCompleted();
                         }, _observer.OnError);
                    }
                    else
                    {
                        _observer.OnNext(null);
                        _observer.OnCompleted();
                        return Disposable.Create(() => { disposable?.Dispose(); });
                    }



                }

                return Disposable.Create(() => { disposable?.Dispose(); });
            });



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





            ResourceResponse response = new ResourceResponse();
            response.m_Name = fileInfo.Name;
            response.m_FilePath = fileInfo.FullName;
            response.m_ResourceType = resourceType;
            response.m_AudioType = audioType;

            return response;
        }
        private static string GetSearchPattern(ResourceResponse.ResourceType _type)
        {
            string searchPattern = string.Empty;
            if (_type == ResourceResponse.ResourceType.Texture)
            {
                searchPattern = ".png|.jpg|.jpeg";
            }
            else if (_type == ResourceResponse.ResourceType.AudioClip)
            {
                searchPattern = ".ogg|.wav|.mp3";

            }
            return searchPattern;

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
        private static IObservable<ResourceResponse> LoadResources(ResourceResponse _dataInfo)
        {
            return Observable.Create<ResourceResponse>(_observer =>
            {

                IDisposable disposable = null;
                var filePath = $"file://{_dataInfo.m_FilePath}";
                if (_dataInfo.m_ResourceType == ResourceResponse.ResourceType.Texture)
                {
                    disposable = ObservableWebRequest
                    .GetTexture(filePath)
                    .Subscribe(_texture =>
                       {
                           _texture.wrapMode = TextureWrapMode.Clamp;
                           _texture.Apply();
                           _dataInfo.m_Texture = _texture;
                           _observer.OnNext(_dataInfo);
                           _observer.OnCompleted();
                       }, _observer.OnError);


                }
                else if (_dataInfo.m_ResourceType == ResourceResponse.ResourceType.AudioClip)
                {
                    disposable = ObservableWebRequest
                    .GetAudioClip(filePath, _dataInfo.m_AudioType)
                    .Subscribe(_audio =>
                    {
                        _dataInfo.m_AudioClip = _audio;
                        _dataInfo.m_AudioClip.name = _dataInfo.m_Name;
                        _observer.OnNext(_dataInfo);
                        _observer.OnCompleted();
                    }, _observer.OnError);
                }


                return Disposable.Create(() =>
                {
                    disposable?.Dispose();
                });

            });
        }


        private static string GetFolderResourcePath()
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
        }

    }
}