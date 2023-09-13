using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

public class MenuEditor
{
    [MenuItem("Utilities/Open Resources Floder")]
    public static void OpenResourcesFloder()
    {

        string folderPath = Path.Combine(Environment.CurrentDirectory, "Resources");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
#if UNITY_EDITOR_WIN
        Process.Start("explorer.exe", folderPath);
#elif UNITY_EDITOR_OSX
        Process.Start("open", folderPath);
#endif
    }
}
