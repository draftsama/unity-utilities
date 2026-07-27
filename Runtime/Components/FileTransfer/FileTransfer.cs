using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace Modules.Utilities
{
    /// <summary>
    /// Unity front end for <see cref="FileTransferServer"/> and <see cref="FileTransferClient"/>.
    /// One connection carries one transfer; there is no resume. See Documents/FileTransfer/README.md.
    /// </summary>
    public class FileTransfer : MonoBehaviour
    {
        #region Constants

        /// <summary>DataTransceiver action id reserved for bridge notifications.</summary>
        public const ushort ACTION_FILE_OFFER = 65100;

        #endregion

        #region Inspector

        [Header("Mode")]
        [SerializeField, Tooltip("Server listens; client dials. The server never initiates a connection.")]
        public bool m_IsServer = false;

        [SerializeField] public bool m_StartOnEnable = true;
        [SerializeField] public bool m_IsDebug = true;

        [Header("Connection")]
        [SerializeField, Tooltip("Client: server host. Left empty, it is copied from the control channel.")]
        public string m_Host = "127.0.0.1";

        [SerializeField, Tooltip("TCP port. Distinct from DataTransceiver's UDP port.")]
        public int m_Port = 55600;

        [SerializeField, Tooltip("Server: bind interface. Empty binds every interface.")]
        public string m_BindAddress = "";

        [SerializeField, Range(1000, 60000)] public int m_ConnectTimeoutMs = 5000;

        [Header("Folders")]
        [SerializeField, Tooltip("Server root. Relative paths resolve under Application.persistentDataPath.")]
        public string m_RootFolder = "FileTransfer";

        [SerializeField, Tooltip("Client download folder. Relative paths resolve under Application.persistentDataPath.")]
        public string m_DownloadFolder = "Downloads";

        [Header("Limits")]
        [SerializeField, Range(1, 4096)] public int m_MaxFileSizeMB = 512;
        [SerializeField, Range(1, 32)] public int m_MaxConcurrentTransfers = 4;
        [SerializeField, Range(4, 1024)] public int m_BufferSizeKB = 80;
        [SerializeField, Range(1000, 300000)] public int m_IdleTimeoutMs = 30000;

        [Header("Policy")]
        [SerializeField] public bool m_AllowUpload = true;
        [SerializeField] public bool m_AllowDownload = true;

        [SerializeField, Tooltip("SHA-256 verification. Off saves CPU on trusted links.")]
        public bool m_VerifyHash = true;

        [SerializeField, Tooltip("Optional. Crosses the wire in plaintext - a guard against cross-talk, not a security boundary.")]
        public string m_SharedSecret = "";

        [Header("Control Channel (optional)")]
        [SerializeField, Tooltip("Assign to inherit the host address and to send or receive file-available notifications.")]
        private DataTransceiver m_ControlChannel;

        [Header("Status (read-only)")]
        [SerializeField] private bool m_IsRunning;
        [SerializeField] private int m_ServerPort;
        [SerializeField] private int m_ActiveTransfers;

        [Header("Events")]
        [SerializeField] private UnityEvent<FileTransferProgress> m_OnTransferStarted = new UnityEvent<FileTransferProgress>();
        [SerializeField] private UnityEvent<FileTransferProgress> m_OnTransferProgress = new UnityEvent<FileTransferProgress>();
        [SerializeField] private UnityEvent<FileTransferResult> m_OnTransferCompleted = new UnityEvent<FileTransferResult>();
        [SerializeField] private UnityEvent<FileTransferResult> m_OnTransferFailed = new UnityEvent<FileTransferResult>();
        [SerializeField] private UnityEvent<FileOffer> m_OnFileOffered = new UnityEvent<FileOffer>();
        [SerializeField] private UnityEvent m_OnServerReady = new UnityEvent();

        #endregion

        #region Internal state

        private FileTransferServer _server;
        private FileTransferClient _client;
        private CancellationTokenSource _cts;
        private IDisposable _offerSubscription;

        private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();

        #endregion

        #region Public surface

        public UnityEvent<FileTransferProgress> OnTransferStarted => m_OnTransferStarted;
        public UnityEvent<FileTransferProgress> OnTransferProgress => m_OnTransferProgress;
        public UnityEvent<FileTransferResult> OnTransferCompleted => m_OnTransferCompleted;
        public UnityEvent<FileTransferResult> OnTransferFailed => m_OnTransferFailed;
        public UnityEvent<FileOffer> OnFileOffered => m_OnFileOffered;
        public UnityEvent OnServerReady => m_OnServerReady;

        public bool IsServer => m_IsServer;
        public bool IsRunning => m_IsRunning;
        public int ServerPort => m_ServerPort;
        public int ActiveTransfers => m_ActiveTransfers;

        /// <summary>Server root as an absolute path.</summary>
        public string RootFolderPath => ResolveFolder(m_RootFolder);

        /// <summary>Client download folder as an absolute path.</summary>
        public string DownloadFolderPath => ResolveFolder(m_DownloadFolder);

        #endregion

        #region Unity lifecycle

        private void OnEnable()
        {
            if (m_StartOnEnable) StartAsync().Forget();
        }

        private void OnDisable() => StopInternal();
        private void OnDestroy() => StopInternal();

        private void Update()
        {
            // Drain main-thread dispatch queue.
            while (_mainQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Log($"Main dispatch error: {ex.Message}"); }
            }

            if (_server != null) m_ActiveTransfers = _server.ActiveTransfers;
        }

        #endregion

        #region Lifecycle

        public async UniTask StartAsync()
        {
            if (m_IsRunning) return;

            _cts = new CancellationTokenSource();

            if (m_ControlChannel != null)
            {
                if (!m_IsServer && string.IsNullOrEmpty(m_Host))
                {
                    m_Host = m_ControlChannel.m_Host;
                    Log($"Host inherited from the control channel: {m_Host}");
                }

                _offerSubscription = m_ControlChannel.Subscribe(ACTION_FILE_OFFER, OnOfferReceived);
            }

            if (m_IsServer) StartServerInternal();
            else BuildClient();

            m_IsRunning = true;
            await UniTask.CompletedTask;
        }

        public UniTask StartServerAsync()
        {
            if (!m_IsServer) throw new InvalidOperationException("StartServerAsync requires m_IsServer = true");

            if (_server == null) StartServerInternal();
            m_IsRunning = true;
            return UniTask.CompletedTask;
        }

        public UniTask StopServerAsync()
        {
            StopInternal();
            return UniTask.CompletedTask;
        }

        private void StartServerInternal()
        {
            _server = new FileTransferServer(new FileTransferServerOptions
            {
                RootFolder = RootFolderPath,
                Port = m_Port,
                BindAddress = m_BindAddress,
                MaxFileSizeBytes = (long)m_MaxFileSizeMB * 1024 * 1024,
                MaxConcurrentTransfers = m_MaxConcurrentTransfers,
                AllowUpload = m_AllowUpload,
                AllowDownload = m_AllowDownload,
                BufferSize = m_BufferSizeKB * 1024,
                VerifyHash = m_VerifyHash,
                IdleTimeoutMs = m_IdleTimeoutMs,
                SharedSecret = m_SharedSecret,
            });

            _server.Log += message => Dispatch(() => Log(message));
            _server.Progress += p => Dispatch(() => m_OnTransferProgress?.Invoke(p));
            _server.FileReceived += r => Dispatch(() => RaiseResult(r));
            _server.FileServed += r => Dispatch(() => RaiseResult(r));

            _server.Start();
            m_ServerPort = _server.Port;

            Dispatch(() => m_OnServerReady?.Invoke());
        }

        private void BuildClient()
        {
            _client = new FileTransferClient(new FileTransferClientOptions
            {
                Host = m_Host,
                Port = m_Port,
                DownloadFolder = DownloadFolderPath,
                ConnectTimeoutMs = m_ConnectTimeoutMs,
                BufferSize = m_BufferSizeKB * 1024,
                VerifyHash = m_VerifyHash,
                IdleTimeoutMs = m_IdleTimeoutMs,
                SharedSecret = m_SharedSecret,
            });

            _client.Log += message => Dispatch(() => Log(message));
        }

        private void StopInternal()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;

            try { _offerSubscription?.Dispose(); } catch { }
            _offerSubscription = null;

            try { _server?.Dispose(); } catch { }
            _server = null;
            _client = null;

            m_IsRunning = false;
            m_ServerPort = 0;
            m_ActiveTransfers = 0;
        }

        #endregion

        #region Transfers

        public async UniTask<FileTransferResult> UploadAsync(
            string localPath,
            string remoteFolder = "",
            IProgress<FileTransferProgress> progress = null,
            CancellationToken ct = default)
        {
            if (m_IsServer) throw new InvalidOperationException("UploadAsync is client-only. Set m_IsServer = false.");
            if (_client == null) BuildClient();

            var relay = BuildProgressRelay(progress);
            Dispatch(() => m_OnTransferStarted?.Invoke(new FileTransferProgress(Path.GetFileName(localPath), 0, 0, 0f)));

            FileTransferResult result;
            using (var scope = LinkedScope(ct))
            {
                result = await _client.UploadAsync(localPath, remoteFolder, relay, scope.Token).AsUniTask();
            }

            Dispatch(() => RaiseResult(result));
            return result;
        }

        public async UniTask<FileTransferResult> DownloadAsync(
            string remoteName,
            string remoteFolder = "",
            string localSavePath = null,
            IProgress<FileTransferProgress> progress = null,
            CancellationToken ct = default)
        {
            if (m_IsServer) throw new InvalidOperationException("DownloadAsync is client-only. Set m_IsServer = false.");
            if (_client == null) BuildClient();

            var relay = BuildProgressRelay(progress);
            Dispatch(() => m_OnTransferStarted?.Invoke(new FileTransferProgress(remoteName, 0, 0, 0f)));

            FileTransferResult result;
            using (var scope = LinkedScope(ct))
            {
                result = await _client.DownloadAsync(remoteName, remoteFolder, localSavePath, relay, scope.Token).AsUniTask();
            }

            Dispatch(() => RaiseResult(result));
            return result;
        }

        #endregion

        #region Control channel bridge

        /// <summary>
        /// Tells one peer that a file is available. The peer decides whether to call
        /// <see cref="DownloadAsync"/>; nothing is transferred by this call.
        /// </summary>
        public UniTask NotifyFileAvailableAsync(int peerId, string remoteName, string remoteFolder = "")
        {
            if (m_ControlChannel == null)
                throw new InvalidOperationException("NotifyFileAvailableAsync requires m_ControlChannel to be assigned");
            if (!m_IsServer)
                throw new InvalidOperationException("NotifyFileAvailableAsync is server-only");

            var offer = new FileOffer { Name = remoteName, Folder = remoteFolder ?? string.Empty };

            var path = Path.Combine(RootFolderPath, offer.Folder, remoteName);
            if (File.Exists(path)) offer.Size = new FileInfo(path).Length;

            return m_ControlChannel.SendToAsync(peerId, ACTION_FILE_OFFER,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(offer)));
        }

        private void OnOfferReceived(DataTransceiver.DataMessage message)
        {
            try
            {
                var offer = message.As<FileOffer>();
                if (offer == null) return;

                Log($"File offered: {offer.Name} ({offer.Size}B)");
                m_OnFileOffered?.Invoke(offer);
            }
            catch (Exception ex)
            {
                Log($"Malformed file offer: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private IProgress<FileTransferProgress> BuildProgressRelay(IProgress<FileTransferProgress> caller)
        {
            return new Progress<FileTransferProgress>(p =>
            {
                caller?.Report(p);
                Dispatch(() => m_OnTransferProgress?.Invoke(p));
            });
        }

        private void RaiseResult(FileTransferResult result)
        {
            if (result == null) return;

            if (result.IsSuccess)
            {
                Log(result.ToString());
                m_OnTransferCompleted?.Invoke(result);
            }
            else
            {
                Log("FAILED " + result);
                m_OnTransferFailed?.Invoke(result);
            }
        }

        /// <summary>
        /// Joins the caller's token to the component's own, so disabling the GameObject
        /// cancels in-flight transfers. The returned source is disposed by the caller's
        /// using-block - linked sources leak a registration on their parent otherwise.
        /// </summary>
        private CancellationTokenSource LinkedScope(CancellationToken ct)
        {
            if (_cts == null) return CancellationTokenSource.CreateLinkedTokenSource(ct);

            return ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct)
                : CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        }

        private string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return Application.persistentDataPath;
            return Path.IsPathRooted(folder) ? folder : Path.Combine(Application.persistentDataPath, folder);
        }

        private void Dispatch(Action action) => _mainQueue.Enqueue(action);

        private void Log(object message)
        {
            if (!m_IsDebug) return;
            Debug.Log($"[{(m_IsServer ? "FT-Server" : "FT-Client")}] {message}");
        }

        #endregion
    }
}
