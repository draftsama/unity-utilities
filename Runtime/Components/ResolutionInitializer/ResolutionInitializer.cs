using System.Threading.Tasks;
using System;
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
            UnityEngine.Debug.LogFormat("Set Resolution: \nX:{0}\nY:{1}\nWidth:{2}\nHeight:{3}", _x, y, width, height);
            SetRefeshRate(refreshRate);
            if (_displayMode == DisplayModes.Fullscreen)
            {
                Screen.SetResolution(width, height, true);
                return;
            }
            var fromFullscreen = Screen.fullScreen;
            if (fromFullscreen)
            {
                Screen.SetResolution(width, height, false);
            }
            Task.Run(async () =>
           {

               if (fromFullscreen) await Task.Delay(50);
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
               _windowsHandler.TrySetDisplayMode(_displayMode, _x, y, width, height);
#endif
           });

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
                        var popupwindow = WS_OVERLAPPED | WS_CAPTION | WS_THICKFRAME;
                        // FIRST PASS: positions the client window correctly
                        Flags.Unset<int>(ref flags, popupwindow);
                        SetWindowLongPtr(Window, GWL_STYLE, flags);
                        UpdateWindowRect(Window, x, y, resolutionWidth, resolutionHeight);

                        // SECOND PASS: ensures that the window has the correct styling
                        Flags.Unset<int>(ref flags, popupwindow);
                        SetWindowLongPtr(Window, GWL_STYLE, flags);
                        //UpdateWindowStyle(Window);                    // for some reason, UpdateWindowStyle does not update the window 
                        // properly here, and instead resets of the window styles. For 
                        // some other reason, a secondary call to SetWindowLongPtr does 
                        // in fact update the window properly. It is not clear why this 
                        // is, only that it seems to work, for now, in our test environment.                    
                        SetWindowLongPtr(Window, GWL_STYLE, flags);

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

