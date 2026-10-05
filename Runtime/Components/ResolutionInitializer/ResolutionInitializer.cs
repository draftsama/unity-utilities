using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
// ReSharper disable All
using Modules.Utilities;
using System.IO;
using System.Runtime.InteropServices;


namespace Modules.Utilities
{


    public class ResolutionInitializer : MonoBehaviour
    {


        public enum DisplayModes { Unknown = -1, Fullscreen = 0, Borderless = 1, Windowed = 2 }

        private static ResolutionInitializer _Instance;



        #region Public Properties
        public static ResolutionInitializer Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = FindFirstObjectByType<ResolutionInitializer>();
                    if (_Instance == null)
                    {
                        var go = new GameObject("ResolutionInitializer");
                        _Instance = go.AddComponent<ResolutionInitializer>();

                    }
                }
                return _Instance;
            }
        }

        #endregion


        #region Public Methods




        public void SetResolution(DisplayModes _displayMode, int _x, int _y, int _width, int _height)
        {
            SetResolution(_displayMode, _x, _y, _width, _height, 30);
        }

        public void SetResolution(DisplayModes _displayMode, int _x, int y, int width, int height, int refreshRate)
        {
            SetResolutionAsync(_displayMode, _x, y, width, height, refreshRate,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        public async UniTask SetResolutionAsync(DisplayModes displayMode, int x, int y, int width, int height,
            int refreshRate, CancellationToken token)
        {
            UnityEngine.Debug.LogFormat("Set Resolution: \nX:{0}\nY:{1}\nWidth:{2}\nHeight:{3}", x, y, width, height);
            SetRefeshRate(refreshRate);
            if (displayMode == DisplayModes.Fullscreen)
            {
                Screen.SetResolution(width, height, true);
                return;
            }

            // Let Unity size its own plain window first, even when it already starts windowed (the player
            // restores the last window size). Changing the native style while Unity is still applying a
            // mode change leaves its client-area origin stale, which shows up as mouse input offset by the
            // title-bar height.
            if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != width || Screen.height != height)
            {
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfterSlim(TimeSpan.FromSeconds(2));
                try
                {
                    await UniTask.WaitUntil(() => !Screen.fullScreen &&
                                                  Screen.fullScreenMode == FullScreenMode.Windowed &&
                                                  Screen.width == width && Screen.height == height,
                        cancellationToken: timeout.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Unity clamps a window taller than the desktop; the native resize below still applies it.
                    UnityEngine.Debug.LogWarningFormat(
                        "Set Resolution: Unity window is {0}x{1} ({2}), expected {3}x{4} windowed.",
                        Screen.width, Screen.height, Screen.fullScreenMode, width, height);
                }
            }

            // The mode switch is applied over a few frames even after Screen reports it.
            await UniTask.DelayFrame(3, cancellationToken: token);

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            // Win32 window calls must run on the thread that owns the window (Unity's main thread).
            if (displayMode == DisplayModes.Borderless && IsPopupWindowLaunch())
            {
                // Unity created the window borderless itself, so only move it; restyling would desync
                // Unity's client-area mapping again.
                _windowsHandler.TryMoveWindow(x, y);
            }
            else
            {
                if (displayMode == DisplayModes.Borderless)
                {
                    UnityEngine.Debug.LogWarning("Set Resolution: borderless without -popupwindow strips the " +
                                                 "title bar natively; mouse input may be offset. Launch with -popupwindow.");
                }
                _windowsHandler.TrySetDisplayMode(displayMode, x, y, width, height);
            }
#endif
        }

        private static bool IsPopupWindowLaunch()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "-popupwindow", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }



        #endregion

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

        #region Private Variables
        private WindowHandler _windowsHandler;


        #endregion
#endif

        #region Private Methods

        protected void Awake()
        {

            DontDestroyOnLoad(gameObject);
            _Instance = this;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

        _windowsHandler = new WindowHandler(Application.productName);
#endif

            QualitySettings.vSyncCount = 0;

        }
        private void OnDestroy()
        {
            _Instance = null;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

            _windowsHandler = null;
#endif

        }

        public void SetRefeshRate(int refreshRate)
        {

            Application.targetFrameRate = GetFrameRate(refreshRate);

        }



        #endregion

        private int GetFrameRate(int _targetRefreshRate)
        {

            int _minFrameRate = 30;
            int _averageFrameRate = 60;
            int _maxFrameRate = 120;

            try
            {
                if (_targetRefreshRate < _minFrameRate)
                {
                    return _minFrameRate;
                }
                else if (_targetRefreshRate > _maxFrameRate)
                {
                    return _maxFrameRate;
                }
                return _averageFrameRate;
            }
            catch (Exception)
            {
                return _averageFrameRate;
            }

        }
        #region Classes
        public class WindowHandler
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;        // x position of upper-left corner
                public int Top;         // y position of upper-left corner
                public int Right;       // x position of lower-right corner
                public int Bottom;      // y position of lower-right corner
            }

            public const int WM_NCLBUTTONDOWN = 0xA1;
            public const int HT_CAPTION = 0x2;

            // import methods
            [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
            public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

            [DllImport("user32.dll", EntryPoint = "GetDesktopWindow", SetLastError = true)]
            public static extern IntPtr GetDesktopWindow();

            [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
            public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int uFlags);

            [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

            [DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
            public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

            [DllImportAttribute("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

            [DllImportAttribute("user32.dll")]
            public static extern bool ReleaseCapture();

            // constructor
            public WindowHandler(string title)
            {
                _title = title;
            }

            public Vector2 GetDesktopResolution()
            {
                RECT desktopRect;
                GetWindowRect(Desktop, out desktopRect);

                return new Vector2(desktopRect.Right - desktopRect.Left, desktopRect.Bottom - desktopRect.Top);
            }

            public void OnMouseDown()
            {
                try
                {
                    ReleaseCapture();
                    SendMessage(Window, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
                catch
                {
                }
            }


            public bool TryMoveWindow(int x, int y)
            {
                var window = Window;
                if (window == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarning("Set Resolution: window '" + _title + "' not found.");
                    return false;
                }

                SetWindowPos(window, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                RECT rect;
                GetWindowRect(window, out rect);
                if (rect.Left != x || rect.Top != y)
                {
                    UnityEngine.Debug.LogWarningFormat("Set Resolution: window is at {0},{1}, expected {2},{3}.",
                        rect.Left, rect.Top, x, y);
                }

                return true;
            }

            public bool TrySetDisplayMode(DisplayModes targetDisplayMode, int x, int y, int resolutionWidth, int resolutionHeight)
            {
                // setup
                int flags = (int)GetWindowLongPtr(Window, GWL_STYLE);

                // desktop rect
                RECT desktopRect;
                GetWindowRect(Desktop, out desktopRect);
                int desktopWidth = desktopRect.Right - desktopRect.Left;
                int desktopHeight = desktopRect.Bottom - desktopRect.Top;

                switch (targetDisplayMode)
                {
                    // fullscreen
                    case DisplayModes.Fullscreen:
                        return true;

                    // borderless
                    case DisplayModes.Borderless:
                        var window = Window;
                        if (window == IntPtr.Zero)
                        {
                            UnityEngine.Debug.LogWarning("Set Resolution: window '" + _title + "' not found.");
                            return false;
                        }

                        Flags.Unset<int>(ref flags, WS_CAPTION | WS_THICKFRAME);
                        SetWindowLongPtr(window, GWL_STYLE, flags);
                        // SWP_FRAMECHANGED makes Windows recompute the client area for the new style, so the
                        // window rect equals the client rect and Unity's mouse mapping follows it.
                        UpdateWindowRect(window, x, y, resolutionWidth, resolutionHeight);

                        RECT borderlessClient;
                        GetClientRect(window, out borderlessClient);
                        int clientWidth = borderlessClient.Right - borderlessClient.Left;
                        int clientHeight = borderlessClient.Bottom - borderlessClient.Top;
                        if (clientWidth != resolutionWidth || clientHeight != resolutionHeight)
                        {
                            UnityEngine.Debug.LogWarningFormat(
                                "Set Resolution: borderless client area is {0}x{1}, expected {2}x{3}.",
                                clientWidth, clientHeight, resolutionWidth, resolutionHeight);
                        }

                        return true;

                    // windowed
                    case DisplayModes.Windowed:
                        // FIRST PASS: determine how many pixels are needed to render the window decorations on each side (top, bottom, left, right)

                        var windowed = WS_CAPTION;

                        Flags.Set<int>(ref flags, windowed);
                        SetWindowLongPtr(Window, GWL_STYLE, flags);
                        UpdateWindowStyle(Window);

                        // window and client rects
                        RECT windowRect, clientRect;
                        GetWindowRect(Window, out windowRect);
                        GetClientRect(Window, out clientRect);

                        // calculate decoration size                    
                        int decorationWidth = (windowRect.Right - windowRect.Left) - (clientRect.Right - clientRect.Left);
                        int decorationHeight = (windowRect.Bottom - windowRect.Top) - (clientRect.Bottom - clientRect.Top);

                        // SECOND PASS: position the client window correctly, w.r.t. decorations
                        Flags.Unset<int>(ref flags, windowed);
                        SetWindowLongPtr(Window, GWL_STYLE, flags);
                        UpdateWindowRect(Window, x, y, resolutionWidth + decorationWidth, resolutionHeight + decorationHeight);

                        // THIRD PASS: ensures that the window has the correct styling
                        Flags.Set<int>(ref flags, windowed);
                        SetWindowLongPtr(Window, GWL_STYLE, flags);
                        UpdateWindowStyle(Window);

                        return true;

                    // other
                    case DisplayModes.Unknown:
                    default:
                        return false;
                }
            }

            private IntPtr Window { get { return FindWindowByCaption(IntPtr.Zero, _title); } }
            private IntPtr Desktop { get { return GetDesktopWindow(); } }

            private void UpdateWindowStyle(IntPtr window)
            {
                SetWindowPos(window, 0, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOOWNERZORDER | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }

            private void UpdateWindowRect(IntPtr window, int x, int y, int width, int height)
            {
                SetWindowPos(window, -2, x, y, width, height, SWP_FRAMECHANGED);
            }

            private bool TestForErrors(IntPtr result)
            {
                if (result == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    if (errorCode != 0)
                    {
                        UnityEngine.Debug.LogError("Error " + errorCode.ToString() + " occured. SetDisplayMode failed.");
                        return true;
                    }
                }

                return false;
            }

            // style flags
            private const int
                WS_BORDER = 0x00800000,
                WS_CAPTION = 0x00C00000,
                WS_CHILD = 0x40000000,
                WS_CHILDWINDOW = 0x40000000,
                WS_CLIPCHILDREN = 0x02000000,
                WS_CLIPSIBLINGS = 0x04000000,
                WS_DISABLED = 0x08000000,
                WS_DLGFRAME = 0x00400000,
                WS_GROUP = 0x00020000,
                WS_HSCROLL = 0x00100000,
                WS_ICONIC = 0x20000000,
                WS_MAXIMIZE = 0x01000000,
                WS_MAXIMIZEBOX = 0x00010000,
                WS_MINIMIZE = 0x20000000,
                WS_MINIMIZEBOX = 0x00020000,
                WS_OVERLAPPED = 0x00000000,
                WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
                WS_POPUP = unchecked((int)0x80000000),
                WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
                WS_SIZEBOX = 0x00040000,
                WS_SYSMENU = 0x00080000,
                WS_TABSTOP = 0x00010000,
                WS_THICKFRAME = 0x00040000,
                WS_TILED = 0x00000000,
                WS_TILEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
                WS_VISIBLE = 0x10000000,
                WS_VSCROLL = 0x00200000;

            // extended style flags
            private const int
                WS_EX_DLGMODALFRAME = 0x00000001,
                WS_EX_CLIENTEDGE = 0x00000200,
                WS_EX_STATICEDGE = 0x00020000;

            // position flags
            private const int
                SWP_FRAMECHANGED = 0x0020,
                SWP_NOMOVE = 0x0002,
                SWP_NOSIZE = 0x0001,
                SWP_NOZORDER = 0x0004,
                SWP_NOACTIVATE = 0x0010,
                SWP_NOOWNERZORDER = 0x0200,
                SWP_SHOWWINDOW = 0x0040,
                SWP_NOSENDCHANGING = 0x0400;

            // index for style and extended style flag management
            private const int
                GWL_STYLE = -16,
                GWL_EXSTYLE = -20;

            private string _title;
        }
        #endregion
    }
}

