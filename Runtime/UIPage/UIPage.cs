using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Modules.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Modules.Utilities
{

    [RequireComponent(typeof(CanvasGroup))]
    public class UIPage : MonoBehaviour
    {
        private static readonly Dictionary<string, List<UIPage>> s_PageRegistry = new();

#if UNITY_EDITOR
        static UIPage()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                    s_PageRegistry.Clear();
            };
        }
#endif

        [SerializeField][HideInInspector] private string m_GroupName = "Default";
        public string GroupName => m_GroupName;
        [SerializeField][HideInInspector] private bool m_IsDefault;
        public bool IsDefault => m_IsDefault;
        [SerializeField][HideInInspector] private bool m_IsOpened;
        public bool IsOpened => m_IsOpened;
        [SerializeField][HideInInspector] private bool m_IsTransitionPage;
        public bool IsTransitionPage => m_IsTransitionPage;
        [SerializeField][HideInInspector] public TransitionInfo m_TransitionInfo;


        public CanvasGroup m_CanvasGroup { get; private set; }

        public RectTransform m_RectTransform { get; private set; }


        /// <summary>
        /// Make sure to call base.Awake() in derived classes
        /// </summary>
        protected virtual void Awake()
        {
            m_CanvasGroup = GetComponent<CanvasGroup>();
            m_RectTransform = GetComponent<RectTransform>();

            // Register into group
            if (!s_PageRegistry.TryGetValue(m_GroupName, out var list))
            {
                list = new List<UIPage>();
                s_PageRegistry[m_GroupName] = list;
            }
            if (!list.Contains(this))
                list.Add(this);

            // Show only if default AND no other page is already open in this group
            var hasOpened = list.Any(_ => _ != this && _.m_IsOpened);
            var shouldShow = m_IsDefault && !hasOpened;

            SetShow(shouldShow);
            if (shouldShow)
                foreach (var pe in GetComponents<IPageShowEnd>())
                    pe.OnEndShowPage();
        }

        protected virtual void OnDestroy()
        {
            if (!s_PageRegistry.TryGetValue(m_GroupName, out var list)) return;
            list.Remove(this);

            // If this page was open, try to show the default page instead
            if (m_IsOpened)
            {
                var defaultPage = list.FirstOrDefault(_ => _ != null && _.m_IsDefault);
                defaultPage?.SetShow(true);
            }
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
            await TransitionPageAsync(this, _overrideTransitionInfo, _token);

        }


        public void OpenPage(TransitionInfo _overrideTransitionInfo = null, CancellationToken _token = default)
        {
            if (m_IsOpened) return;
            if (_token == default)
                _token = this.GetCancellationTokenOnDestroy();
            TransitionPageAsync(this, _overrideTransitionInfo, _token).Forget();

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

        public void SetDefault(bool _value = true)
        {
            if (m_IsDefault == _value) return;
            m_IsDefault = _value;

            if (_value)
            {
                foreach (var p in GetPages(m_GroupName))
                    if (p != this) p.SetDefault(false);
            }
        }

        public void SetGroupName(string _groupName)
        {
            if (m_GroupName == _groupName) return;

            // Unregister from old group
            if (s_PageRegistry.TryGetValue(m_GroupName, out var oldList))
                oldList.Remove(this);

            m_GroupName = _groupName;

            // Register into new group
            if (!s_PageRegistry.TryGetValue(m_GroupName, out var newList))
            {
                newList = new List<UIPage>();
                s_PageRegistry[m_GroupName] = newList;
            }
            if (!newList.Contains(this))
                newList.Add(this);
        }


        #region Static Methods

        public static T GetPage<T>(string _groupName = "Default") where T : UIPage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var pages = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
                return pages.FirstOrDefault(_ => _.m_GroupName == _groupName);
            }
#endif
            if (!s_PageRegistry.TryGetValue(_groupName, out var list)) return null;
            return list.OfType<T>().FirstOrDefault();
        }


        public static UIPage GetPageByName(string _name, string _groupName = "Default")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var pages = Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None);
                return pages.FirstOrDefault(_ => _.m_GroupName == _groupName && _.name == _name);
            }
#endif
            if (!s_PageRegistry.TryGetValue(_groupName, out var list)) return null;
            return list.FirstOrDefault(_ => _ != null && _.name == _name);

        }
        



        public static UIPage GetCurrentPage(string _groupName = "Default")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                    .FirstOrDefault(_ => _.m_GroupName == _groupName && _.m_IsOpened);
