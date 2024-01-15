using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

namespace Modules.Utilities
{

    [DefaultExecutionOrder(-100)]
    public class ActivateManager : MonoBehaviour
    {
        private string _SavePath;
        [SerializeField] private string m_Code = "CODE";
        [SerializeField] private string m_Password = "Password";
        [SerializeField] private string m_EncyptKey = "DraftSama";
        [SerializeField] private string m_Scene = "";
        [SerializeField] private bool m_AutoLoadScene = false;

        [Header("UI")][SerializeField] private CanvasGroup m_ContentCanvas;
        [SerializeField] private InputField m_InputField;
        [SerializeField] private Text m_MsgText;
        [SerializeField] private Button m_Button;

        private static ActivateManager _Instance;
        public static ActivateManager Instance => _Instance;

        public static bool IsActivated { get; private set; }

        public UnityEvent _OnActivated = new UnityEvent();
        private void Awake()
        {
            _Instance = this;
            IsActivated = false;
        }

        private void OnDestroy()
        {
            _Instance = null;
        }


        private void Start()
        {
            var folderPath = Directory.GetParent(Application.dataPath);
            _SavePath = Path.Combine(folderPath.FullName, "activate");

            m_Button.OnClickAsAsyncEnumerable().ForEachAsync(_ =>
                        {
                            if (m_InputField.text.Equals(m_Password))
                            {
                                m_ContentCanvas.SetAlpha(0);
                                CreateActivateFile();
                                OnActivated();
                            }
                            else
                            {
                                m_MsgText.text = "This code is not match";

                            }
                        }, this.GetCancellationTokenOnDestroy()).Forget();


            m_ContentCanvas.SetAlpha(0);
            CheckActivateFile();
        }
        private void OnActivated()
        {
            Debug.Log("Activated");
            IsActivated = true;
            _OnActivated?.Invoke();

            if (m_AutoLoadScene)
            {
                if (string.IsNullOrEmpty(m_Scene))
                {
                    Debug.LogWarning($"Scene is empty");
                    return;
                }
                SceneManager.LoadSceneAsync(m_Scene, LoadSceneMode.Single);

            }
        }
        public void ShowActivateUI(string _msg)
        {
            m_InputField.text = String.Empty;
            m_MsgText.text = _msg;
            m_ContentCanvas.SetAlpha(1);
        }

        private void CreateActivateFile()
        {
            ActivateData activateData = new ActivateData();
            activateData.info = new ActivateInfo(m_Code, m_Password, SystemInfo.deviceUniqueIdentifier);
            activateData.id = Guid.NewGuid().ToString();
            UpdateActivateFile(activateData);


            Debug.Log("Create Activate Completed");
        }



        public void CheckActivateFile()
        {
            var folderPath = Directory.GetParent(Application.dataPath);
            var filePath = Path.Combine(folderPath.FullName, "activate");


            if (!File.Exists(filePath))
            {
                ShowActivateUI("Cannot found activate file");
                return;
            }

            var rawText = String.Empty;
            var jsonString = String.Empty;
            ActivateData activateData = null;

            try
            {
                rawText = File.ReadAllText(filePath);
                jsonString = AesEncryptor.Decrypt(rawText, m_EncyptKey);

                activateData = JsonUtility.FromJson<ActivateData>(jsonString);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message);
                ShowActivateUI(e.Message);
                return;
            }
#if UNITY_EDITOR
            Debug.Log(jsonString);
#endif

            if (activateData != null)
            {
                if (activateData.info.udid == SystemInfo.deviceUniqueIdentifier && activateData.info.code == m_Code && activateData.info.password == m_Password)
                {

                    if (activateData.info.block)
                    {
                        //block
                        UpdateActivateFile(activateData);
                        return;
                    }
                    else
                    {
                        UpdateActivateFile(activateData);
                        OnActivated();
                    }

                }
                else
                {
                    //udid and appname not match
                    ShowActivateUI("Current activate file is not match");

                }
            }
            else
            {
                ShowActivateUI("Current activate file is not match");

            }
        }




        private void UpdateActivateFile(ActivateData _data)
        {
            if (_data != null)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        var jsonString = JsonUtility.ToJson(_data);
                        string encryptString = AesEncryptor.Encrypt(jsonString, m_EncyptKey);
                        File.WriteAllText(_SavePath, encryptString);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError(e.Message);
                        throw;
                    }
                });

                thread.Start();
            }
            else
            {
                Debug.Log("No Activate Data");
            }
        }

        public IUniTaskAsyncEnumerable<AsyncUnit> OnLoadRequireCompleted(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnActivated, _token);
        }
        [Serializable]
        public class ActivateData
        {
            public ActivateInfo info;
            public string id;

            public ActivateData()
            {
            }

            public ActivateData(string _id, ActivateInfo _info)
            {
                this.id = _id;
                this.info = _info;
            }
        }

        [Serializable]
        public class ActivateInfo
        {
            public string appName;
            public string code;
            public string password;
            public string version;
            public string udid;
            public bool block = false;
            public string create_at;
            public string expired_at;

            public ActivateInfo()
            {
            }

            public ActivateInfo(string _code, string password, string _udid)
            {
                this.appName = Application.productName;
                this.version = Application.version;
                this.udid = _udid;
                this.code = _code;
                this.password = password;
                this.create_at = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            }
        }
    }

}