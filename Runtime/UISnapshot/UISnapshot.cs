using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Modules.Utilities;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.IO;

namespace Modules.Utilities
{
    public class UISnapshot : Singleton<UISnapshot>
    {
        public enum Layout
        {
            Auto,
            Landscape,
            Portrait,
        }

        [SerializeField] private Layout m_Layout = Layout.Auto;

        private CanvasGroup _CanvasGroup;
        private ISnapshotSource _SourceDelegate;

        [System.Serializable]
        public class SnapshotInfo
        {
            public Texture2D texture;
            public string eventName;
        }

        [SerializeField] private List<SnapshotInfo> m_SnapshotList = new List<SnapshotInfo>();


        [Header("Landscape")]
        [SerializeField] private GameObject m_LanscapeContent;
        [SerializeField] private List<RawImage> m_LanscapeSnapshotDisplay = new List<RawImage>();


        [Header("Portrait")]
        [SerializeField] private GameObject m_PortraitContent;
        [SerializeField] private List<RawImage> m_PortraitSnapshotDisplay = new List<RawImage>();
        List<RawImage> currentSnapshotDisplayList = null;

        void Start()
        {
            _CanvasGroup = GetComponent<CanvasGroup>();
            _CanvasGroup.SetAlpha(0);


            if (m_Layout == Layout.Auto)
            {
                var isPortait = Screen.height > Screen.width;

                m_LanscapeContent.SetActive(!isPortait);
                m_PortraitContent.SetActive(isPortait);

                currentSnapshotDisplayList = isPortait ? m_PortraitSnapshotDisplay : m_LanscapeSnapshotDisplay;


            }
            else if (m_Layout == Layout.Landscape)
            {
                m_LanscapeContent.SetActive(true);
                m_PortraitContent.SetActive(false);
                currentSnapshotDisplayList = m_LanscapeSnapshotDisplay;

            }
            else if (m_Layout == Layout.Portrait)
            {

                m_LanscapeContent.SetActive(false);
                m_PortraitContent.SetActive(true);
                currentSnapshotDisplayList = m_PortraitSnapshotDisplay;


            }

        }

        public async UniTask Show(int _duration, CancellationToken _token)
        {


            _CanvasGroup.SetAlpha(0);

            //get texture snapshots and random


            if (m_SnapshotList.Count > 0)
            {
                //if request events is not null, filter snapshots
                var random = new System.Random();
                var datas = m_SnapshotList.OrderBy(x => random.Next()).ToList();



                Debug.Log($"Snapshots : {datas.Count} / Displays : {currentSnapshotDisplayList.Count}");

                var folder = Path.Combine(System.Environment.CurrentDirectory, "Snapshots");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var count = Mathf.Min(datas.Count, currentSnapshotDisplayList.Count);
                for (int i = 0; i < count; i++)
                {
                    var data = datas[i];
                    currentSnapshotDisplayList[i].texture = data.texture;

                    var path = Path.Combine(folder, $"snapshot_{System.DateTime.Now.ToString("yyyyMMddHHmmss")}_{i}.png");
                    Debug.Log($"Save snapshot : {path}");
                    await data.texture.SaveFileAsync(path);
                }

                await _CanvasGroup.LerpAlphaAsync(300, 1f, _token: _token);

                await UniTask.Delay(_duration, cancellationToken: _token);

                await _CanvasGroup.LerpAlphaAsync(300, 0f, _token: _token);

                ClearSnapshots();

            }


        }

        public int SnapshotDisplayCount => currentSnapshotDisplayList.Count; 

        public void ClearSnapshots()
        {
            foreach (var p in m_SnapshotList)
                if (p.texture != null)
                    DestroyImmediate(p.texture, true);

            m_SnapshotList.Clear();
        }

        public void SetSourceDelegate(ISnapshotSource _source)
        {
            _SourceDelegate = _source;
        }

        public bool TakeSnapshot(string _event)
        {
            if (_SourceDelegate == null)
            {
                Debug.LogWarning("[UISnapshot] TakeSnapshot skipped: no ISnapshotSource set");
                return false;
            }

            var texture = _SourceDelegate.GetTexture2D();
            if (texture == null)
                return false;

            Debug.Log($"[UISnapshot] TakeSnapshot : {_event}");

            m_SnapshotList.Add(new SnapshotInfo()
            {
                texture = texture,
                eventName = _event
            });

            return true;
        }


        public void SnapshotEachTime(float currentTime, float totalTime, int totalSnapshots)
        {


            float progressTime = currentTime / (totalTime * 0.9f);
            progressTime = Mathf.Clamp01(progressTime);

            // Snapshot k fires at progress k/N, so the last one lands at 90% of totalTime.
            float nextSnapshotAt = (m_SnapshotList.Count + 1) / (float)totalSnapshots;

            if (progressTime >= nextSnapshotAt)
            {
                TakeSnapshot($"TimeBasedEvent {m_SnapshotList.Count + 1}");
            }


        }


        public async UniTaskVoid ProcessSnapshotEachTime(float totalTime, int totalSnapshots, CancellationToken _token)
        {
            if (totalSnapshots <= 0 || totalTime <= 0)
                return;

            ClearSnapshots();

            try
            {
                float currentTime = 0;
                while (!_token.IsCancellationRequested && m_SnapshotList.Count < totalSnapshots)
                {
                    currentTime += Time.deltaTime;
                    SnapshotEachTime(currentTime, totalTime, totalSnapshots);
                    await UniTask.Yield(PlayerLoopTiming.Update, _token);
                }
            }
            catch (System.Exception ex) when (ex is not System.OperationCanceledException)
            {
                Debug.LogError($"[UISnapshot] ProcessSnapshotEachTime Exception: {ex.Message}");
            }
        }



    }

}