#endif
            if (!s_PageRegistry.TryGetValue(_groupName, out var list)) return null;
            return list.FirstOrDefault(_ => _ != null && _.m_IsOpened);
        }

        public static UIPage[] GetPages(string _groupName = "Default")
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                    .Where(_ => _.m_GroupName == _groupName).ToArray();
#endif
            if (!s_PageRegistry.TryGetValue(_groupName, out var list)) return Array.Empty<UIPage>();
            return list.Where(_ => _ != null).ToArray();
        }

        public static bool ResetUIPagesWithoutNotify(string _groupName)
        {
            var uiPages = GetPages(_groupName);

            foreach (var p in uiPages)
            {
                p.SetShow(p.m_IsDefault);

            }

            return true;
        }

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

        #endregion


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

            var groupName = serializedObject.FindProperty("m_GroupName");
            var isDefault = serializedObject.FindProperty("m_IsDefault");
            var transitionInfo = serializedObject.FindProperty(nameof(script.m_TransitionInfo));
            var isTransitionPage = serializedObject.FindProperty("m_IsTransitionPage");
            var isOpened = serializedObject.FindProperty("m_IsOpened");



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
                    var savedGroupName = script.GroupName;
                    var savedIsDefault = script.IsDefault;
                    var savedTransitionInfo = script.m_TransitionInfo;

                    // Remove old component and add new one
                    var index = gameObject.GetComponents<Component>().ToList().IndexOf(script);
                    DestroyImmediate(script, true);
                    var newComponent = gameObject.AddComponent(scriptType) as UIPage;

                    // Restore values
                    if (newComponent != null)
                    {
                        newComponent.SetGroupName(savedGroupName);
                        if (savedIsDefault) newComponent.SetDefault();
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

            var uiPagesByGroup = UIPage.GetPages(script.GroupName);

            var groupCount = uiPagesByGroup.Count();

            var currentDefault = uiPagesByGroup.FirstOrDefault(_ => _.IsDefault);
            EditorGUILayout.LabelField("UI Pages in Group : " + groupCount);


            EditorGUILayout.BeginHorizontal();
            if (!isDefault.boolValue && currentDefault != script && GUILayout.Button("Set As Default Page"))
            {
                isDefault.boolValue = true;

                var allPages = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                                   .Where(_ => _.GroupName == script.GroupName && _ != script);

                foreach (var p in allPages)
                {
                    p.SetShow(false);
                }

                script.SetShow(true);
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
                    .Where(_ => _.GroupName == script.GroupName && _ != script);

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






            //
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_GroupName", "m_IsDefault", "m_TransitionInfo", "m_IsTransitionPage", "m_IsOpened");
            EditorGUILayout.EndVertical();

            // Draw buttons from ButtonAttribute
            CustomAttributeDrawer.DrawButtonMethods(target);

            serializedObject.ApplyModifiedProperties();


            if (GUI.changed)
            {
                if (isDefault.boolValue)
                {
                    var allPages = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                        .Where(_ => _.GroupName == script.GroupName && _ != script);

            

                    foreach (var p in allPages)
                    {
                        p.SetDefault(false);
                        EditorUtility.SetDirty(p);
                    }
                }

                //set dirty
                EditorUtility.SetDirty(script);
            }




        }

    }

    [UnityEditor.InitializeOnLoad]
    public static class UIPageHierarchyIndicator
    {
        private static Texture2D s_CircleTexture;
        private static double s_NextRepaintTime;
        private const double k_RepaintInterval = 0.1;

        static UIPageHierarchyIndicator()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup < s_NextRepaintTime) return;
            s_NextRepaintTime = EditorApplication.timeSinceStartup + k_RepaintInterval;
            EditorApplication.RepaintHierarchyWindow();
        }

        private static Texture2D GetCircleTexture()
        {
            if (s_CircleTexture != null) return s_CircleTexture;

            const int size = 32;
            s_CircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            s_CircleTexture.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    s_CircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            s_CircleTexture.Apply();
            return s_CircleTexture;
        }

        private static readonly GUIStyle s_BadgeStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold,
        };

        private static void DrawBadge(Rect rect, Color textColor, string text, string tooltip)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.18f, 0.18f, 0.18f, 0.92f);
            GUI.DrawTexture(rect, GetCircleTexture());
            GUI.color = prev;

            s_BadgeStyle.normal.textColor = textColor;
            GUI.Label(rect, new GUIContent(text, tooltip), s_BadgeStyle);
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
#if UNITY_6000_3_OR_NEWER
            var go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
#else
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
#endif
            if (go == null) return;

            var uiPage = go.GetComponent<UIPage>();
            if (uiPage == null) return;

            const float iconSize = 15f;
            const float padding = 2f;

            float offsetX = selectionRect.xMax - padding;

            if (uiPage.IsTransitionPage)
            {
                offsetX -= iconSize;
                DrawBadge(new Rect(offsetX, selectionRect.y + 1, iconSize, iconSize),
                    new Color(1f, 0.6f, 0f), "T", "UIPage: Transitioning");
                offsetX -= padding;
            }
            else if (uiPage.IsOpened)
            {
                offsetX -= iconSize;
                DrawBadge(new Rect(offsetX, selectionRect.y + 1, iconSize, iconSize),
                    new Color(0.3f, 1f, 0.3f), "O", "UIPage: Opened");
                offsetX -= padding;
            }

            if (uiPage.IsDefault)
            {
                offsetX -= iconSize;
                DrawBadge(new Rect(offsetX, selectionRect.y + 1, iconSize, iconSize),
                    new Color(0.4f, 0.7f, 1f), "D", "UIPage: Default");
            }
        }
    }

}

#endif