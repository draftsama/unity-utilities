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
    public class UIPage : MonoBehaviour
    {



        [SerializeField][HideInInspector] public string m_GroupName = "Default";
        [SerializeField][HideInInspector] public bool m_IsDefault;
        [SerializeField][ReadOnlyField] public bool m_IsOpened;
        [SerializeField][ReadOnlyField] public bool m_IsTransitionPage;
        [SerializeField] public TransitionInfo m_TransitionInfo;




        public CanvasGroup m_CanvasGroup { get; private set; }

        public RectTransform m_RectTransform { get; private set; }


        /// <summary>
        /// Make sure to call base.Awake() in derived classes
        /// </summary>
        protected virtual void Awake()
        {
            m_CanvasGroup = GetComponent<CanvasGroup>();
            m_RectTransform = GetComponent<RectTransform>();
            SetShow(m_IsDefault);
            if (m_IsDefault)
                foreach (var pe in GetComponents<IPageShowEnd>())
                    pe.OnEndShowPage();
        }


        public void SetShow(bool _isShow)
        {
            if (!m_CanvasGroup) m_CanvasGroup = GetComponent<CanvasGroup>();

            m_CanvasGroup.SetAlpha(_isShow ? 1 : 0);
            m_IsOpened = _isShow;

        }
        public void OpenPage()
        {
            var token = this.GetCancellationTokenOnDestroy();
            OpenPage(_overrideTransitionInfo: null, _token: token);
        }

        public async UniTask OpenPageAsync(TransitionInfo _overrideTransitionInfo = null, CancellationToken _token = default)
        {
            if (m_IsOpened) return;
            if (_token == default)
                _token = this.GetCancellationTokenOnDestroy();
            await UIPageHelper.TransitionPageAsync(this, _overrideTransitionInfo, _token);

        }



        public void OpenPage(TransitionInfo _overrideTransitionInfo = null, CancellationToken _token = default)
        {
            if (m_IsOpened) return;
            if (_token == default)
                _token = this.GetCancellationTokenOnDestroy();
            UIPageHelper.TransitionPageAsync(this, _overrideTransitionInfo, _token).Forget();

        }


        public async UniTask ShowPageAsync(int _milliseconds, bool _isShow, CancellationToken _token = default)
        {
            var targetAlpha = _isShow ? 1f : 0f;
            if (!m_CanvasGroup) m_CanvasGroup = GetComponent<CanvasGroup>();


            if (_isShow)
                foreach (var pe in GetComponents<IPageShowBegin>())
                    pe.OnBeginShowPage();
            else
                foreach (var pe in GetComponents<IPageHideBegin>())
                    pe.OnBeginHidePage();

            await m_CanvasGroup.LerpAlphaAsync(_milliseconds, targetAlpha, _token: _token);


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




        public static async UniTask TransitionPageAsync(UIPage _target, TransitionInfo _overrideTransition = null, CancellationToken _token = default)
        {
            var current = UIPage.GetCurrentPage(_target.m_GroupName);
            // Debug.Log($"TransitionPageAsync current:{current}  - target:{_target}");

            if (current == null)
            {
                return;
            }

            await TransitionPageAsync(current, _target, _overrideTransition, _token);
        }



        public static async UniTask TransitionPageAsync(UIPage _current, UIPage _target, TransitionInfo _overrideTransitionInfo = null, CancellationToken _token = default)
        {

            var transitionInfo = _overrideTransitionInfo ?? _target.m_TransitionInfo;


            if (_current == null || _target == null || _current == _target || _current.m_IsTransitionPage || _target.m_IsTransitionPage)
                return;

            _current.m_IsTransitionPage = true;
            _target.m_IsTransitionPage = true;
            _current.m_CanvasGroup.blocksRaycasts = false;
            _target.m_CanvasGroup.blocksRaycasts = false;

            //if target sibling index is less than current sibling index, set target sibling index to current sibling index

            if (_target.m_RectTransform.GetSiblingIndex() < _current.m_RectTransform.GetSiblingIndex())
            {
                _target.m_RectTransform.SetSiblingIndex(_current.m_RectTransform.GetSiblingIndex());

            }


            foreach (var pe in _target.GetComponents<IPageShowBegin>())
                pe.OnBeginShowPage();

            foreach (var pe in _current.GetComponents<IPageHideBegin>())
                pe.OnBeginHidePage();

            if (transitionInfo.m_Type == TransitionInfo.TransitionType.Fade)
            {
                var duration = transitionInfo.m_Duration * 0.5f;


                await UITransitionFade.Instance.FadeIn((int)duration, transitionInfo.m_FadeColor, _token);
                // await UniTask.Delay(300, cancellationToken: _token);
                _current.SetShow(false);
                _target.SetShow(true);

                await UITransitionFade.Instance.FadeOut((int)duration, transitionInfo.m_FadeColor, _token);


            }
            else if (transitionInfo.m_Type == TransitionInfo.TransitionType.CrossFade)
            {


                await UniTask.WhenAll(
                    _current.m_CanvasGroup.LerpAlphaAsync(transitionInfo.m_Duration, 0f, _token: _token),
                    _target.m_CanvasGroup.LerpAlphaAsync(transitionInfo.m_Duration, 1f, _token: _token)
                );
                _current.SetShow(false);
                _target.SetShow(true);



            }
            else if (transitionInfo.m_Type == TransitionInfo.TransitionType.Slide)
            {
                var duration = Mathf.FloorToInt(transitionInfo.m_Duration * 0.5f);
                _target.m_RectTransform.anchoredPosition = transitionInfo.m_StartPosition;
                _target.m_CanvasGroup.SetAlpha(1f);
                await UniTask.WhenAll(
                                 _current.m_RectTransform.LerpAnchorPositionAsync(
                                     duration,
                                        transitionInfo.m_EndPosition - transitionInfo.m_StartPosition,
                                        _ease: transitionInfo.m_Ease,
                                     _token: _token
                                 ),
                                _target.m_RectTransform.LerpAnchorPositionAsync(
                                    duration,
                                    transitionInfo.m_EndPosition,
                                    _ease: transitionInfo.m_Ease,
                                    _token: _token
                                )
                             );

                _current.SetShow(false);
                _target.SetShow(true);

            }

            foreach (var pe in _target.GetComponents<IPageShowEnd>())
                pe.OnEndShowPage();

            foreach (var pe in _current.GetComponents<IPageHideEnd>())
                pe.OnEndHidePage();

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
            var transitionInfo = serializedObject.FindProperty(nameof(script.m_TransitionInfo));
            var isTransitionPage = serializedObject.FindProperty(nameof(script.m_IsTransitionPage));
            var isOpened = serializedObject.FindProperty(nameof(script.m_IsOpened));



            //draw script field
            EditorGUILayout.LabelField("Script", EditorStyles.boldLabel);
            var newScript = EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(script), typeof(MonoScript), false) as MonoScript;

            // Check if script was changed and is valid
            if (newScript != null && newScript != MonoScript.FromMonoBehaviour(script))
            {
                var scriptType = newScript.GetClass();
                if (scriptType != null && scriptType.IsSubclassOf(typeof(UIPage)))
                {
                    var gameObject = script.gameObject;

                    // Save current values
                    var savedGroupName = script.m_GroupName;
                    var savedIsDefault = script.m_IsDefault;
                    var savedTransitionInfo = script.m_TransitionInfo;

                    // Remove old component and add new one
                    var index = gameObject.GetComponents<Component>().ToList().IndexOf(script);
                    DestroyImmediate(script, true);
                    var newComponent = gameObject.AddComponent(scriptType) as UIPage;

                    // Restore values
                    if (newComponent != null)
                    {
                        newComponent.m_GroupName = savedGroupName;
                        newComponent.m_IsDefault = savedIsDefault;
                        newComponent.m_TransitionInfo = savedTransitionInfo;

                        // Move component to original position
                        for (int i = 0; i < index; i++)
                        {
                            UnityEditorInternal.ComponentUtility.MoveComponentUp(newComponent);
                        }

                        // Select the new component
                        Selection.activeObject = newComponent;
                        EditorUtility.SetDirty(gameObject);
                    }

                    return;
                }
            }


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

            GUI.enabled = false;
            EditorGUILayout.PropertyField(isTransitionPage);
            EditorGUILayout.PropertyField(isOpened);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(transitionInfo);




            serializedObject.ApplyModifiedProperties();



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




        }
    }

}

#endif