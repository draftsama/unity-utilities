#if ENABLE_UIPAGE_OLD

using System.Linq;
using UnityEditor;
using UnityEngine;
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


            if (currentDefault != null && currentDefault != script && GUILayout.Button("Select Default Page"))
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


            //open page
            GUI.color = script.IsOpened ? Color.green : Color.white;
            string openButtonText = script.IsOpened ? "Page is Opened" : "Open Page";

            if (GUILayout.Button(openButtonText))
            {
                if (Application.isPlaying)
                {

                    script.OpenPage();
                }
                else
                {
                    var allPages = FindObjectsByType<UIPage>(FindObjectsSortMode.None)
                        .Where(_ => _.GroupName == script.GroupName && _ != script);

                    foreach (var p in allPages)
                    {
                        p.SetShow(false);
                    }

                    script.SetShow(true);
                }
            }
            GUI.color = Color.white;
            // if (GUILayout.Button("Hide"))
            // {
            //     script.SetShow(false);
            // }

            GUI.enabled = false;
            EditorGUILayout.PropertyField(isTransitionPage);
            EditorGUILayout.PropertyField(isOpened);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(transitionInfo);


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