using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Modules.Utilities.Editor
{
    /// <summary>
    /// Scans loaded scenes for TMP_Text components and replaces their font asset in bulk,
    /// with a per-item opt-out.
    /// </summary>
    public class TMPFontReplacerWindow : EditorWindow
    {
        private class Entry
        {
            public TMP_Text text;
            public string path;
        }

        private static readonly Color SelectedRowColor = new Color(0.24f, 0.49f, 0.9f, 0.35f);

        private TMP_FontAsset targetFont;
        private bool includeInactive = true;
        private bool onlyDifferentFont = true;
        private string filter = "";
        private Vector2 scroll;

        private readonly List<Entry> entries = new List<Entry>();
        // Excluded items are tracked by reference so the choice survives a re-scan.
        private readonly HashSet<TMP_Text> excluded = new HashSet<TMP_Text>();

        [MenuItem("Utilities/TMP Font Replacer")]
        public static void ShowWindow()
        {
            GetWindow<TMPFontReplacerWindow>(false, "TMP Font Replacer", true);
        }

        private void OnEnable()
        {
            Scan();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnHierarchyChange()
        {
            Scan();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawList();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(45)))
                    Scan();

                GUILayout.Space(6);
                if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SetVisibleExcluded(false);
                if (GUILayout.Button("Deselect All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    SetVisibleExcluded(true);

                GUILayout.Space(6);
                EditorGUI.BeginChangeCheck();
                includeInactive = GUILayout.Toggle(includeInactive, "Include inactive", EditorStyles.toolbarButton, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                    Scan();

                onlyDifferentFont = GUILayout.Toggle(onlyDifferentFont, "Only different font", EditorStyles.toolbarButton, GUILayout.Width(115));

                GUILayout.FlexibleSpace();
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            }
        }

        private void DrawList()
        {
            var visible = GetVisible();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox(entries.Count == 0
                    ? "No TMP_Text found in the loaded scenes."
                    : "Nothing matches the current filters.", MessageType.Info);
            }

            foreach (var entry in visible)
            {
                var text = entry.text;
                using (var row = new EditorGUILayout.HorizontalScope())
                {
                    if (Event.current.type == EventType.Repaint && Selection.Contains(text.gameObject))
                        EditorGUI.DrawRect(row.rect, SelectedRowColor);

                    bool included = !excluded.Contains(text);
                    bool newIncluded = EditorGUILayout.Toggle(included, GUILayout.Width(16));
                    if (newIncluded != included)
                    {
                        if (newIncluded) excluded.Remove(text);
                        else excluded.Add(text);
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(text.name);
                        EditorGUILayout.LabelField(entry.path, EditorStyles.miniLabel);
                    }

                    var current = text.font;
                    EditorGUILayout.LabelField(current != null ? current.name : "(none)", GUILayout.Width(160));

                    // Runs after the child controls so the checkbox gets the click first.
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 0 && row.rect.Contains(e.mousePosition))
                    {
                        Selection.activeGameObject = text.gameObject;
                        EditorGUIUtility.PingObject(text.gameObject);
                        e.Use();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            int count = CountToReplace();
            EditorGUILayout.LabelField($"{entries.Count} found, {count} selected to replace", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font Asset", targetFont, typeof(TMP_FontAsset), false);
                using (new EditorGUI.DisabledScope(targetFont == null || count == 0))
                {
                    if (GUILayout.Button($"Replace ({count})", GUILayout.Width(120), GUILayout.Height(20)))
                        Replace();
                }
            }
            EditorGUILayout.Space(4);
        }

        private void Scan()
        {
            entries.Clear();
            var roots = new List<GameObject>();
            var buffer = new List<TMP_Text>();
            bool multiScene = SceneManager.sceneCount > 1;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                roots.Clear();
                scene.GetRootGameObjects(roots);
                foreach (var root in roots)
                {
                    buffer.Clear();
                    root.GetComponentsInChildren(includeInactive, buffer);
                    foreach (var text in buffer)
                    {
                        if (text.hideFlags.HasFlag(HideFlags.HideInHierarchy) || text.hideFlags.HasFlag(HideFlags.NotEditable))
                            continue;
                        entries.Add(new Entry { text = text, path = BuildPath(text.transform, multiScene ? scene.name : null) });
                    }
                }
            }

            excluded.RemoveWhere(t => t == null);
        }

        private static string BuildPath(Transform transform, string scenePrefix)
        {
            var sb = new StringBuilder(transform.name);
            for (var p = transform.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            if (scenePrefix != null)
                sb.Insert(0, scenePrefix + ":");
            return sb.ToString();
        }

        private List<Entry> GetVisible()
        {
            var result = new List<Entry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.text == null) continue;
                if (onlyDifferentFont && targetFont != null && entry.text.font == targetFont) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    entry.path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(entry);
            }
            return result;
        }

        private void SetVisibleExcluded(bool value)
        {
            foreach (var entry in GetVisible())
            {
                if (value) excluded.Add(entry.text);
                else excluded.Remove(entry.text);
            }
        }

        private int CountToReplace()
        {
            int count = 0;
            foreach (var entry in GetVisible())
                if (!excluded.Contains(entry.text)) count++;
            return count;
        }

        private void Replace()
        {
            if (targetFont == null) return;

            var targets = new List<TMP_Text>();
            foreach (var entry in GetVisible())
                if (!excluded.Contains(entry.text)) targets.Add(entry.text);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Replace TMP Font");
            int group = Undo.GetCurrentGroup();

            var scenes = new HashSet<Scene>();
            foreach (var text in targets)
            {
                Undo.RecordObject(text, "Replace TMP Font");
                text.font = targetFont;
                EditorUtility.SetDirty(text);
                scenes.Add(text.gameObject.scene);
            }

            foreach (var scene in scenes)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[TMPFontReplacer] Replaced font on {targets.Count} objects with '{targetFont.name}'.");
            Repaint();
        }
    }
}
