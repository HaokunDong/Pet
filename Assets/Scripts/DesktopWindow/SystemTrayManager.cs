using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Manages a Windows system tray icon with a right-click context menu.
    /// Provides options like: Toggle TopMost, Exit application.
    /// Uses Windows Shell_NotifyIcon API directly.
    /// </summary>
    public class SystemTrayManager : MonoBehaviour
    {
        [Header("Tray Settings")]
        [Tooltip("Tooltip text shown when hovering over the tray icon.")]
        [SerializeField] private string trayTooltip = "Desktop Pet";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        #region Windows API Imports

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName,
            string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        #region Constants

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint WM_USER = 0x0400;
        private const uint WM_TRAYICON = WM_USER + 1;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_COMMAND = 0x0111;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint MF_CHECKED = 0x00000008;
        private const uint MF_UNCHECKED = 0x00000000;

        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_NONOTIFY = 0x0080;

        // Menu item IDs
        private const uint MENU_TOPMOST = 1001;
        private const uint MENU_EXIT = 1002;

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        private NOTIFYICONDATA _notifyData;
        private bool _trayCreated = false;
        private bool _requestExit = false;
        private bool _requestToggleTopMost = false;

        private void Start()
        {
            CreateTrayIcon();
        }

        private void Update()
        {
            // Handle menu actions on main thread
            if (_requestExit)
            {
                _requestExit = false;
                Application.Quit();
            }

            if (_requestToggleTopMost)
            {
                _requestToggleTopMost = false;
                if (TransparentWindowManager.Instance != null)
                {
                    TransparentWindowManager.Instance.SetTopMost(!TransparentWindowManager.Instance.IsTopMost);
                }
            }

            // Poll for tray icon right-click (simplified approach using Input)
            // In a full implementation, you'd use a message-only window with WndProc
            // For simplicity, we use a polling approach with right-click detection
        }

        private void CreateTrayIcon()
        {
            IntPtr hWnd = GetActiveWindow();

            _notifyData = new NOTIFYICONDATA();
            _notifyData.cbSize = Marshal.SizeOf(_notifyData);
            _notifyData.hWnd = hWnd;
            _notifyData.uID = 1;
            _notifyData.uFlags = NIF_TIP | NIF_MESSAGE;
            _notifyData.uCallbackMessage = WM_TRAYICON;
            _notifyData.szTip = trayTooltip;
            // Note: hIcon would need a valid icon handle. 
            // In production, load from Resources or embed as native resource.
            _notifyData.hIcon = IntPtr.Zero;
            _notifyData.uFlags |= NIF_ICON;

            _trayCreated = Shell_NotifyIcon(NIM_ADD, ref _notifyData);

            if (!_trayCreated)
            {
                Debug.LogWarning("[SystemTrayManager] Failed to create system tray icon. " +
                    "This may be due to missing icon resource.");
            }
            else
            {
                Debug.Log("[SystemTrayManager] System tray icon created.");
            }
        }

        /// <summary>
        /// Shows the right-click context menu at the current cursor position.
        /// Call this when detecting a right-click on the tray icon.
        /// </summary>
        public void ShowContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            // Add menu items
            bool isTopMost = TransparentWindowManager.Instance != null &&
                             TransparentWindowManager.Instance.IsTopMost;
            uint topMostFlags = MF_STRING | (isTopMost ? MF_CHECKED : MF_UNCHECKED);
            AppendMenu(hMenu, topMostFlags, MENU_TOPMOST, "Always On Top");
            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, MENU_EXIT, "Exit");

            // Get cursor position and show menu
            POINT cursorPos;
            GetCursorPos(out cursorPos);

            IntPtr hWnd = GetActiveWindow();
            SetForegroundWindow(hWnd);

            uint cmd = (uint)TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_NONOTIFY,
                cursorPos.X, cursorPos.Y, 0, hWnd, IntPtr.Zero);

            DestroyMenu(hMenu);

            // Handle menu selection
            switch (cmd)
            {
                case MENU_TOPMOST:
                    _requestToggleTopMost = true;
                    break;
                case MENU_EXIT:
                    _requestExit = true;
                    break;
            }
        }

        private void RemoveTrayIcon()
        {
            if (_trayCreated)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _notifyData);
                _trayCreated = false;
                Debug.Log("[SystemTrayManager] System tray icon removed.");
            }
        }

        private void OnApplicationQuit()
        {
            RemoveTrayIcon();
        }

        private void OnDestroy()
        {
            RemoveTrayIcon();
        }

#else
        // Editor / non-Windows fallback
        private void Start()
        {
            Debug.Log("[SystemTrayManager] System tray only works in Windows Standalone builds.");
        }

        public void ShowContextMenu() { }
#endif
    }
}
