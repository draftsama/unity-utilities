using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Modules.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Modules.Utilities
{

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPage : MonoBehaviour
    {



        [SerializeField][HideInInspector] public string m_GroupName = "Default";
        [SerializeField][HideInInspector] public bool m_IsDefault;
        [SerializeField][ReadOnlyField] public bool m_IsOpened;
        [SerializeField][ReadOnlyField] public bool m_IsTransitionPage;
        [SerializeField] public TransitionInfo m_TransitionInfo;



        protected CanvasGroup canvasGroup;


        /// <summary>
        /// Make sure to call base.Awake() in derived classes
        /// </summary>
        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            SetShow(m_IsDefault);
            if (m_IsDefault)
                foreach (var pe in GetComponents<IPageShowEnd>())
                    pe.OnEndShowPage();
        }


        public void SetShow(bool _isShow)
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.SetAlpha(_isShow ? 1 : 0);
            m_IsOpened = _isShow;
        }

        public void OpenPage()
        {
            if (m_IsOpened) return;
            var token = this.GetCancellationTokenOnDestroy();
            UIPageHelper.TransitionPageAsync(this, token).Forget();

        }

        public async UniTask ShowPageAsync(int _milliseconds, bool _isShow, CancellationToken _token = default)
        {
            var targetAlpha = _isShow ? 1f : 0f;
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();


            if (_isShow)
                foreach (var pe in GetComponents<IPageShowBegin>())
                    pe.OnBeginShowPage();
            else
                foreach (var pe in GetComponents<IPageHideBegin>())
                    pe.OnBeginHidePage();

            await canvasGroup.LerpAlphaAsync(_milliseconds, targetAlpha, _token: _token);


            if (_isShow)
                foreach (var pe in GetComponents<IPageShowEnd>())
                    pe.OnEndShowPage();
            else
                foreach (var pe in GetComponents<IPageHideEnd>())
                    pe.OnEndHidePage();

            m_IsOpened = _isShow;
        }

        public static T GetPage<T>(string _groupName = "Default") where T : UIPage
        {
            //get page by type
            var pages = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            return pages.FirstOrDefault(_ => _.m_GroupName == _groupName);
        }

        public static UIPage GetCurrentPage(string _groupName = "Default")
        {
            return Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                .FirstOrDefault(_ => _.m_GroupName == _groupName && _.m_IsOpened);
        }

        public static UIPage[] GetPages(string _groupName = "Default")
        {
            return Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                .Where(_ => _.m_GroupName == _groupName).ToArray();
        }


    }


    public static class UIPageHelper
    {




        public static async UniTask TransitionPageAsync(UIPage _target, CancellationToken _token = default)
        {
            var current = UIPage.GetCurrentPage(_target.m_GroupName);
            // Debug.Log($"TransitionPageAsync current:{current}  - target:{_target}");

            if (current == null)
            {
                return;
            }

            await TransitionPageAsync(current, _target, _target.m_TransitionInfo, _token);
        }
        public static async UniTask TransitionPageAsync(UIPage _current, UIPage _target, CancellationToken _token = default)
        {
      
            await TransitionPageAsync(_current, _target, _target.m_TransitionInfo, _token);
        }


        public static async UniTask TransitionPageAsync(UIPage _current, UIPage _target, TransitionInfo _transitionInfo, CancellationToken _token = default)
        {

            if (_current == null || _target == null || _current == _target || _current.m_IsTransitionPage || _target.m_IsTransitionPage)
                return;

            _current.m_IsTransitionPage = true;
            _target.m_IsTransitionPage = true;
            if (_transitionInfo.m_Type == TransitionInfo.TransitionType.Fade)
            {
                var duration = _transitionInfo.m_Duration * 0.5f;


                foreach (var pe in _target.GetComponents<IPageShowBegin>())
                    pe.OnBeginShowPage();

                foreach (var pe in _current.GetComponents<IPageHideBegin>())
                    pe.OnBeginHidePage();

                await UITransitionFade.Instance.FadeIn((int)duration, _transitionInfo.m_FadeColor, _token);
                // await UniTask.Delay(300, cancellationToken: _token);
                _current.SetShow(false);
                _target.SetShow(true);

                await UITransitionFade.Instance.FadeOut((int)duration, _transitionInfo.m_FadeColor, _token);

                foreach (var pe in _target.GetComponents<IPageShowEnd>())
                    pe.OnEndShowPage();

                foreach (var pe in _current.GetComponents<IPageHideEnd>())
                    pe.OnEndHidePage();
            }
            else
            {


                await UniTask.WhenAll(
                    _current.ShowPageAsync(_transitionInfo.m_Duration, false, _token),
                    _target.ShowPageAsync(_transitionInfo.m_Duration, true, _token)
                );
            }

            _current.m_IsTransitionPage = false;
            _target.m_IsTransitionPage = false;
        }

        public static bool ResetUIPagesWithoutNotify(string _groupName)
        {
            var uiPages = UIPage.GetPages(_groupName);

            foreach (var p in uiPages)
            {
                p.SetShow(p.m_IsDefault);

            }

            return true;
        }
    }


    public interface IPageShowBegin
    {
        void OnBeginShowPage();
    }

    public interface IPageShowEnd
    {
        void OnEndShowPage();
    }

    public interface IPageHideBegin
    {
        void OnBeginHidePage();
    }

    public interface IPageHideEnd
    {
        void OnEndHidePage();
    }

}


