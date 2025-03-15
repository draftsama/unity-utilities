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

[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPage : MonoBehaviour
{
    [SerializeField] [HideInInspector] public string m_GroupName = "Default";
    [SerializeField] [HideInInspector] public bool m_IsDefault;
    [SerializeField] [ReadOnlyField] public bool m_IsOpened;
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
    }

    
}


public static class UIPageHelper
{
    public enum TransitionType
    {
        Fade,
        CrossFade,
    }

    public static async UniTask TransitionPageAsync(UIPage _current, UIPage _target, int _milliseconds = 1000,
        TransitionType _transitionType = TransitionType.Fade, CancellationToken _token = default)
    {
        if (_transitionType == TransitionType.Fade)
        {
            var duration = _milliseconds * 0.5f;


            foreach (var pe in _target.GetComponents<IPageShowBegin>())
                pe.OnBeginShowPage();

            foreach (var pe in _current.GetComponents<IPageHideBegin>())
                pe.OnBeginHidePage();

            await UITransitionFade.Instance.FadeIn((int)duration, Color.black, _token);
            // await UniTask.Delay(300, cancellationToken: _token);
            _current.SetShow(false);
            _target.SetShow(true);

            await UITransitionFade.Instance.FadeOut((int)duration, Color.black, _token);

            foreach (var pe in _target.GetComponents<IPageShowEnd>())
                pe.OnEndShowPage();

            foreach (var pe in _current.GetComponents<IPageHideEnd>())
                pe.OnEndHidePage();
        }
        else
        {
            await UniTask.WhenAll(
                _current.ShowPageAsync(_milliseconds, false, _token),
                _target.ShowPageAsync(_milliseconds, true, _token)
            );
        }
    }

    public static bool ResetUIPagesWithoutNotify(string _groupName)
    {
        var uiPages = Object.FindObjectsByType<UIPage>(FindObjectsSortMode.None)
            .Where(_ => _.m_GroupName == _groupName);
    
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


#if UNITY_EDITOR

[CustomEditor(typeof(UIPage), true)]
public class UIPageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var script = (UIPage)target;
        var groupName = serializedObject.FindProperty(nameof(script.m_GroupName));
        var isDefault = serializedObject.FindProperty(nameof(script.m_IsDefault));

        //draw properties
        EditorGUILayout.PropertyField(groupName);

        var uiPagesByGroup = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
            .Where(_ => _.m_GroupName == script.m_GroupName);

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
        base.DrawDefaultInspector();
    }
}

#endif