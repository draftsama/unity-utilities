#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System;
using System.Collections.Generic;

namespace Modules.Utilities.Editor
{
    public class PackageInfo
    {
        public string name;
        public string version;
        public string path;
        public string gitUrl;
        public string[] defineSymbols;

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
        static bool hasAlreadyAskedInThisSession = false;

        //  "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
        //"com.unity.addressables"
        static List<PackageInfo> packagesToInstall = new List<PackageInfo>()
        {
            // packageName, gitUrl
            new PackageInfo { name = "com.unity.addressables", gitUrl = "" , defineSymbols = new string[] { "PACKAGE_ADDRESSABLES_INSTALLED" } },
            new PackageInfo { name = "com.unity.nuget.newtonsoft-json",gitUrl = "", defineSymbols = new string[] { "PACKAGE_NEWTONSOFT_JSON_INSTALLED" } },
            // new PackageInfo { name = "com.cysharp.unitask", gitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"},
        };


        [MenuItem("Utilities/Install Dependency Packages")]
        static void InstallDependencyPackages()
        {

            Debug.Log("Installing Dependency Packages...");
            currentPackageIndex = 0;
            InstallNextPackage();

        }
        [MenuItem("Utilities/Add All Define Symbols")]
        static void UpdateAllDefineSymbols()
        {
            //get current build target group
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            //get current define symbols
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));

            //add new define symbols
            foreach (var package in packagesToInstall)
            {
                if (package.defineSymbols != null)
                {
                    foreach (var symbol in package.defineSymbols)
                    {
                        if (!defineSymbols.Contains(symbol))
                        {
                            defineSymbols += ";" + symbol;
                        }
                    }
                }
            }

            //set new define symbols
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), defineSymbols);
        }

        static void AddDefineSymbols(string[] defineSymbols)
        {
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            //get current define symbols
            string currentDefineSymbols = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));

            //add new define symbols
            foreach (var symbol in defineSymbols)
            {
                if (!currentDefineSymbols.Contains(symbol))
                {
                    Debug.Log("Adding define symbol: " + symbol);

                    currentDefineSymbols += ";" + symbol;
                }
            }

            //set new define symbols
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), currentDefineSymbols);
        }




        static void InstallNextPackage()
        {
            if (currentPackageIndex >= packagesToInstall.Count)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("All packages installed.");
                UpdateAllDefineSymbols();

                return;
            }

            string packageName = packagesToInstall[currentPackageIndex].GetInstallationString();
            Debug.Log("Installing package: " + packageName);
            request = Client.Add(packageName);
            EditorApplication.update += Process;

            static void Process()
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

                EditorApplication.update -= Process;

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
        static void DependencyRequire()
        {
            // Don't ask again in the same session
            if (hasAlreadyAskedInThisSession) return;

            bool hasAnyMissingPackage = false;
            int checkedPackages = 0;

            for (int i = 0; i < packagesToInstall.Count; i++)
            {
                string packageName = packagesToInstall[i].name;
                string[] defineSymbols = packagesToInstall[i].defineSymbols;

                IsPackageInstalled(packagesToInstall[i], (isInstalled, pkgInfo) =>
                {
                    checkedPackages++;

                    if (!isInstalled)
                    {
                        hasAnyMissingPackage = true;
                    }
                    else
                    {
                        // Debug.Log($"Package {pkgInfo.name} is already installed.");
                        // Add define symbols 
                        if (pkgInfo.defineSymbols != null && pkgInfo.defineSymbols.Length > 0)
                            AddDefineSymbols(pkgInfo.defineSymbols);
                    }

                    // Check if all packages have been checked
                    if (checkedPackages >= packagesToInstall.Count)
                    {
                        if (hasAnyMissingPackage)
                        {
                            hasAlreadyAskedInThisSession = true; // Mark as asked
                            if (EditorUtility.DisplayDialog("Unity Utilities", "Some dependency packages are missing. Do you want to install them?", "Install", "Cancel"))
                            {
                                InstallDependencyPackages();
                            }
                        }
                    }
                });
            }

            // Debug.Log("DependencyRequire");
        }

        private static ListRequest listRequest;
        private static Action<bool, PackageInfo> onCheckComplete;

        /// <summary>
        /// Checks if a specific package is installed.
        /// </summary>
        /// <param name="packageName">e.g. "com.unity.textmeshpro"</param>
        /// <param name="callback">Callback returns true if installed, false otherwise</param>
        public static void IsPackageInstalled(PackageInfo packageInfo, Action<bool, PackageInfo> callback)
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
                        if (package.name == packageInfo.name)
                        {
                            found = true;
                            break;
                        }
                    }
                    onCheckComplete?.Invoke(found, packageInfo);
                }
                else
                {
                    Debug.LogError("Failed to list packages: " + listRequest.Error.message);
                    onCheckComplete?.Invoke(false, packageInfo);
                }
            }
        }
    }



}
#endif