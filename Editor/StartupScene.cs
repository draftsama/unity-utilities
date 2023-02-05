using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StartupScene
{

    [SerializeField]
    static StartupScene()
    {
        EditorApplication.playModeStateChanged -= StateChange;

        EditorApplication.playModeStateChanged += StateChange;
    }

    static void StateChange(PlayModeStateChange _stateChange)
    {
        if (_stateChange == PlayModeStateChange.ExitingEditMode)
        {

            var startup = PlayerPrefs.GetString("STARTUP_SCENE", string.Empty);

            if (!string.IsNullOrEmpty(startup))
            {
                SessionState.SetString("OLD_SCENE", EditorSceneManager.GetActiveScene().path);

                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                EditorSceneManager.OpenScene(startup);
            }

        }
        else if (_stateChange == PlayModeStateChange.EnteredEditMode)
        {

            var oldScene = SessionState.GetString("OLD_SCENE", string.Empty);
            if (!string.IsNullOrEmpty(oldScene))
            {
                EditorSceneManager.OpenScene(oldScene);
            }


        }


    }


    public const string _MENUITEM_SET_STARTUP_PATH = "Assets/Set Startup Scene/Select this Scene as Startup Scene";
    public const string _MENUITEM_CLEAR_STARTUP_PATH = "Assets/Set Startup Scene/Clear";
    [MenuItem(_MENUITEM_SET_STARTUP_PATH, false, 0)]
    private static void SetStartupScene()
    {
        if (Selection.activeObject != null)
        {
            var selected = Selection.activeObject as SceneAsset;

            var path = AssetDatabase.GetAssetPath(selected);
            PlayerPrefs.SetString("STARTUP_SCENE", path);
            Debug.Log($"Set Startup Scene : {path}");
        }

    }
    [MenuItem(_MENUITEM_SET_STARTUP_PATH, true, 0)]

    private static bool SetStartupSceneValidate()
    {

        if (Selection.activeObject != null)
        {
            var selected = Selection.activeObject as SceneAsset;
            if (selected)
            {
                var startupPath = PlayerPrefs.GetString("STARTUP_SCENE", string.Empty);
                var path = AssetDatabase.GetAssetPath(selected);

                return string.IsNullOrEmpty(startupPath) || startupPath != path;
            }
        }

        return false;

    }

    [MenuItem(_MENUITEM_CLEAR_STARTUP_PATH, false, 0)]
    private static void SetClear()
    {
        PlayerPrefs.SetString("STARTUP_SCENE", string.Empty);
        SessionState.SetString("OLD_SCENE", string.Empty);

        Debug.Log($"Clear Startup Scene");

    }
    [MenuItem(_MENUITEM_CLEAR_STARTUP_PATH, true, 0)]
    private static bool SetClearValidate()
    {
        var startupPath = PlayerPrefs.GetString("STARTUP_SCENE", string.Empty);
        return !string.IsNullOrEmpty(startupPath);

    }

}