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
            OpenFolder(Environment.CurrentDirectory);
        }
        [MenuItem("Utilities/Open Resources Folder")]
        public static void OpenResourcesFloder()
        {

            string folderPath = Path.Combine(Environment.CurrentDirectory, "Resources");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            OpenFolder(folderPath);
        }



        public static void OpenFolder(string path)
        {

#if UNITY_EDITOR_WIN
        Process.Start("explorer.exe", path);
#elif UNITY_EDITOR_OSX
            Process.Start("open", path);
#endif
        }

    }


}
