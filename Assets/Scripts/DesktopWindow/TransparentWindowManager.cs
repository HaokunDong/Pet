using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Core manager for transparent desktop window functionality.
    /// Handles: borderless window, per-pixel transparency via DWM, always-on-top.
    /// Only works in Windows Standalone builds.
    /// </summary>
    public class TransparentWindowManager : MonoBehaviour
    {
        #region Singleton

        private static TransparentWindowManager _instance;
        public static TransparentWindowManager Instance => _instance;

        #endregion

        [Header("Configuration")]
        [Tooltip("Optional WindowConfig asset. If not assigned, uses default settings.")]
        [SerializeField] private WindowConfig _config;

        /// <summary>
        /// The active window configuration.
        /// </summary>
        public WindowConfig Config => _config;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        #region Windows API Imports

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("Dwmapi.dll")]
        private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        #endregion

        #region Constants

        // Window style indices
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        // Window styles
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_BORDER = 0x00800000;
        private const uint WS_DLGFRAME = 0x00400000;
        private const uint WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_MAXIMIZEBOX = 0x00010000;

        // Extended window styles
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOPMOST = 0x00000008;

        // SetWindowPos flags
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        // SetWindowPos insert-after values
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        // System metrics
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// The native window handle.
        /// </summary>
        public IntPtr WindowHandle => _hWnd;

        /// <summary>
        /// Whether the window is currently set to always-on-top.
        /// </summary>
        public bool IsTopMost => _isTopMost;

        /// <summary>
        /// Whether click-through (WS_EX_TRANSPARENT) is currently enabled.
        /// </summary>
        public bool IsClickThrough => _isClickThrough;

        #endregion

        private IntPtr _hWnd;
        private bool _isTopMost = true;
        private bool _isClickThrough = false;

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Get the window handle
            _hWnd = GetActiveWindow();

            // Apply borderless + transparent window
            ApplyBorderlessStyle();
            ApplyTransparency();
            SetTopMost(_config != null ? _config.alwaysOnTop : true);
            SetupTransparentCamera();
            ApplyWindowSize();
            ValidateRuntimeSettings();

            Debug.Log("[TransparentWindowManager] Window initialized: borderless, transparent, topmost.");
        }

        /// <summary>
        /// Checks runtime settings and logs warnings if configuration may cause issues.
        /// </summary>
        private void ValidateRuntimeSettings()
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Debug.LogWarning("[TransparentWindowManager] Screen.fullScreenMode is not Windowed! " +
                    $"Current: {Screen.fullScreenMode}. Transparent window may not work correctly. " +
                    "Set Player Settings > Fullscreen Mode to 'Windowed'.");
            }
        }

        /// <summary>
        /// Configures the main camera for transparent rendering.
        /// Sets clear flags to SolidColor with fully transparent background.
        /// </summary>
        private void SetupTransparentCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                Debug.LogWarning("[TransparentWindowManager] Main Camera not found! " +
                    "Ensure your camera has the 'MainCamera' tag for transparent background to work.");
            }
        }

        /// <summary>
        /// Removes window border, title bar, and system menu to create a borderless window.
        /// </summary>
        private void ApplyBorderlessStyle()
        {
            // Get current style and remove border/caption/sysmenu/thickframe
            uint style = GetWindowLong(_hWnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            style |= WS_POPUP | WS_VISIBLE;
            SetWindowLong(_hWnd, GWL_STYLE, style);

            // Add layered extended style for transparency support
            uint exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            SetWindowLong(_hWnd, GWL_EXSTYLE, exStyle);

            // Force window to redraw with new style
            SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Enables per-pixel alpha transparency using DWM (Desktop Window Manager).
        /// </summary>
        private void ApplyTransparency()
        {
            // Extend frame into entire client area (-1 means full window)
            MARGINS margins = new MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            DwmExtendFrameIntoClientArea(_hWnd, ref margins);
        }

        /// <summary>
        /// Sets or removes the always-on-top (TOPMOST) flag.
        /// </summary>
        /// <param name="enabled">True to make window always on top, false to restore normal z-order.</param>
        public void SetTopMost(bool enabled)
        {
            _isTopMost = enabled;
            IntPtr insertAfter = enabled ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(_hWnd, insertAfter, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Enables or disables click-through (WS_EX_TRANSPARENT).
        /// When enabled, all mouse events pass through the window.
        /// </summary>
        /// <param name="enabled">True to enable click-through, false to receive clicks.</param>
        public void SetClickThrough(bool enabled)
        {
            _isClickThrough = enabled;
            uint exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);

            if (enabled)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;

            SetWindowLong(_hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Moves the window to the specified screen position.
        /// </summary>
        public void MoveWindow(int x, int y)
        {
            RECT rect;
            GetWindowRect(_hWnd, out rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            SetWindowPos(_hWnd, IntPtr.Zero, x, y, width, height, SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Resizes and repositions the window.
        /// </summary>
        public void SetWindowRect(int x, int y, int width, int height)
        {
            IntPtr insertAfter = _isTopMost ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(_hWnd, insertAfter, x, y, width, height, SWP_SHOWWINDOW | SWP_FRAMECHANGED);
        }

        /// <summary>
        /// Gets the current window rectangle in screen coordinates.
        /// </summary>
        public RECT GetCurrentWindowRect()
        {
            RECT rect;
            GetWindowRect(_hWnd, out rect);
            return rect;
        }

        /// <summary>
        /// Gets the primary screen resolution.
        /// </summary>
        public Vector2Int GetScreenSize()
        {
            return new Vector2Int(
                GetSystemMetrics(SM_CXSCREEN),
                GetSystemMetrics(SM_CYSCREEN));
        }

        /// <summary>
        /// Applies window size based on WindowConfig settings.
        /// Full screen transparent mode covers the entire screen;
        /// fixed mode uses the configured width/height.
        /// </summary>
        private void ApplyWindowSize()
        {
            if (_config == null || _config.fullScreenTransparent)
            {
                // Full screen transparent mode: cover entire screen
                int screenW = GetSystemMetrics(SM_CXSCREEN);
                int screenH = GetSystemMetrics(SM_CYSCREEN);
                SetWindowRect(0, 0, screenW, screenH);
            }
            else
            {
                // Fixed window size mode
                int x = _config.initialPositionX;
                int y = _config.initialPositionY;

                // Center on screen if position is -1
                if (x < 0)
                    x = (GetSystemMetrics(SM_CXSCREEN) - _config.windowWidth) / 2;
                if (y < 0)
                    y = (GetSystemMetrics(SM_CYSCREEN) - _config.windowHeight) / 2;

                SetWindowRect(x, y, _config.windowWidth, _config.windowHeight);
            }
        }

#else
        // Editor / non-Windows fallback
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[TransparentWindowManager] Running in Editor or non-Windows platform. " +
                      "Transparent window features are disabled. Build for Windows Standalone to test.");
        }

        public void SetTopMost(bool enabled) { }
        public void SetClickThrough(bool enabled) { }
        public void MoveWindow(int x, int y) { }
        public void SetWindowRect(int x, int y, int width, int height) { }
#endif
    }
}
