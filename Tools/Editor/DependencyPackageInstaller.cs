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

    [InitializeOnLoad]
    public class DependencyPackageInstaller
    {
        static int currentPackageIndex = 0;
        static AddRequest request;
        static bool hasAlreadyAskedInThisSession = false;

        static List<PackageInfo> packagesToInstall = new List<PackageInfo>()
        {
            new PackageInfo { name = "com.unity.addressables", gitUrl = "" , defineSymbols = new string[] { "PACKAGE_ADDRESSABLES_INSTALLED" } },
            new PackageInfo { name = "com.unity.nuget.newtonsoft-json", gitUrl = "", defineSymbols = new string[] { "PACKAGE_NEWTONSOFT_JSON_INSTALLED" } },
        };

        // Constructor called on editor load
        static DependencyPackageInstaller()
        {
            // Delay execution to avoid issues during compilation
            EditorApplication.delayCall += DependencyRequire;
        }

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
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // Use reflection to handle API changes between Unity versions
            try
            {
                var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                if (namedBuildTargetType != null)
                {
                    // Unity 2021.2+
                    var fromBuildTargetGroupMethod = namedBuildTargetType.GetMethod("FromBuildTargetGroup");
                    var namedBuildTarget = fromBuildTargetGroupMethod.Invoke(null, new object[] { buildTargetGroup });
                    
                    var getMethod = typeof(PlayerSettings).GetMethod("GetScriptingDefineSymbols", new[] { namedBuildTargetType });
                    string defineSymbols = (string)getMethod.Invoke(null, new[] { namedBuildTarget });

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

                    var setMethod = typeof(PlayerSettings).GetMethod("SetScriptingDefineSymbols", new[] { namedBuildTargetType, typeof(string) });
                    setMethod.Invoke(null, new[] { namedBuildTarget, defineSymbols });
                }
                else
                {
                    // Unity 2020.x and older - fallback
#pragma warning disable CS0618
                    string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                    
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
                    
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defineSymbols);
#pragma warning restore CS0618
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update define symbols: {e.Message}");
            }
        }

        static void AddDefineSymbols(string[] defineSymbols)
        {
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            try
            {
                var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                if (namedBuildTargetType != null)
                {
                    var fromBuildTargetGroupMethod = namedBuildTargetType.GetMethod("FromBuildTargetGroup");
                    var namedBuildTarget = fromBuildTargetGroupMethod.Invoke(null, new object[] { buildTargetGroup });
                    
                    var getMethod = typeof(PlayerSettings).GetMethod("GetScriptingDefineSymbols", new[] { namedBuildTargetType });
                    string currentDefineSymbols = (string)getMethod.Invoke(null, new[] { namedBuildTarget });

                    foreach (var symbol in defineSymbols)
                    {
                        if (!currentDefineSymbols.Contains(symbol))
                        {
                            Debug.Log("Adding define symbol: " + symbol);
                            currentDefineSymbols += ";" + symbol;
                        }
                    }

                    var setMethod = typeof(PlayerSettings).GetMethod("SetScriptingDefineSymbols", new[] { namedBuildTargetType, typeof(string) });
                    setMethod.Invoke(null, new[] { namedBuildTarget, currentDefineSymbols });
                }
                else
                {
#pragma warning disable CS0618
                    string currentDefineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                    
                    foreach (var symbol in defineSymbols)
                    {
                        if (!currentDefineSymbols.Contains(symbol))
                        {
                            Debug.Log("Adding define symbol: " + symbol);
                            currentDefineSymbols += ";" + symbol;
                        }
                    }
                    
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, currentDefineSymbols);
#pragma warning restore CS0618
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to add define symbols: {e.Message}");
            }
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
                        $"Installing: {packagesToInstall[currentPackageIndex].name}",
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
                    Debug.LogError($"❌ Failed to install {packagesToInstall[currentPackageIndex].name}: {request.Error.message}");
                }

                currentPackageIndex++;
                EditorUtility.ClearProgressBar();
                InstallNextPackage();
            }
        }

        static void DependencyRequire()
        {
            if (hasAlreadyAskedInThisSession) return;

            bool hasAnyMissingPackage = false;
            int checkedPackages = 0;

            for (int i = 0; i < packagesToInstall.Count; i++)
            {
                IsPackageInstalled(packagesToInstall[i], (isInstalled, pkgInfo) =>
                {
                    checkedPackages++;

                    if (!isInstalled)
                    {
                        hasAnyMissingPackage = true;
                    }
                    else
                    {
                        if (pkgInfo.defineSymbols != null && pkgInfo.defineSymbols.Length > 0)
                            AddDefineSymbols(pkgInfo.defineSymbols);
                    }

                    if (checkedPackages >= packagesToInstall.Count)
                    {
                        if (hasAnyMissingPackage)
                        {
                            hasAlreadyAskedInThisSession = true;
                            if (EditorUtility.DisplayDialog("Unity Utilities", "Some dependency packages are missing. Do you want to install them?", "Install", "Cancel"))
                            {
                                InstallDependencyPackages();
                            }
                        }
                    }
                });
            }
        }

        private static ListRequest listRequest;
        private static Action<bool, PackageInfo> onCheckComplete;

        public static void IsPackageInstalled(PackageInfo packageInfo, Action<bool, PackageInfo> callback)
        {
            onCheckComplete = callback;
            listRequest = Client.List(true);
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