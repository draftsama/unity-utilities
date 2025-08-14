using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

namespace Modules.Utilities.Editor
{


    public class MenuEditor
    {
        [MenuItem("Utilities/Open Project Folder")]
        public static void OpenProjectFolder()
        {
           EditorUtility.RevealInFinder(Environment.CurrentDirectory);
        }
        [MenuItem("Utilities/Open Persistent Data Folder")]
        public static void OpenPersistentDataFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
     
            // OpenFolder(folderPath);
        }

   
        [MenuItem("Utilities/Value Config/Open Config File")]
        public static void OpenConfigFile()
        {
            var path = Path.Combine(Application.persistentDataPath, "value.config.json");
            UnityEngine.Debug.Log("Open Config File: " + path);
            OpenFolder(path);
        }
      

        public static void OpenFolder(string path)
        {

#if UNITY_EDITOR_WIN
        Process.Start("explorer.exe", $"\"{path}\"");
#elif UNITY_EDITOR_OSX
            Process.Start("open", $"\"{path}\"");
#endif
        }


    }


}
