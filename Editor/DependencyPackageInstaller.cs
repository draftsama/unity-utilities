using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Modules.Utilities.Editor
{
    public class PackageInfo
    {
        public string name;
        public string version;
        public string path;
        public string gitUrl;

        public string GetInstallationString()
        {
            //if gitUrl null or empty, return name
            if (string.IsNullOrEmpty(gitUrl))
            {
                return name;
            }
            else
            {
                return gitUrl;
            }
        }
    }
    public class DependencyPackageInstaller
    {
        static int currentPackageIndex = 0;
        static AddRequest request;

        //  "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
        //"com.unity.addressables"
        static List<PackageInfo> packagesToInstall = new List<PackageInfo>()
        {
            // packageName, gitUrl
            new PackageInfo { name = "com.unity.addressables", gitUrl = ""},
            new PackageInfo { name = "com.cysharp.unitask", gitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"},
        };


        [MenuItem("Utilities/Install Dependency Packages")]
        static void InstallDependencyPackages()
        {

            Debug.Log("Installing Dependency Packages...");
            currentPackageIndex = 0;
            InstallNextPackage();


        }



        static void InstallNextPackage()
        {
            if (currentPackageIndex >= packagesToInstall.Count)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("All packages installed.");
                return;
            }

            string packageName = packagesToInstall[currentPackageIndex].GetInstallationString();
            Debug.Log("Installing package: " + packageName);
            request = Client.Add(packageName);
            EditorApplication.update += Update;

            static void Update()
            {
                if (!request.IsCompleted)
                {
                    float progress = (float)currentPackageIndex / packagesToInstall.Count;
                    EditorUtility.DisplayProgressBar(
                        "Installing Packages",
                        $"Installing: {packagesToInstall[currentPackageIndex]}",
                        progress
                    );
                    return;
                }

                EditorApplication.update -= Update;

                if (request.Status == StatusCode.Success)
                {
                    Debug.Log($"✅ Installed: {request.Result.packageId}");
                }
                else
                {
                    Debug.LogError($"❌ Failed to install {packagesToInstall[currentPackageIndex]}: {request.Error.message}");
                }

                currentPackageIndex++;
                EditorUtility.ClearProgressBar();

                InstallNextPackage();
            }
        }

        [InitializeOnLoadMethod]
        static void DependencyRequire()
        {


            for (int i = 0; i < packagesToInstall.Count; i++)
            {
                string packageName = packagesToInstall[i].name;
                IsPackageInstalled(packageName, (isInstalled, pkgName) =>
                {
                    if (!isInstalled)
                    {
                        // Debug.Log($"Package {pkgName} is not installed.");
                        if (EditorUtility.DisplayDialog("Unity Utilities", "Require Dependency Packages", "Install", "Cancel"))
                        {
                            InstallDependencyPackages();

                        }
                    }
                    else
                    {
                        // Debug.Log($"Package {pkgName} is already installed.");
                    }

                });

            }
        }

        private static ListRequest listRequest;
        private static Action<bool, string> onCheckComplete;

        /// <summary>
        /// Checks if a specific package is installed.
        /// </summary>
        /// <param name="packageName">e.g. "com.unity.textmeshpro"</param>
        /// <param name="callback">Callback returns true if installed, false otherwise</param>
        public static void IsPackageInstalled(string packageName, Action<bool, string> callback)
        {
            onCheckComplete = callback;
            listRequest = Client.List(true); // true = include dependencies
            EditorApplication.update += CheckProgress;

            void CheckProgress()
            {
                if (!listRequest.IsCompleted)
                    return;

                EditorApplication.update -= CheckProgress;

                if (listRequest.Status == StatusCode.Success)
                {
                    bool found = false;
                    foreach (var package in listRequest.Result)
                    {
                        if (package.name == packageName)
                        {
                            found = true;
                            break;
                        }
                    }
                    onCheckComplete?.Invoke(found, packageName);
                }
                else
                {
                    Debug.LogError("Failed to list packages: " + listRequest.Error.message);
                    onCheckComplete?.Invoke(false, packageName);
                }
            }
        }
    }



}