#if UNITY_EDITOR
namespace Modules.Utilities.Editor
{
    [CustomEditor(typeof(UIPage), true)]
    public class UIPageEditor : UnityEditor.Editor
    {


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var script = (UIPage)target;
            var groupName = serializedObject.FindProperty(nameof(script.m_GroupName));
            var isDefault = serializedObject.FindProperty(nameof(script.m_IsDefault));

            //draw properties
            EditorGUILayout.PropertyField(groupName);

            var uiPagesByGroup = UIPage.GetPages(script.m_GroupName);

            var groupCount = uiPagesByGroup.Count();

            var currentDefault = uiPagesByGroup.FirstOrDefault(_ => _.m_IsDefault);
            EditorGUILayout.LabelField("UI Pages in Group : " + groupCount);


            EditorGUILayout.BeginHorizontal();
            if (!isDefault.boolValue && currentDefault != script && GUILayout.Button("Set As Default Page"))
            {
                isDefault.boolValue = true;
            }


            if (currentDefault != null && currentDefault != script && GUILayout.Button("Go to Default Page"))
            {
                Selection.activeObject = currentDefault;
            }


            if (currentDefault == script)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Is Default Page");
                GUI.color = Color.white;

                if (GUILayout.Button("Clear Default Page"))
                    isDefault.boolValue = false;
            }

            EditorGUILayout.EndHorizontal();


            // EditorGUILayout.PropertyField(isDefault);


            //horizontal layout
            EditorGUILayout.BeginHorizontal();

            //button to show page
            if (GUILayout.Button("Show"))
            {
                script.SetShow(true);
            }

            //button to hide page
            if (GUILayout.Button("Hide"))
            {
                script.SetShow(false);
            }

            //show show only page
            if (GUILayout.Button("Show Only"))
            {
                var allPages = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                    .Where(_ => _.m_GroupName == script.m_GroupName && _ != script);

                foreach (var p in allPages)
                {
                    p.SetShow(false);
                }

                script.SetShow(true);
            }

            EditorGUILayout.EndHorizontal();





            base.DrawDefaultInspector();


            if (GUI.changed)
            {
                if (isDefault.boolValue)
                {
                    var allPages = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                        .Where(_ => _.m_GroupName == script.m_GroupName && _ != script);

                    foreach (var p in allPages)
                    {
                        p.m_IsDefault = false;
                    }
                }

                //set dirty
                EditorUtility.SetDirty(script);
            }

            serializedObject.ApplyModifiedProperties();

        }
    }

}

#endif