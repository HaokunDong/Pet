using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Handles window dragging functionality.
    /// Allows the user to drag the game window by holding a modifier key + left mouse button,
    /// or by dragging on a designated UI area.
    /// </summary>
    public class WindowDragHandler : MonoBehaviour
    {
        [Header("Drag Settings")]
        [Tooltip("Key modifier required to drag the window. Hold this key + left mouse button to drag.")]
        [SerializeField] private KeyCode dragModifierKey = KeyCode.LeftAlt;

        [Tooltip("If true, clamp window position to screen bounds.")]
        [SerializeField] private bool clampToScreen = true;

        [Tooltip("Minimum pixels of the window that must remain visible on screen.")]
        [SerializeField] private int minVisiblePixels = 50;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private bool _isDragging = false;
        private POINT _dragStartCursorPos;
        private RECT _dragStartWindowRect;

        private void Update()
        {
            if (TransparentWindowManager.Instance == null) return;

            // Start drag: modifier key held + left mouse button pressed
            if (Input.GetKey(dragModifierKey) && Input.GetMouseButtonDown(0))
            {
                StartDrag();
            }

            // Continue drag
            if (_isDragging)
            {
                if (Input.GetMouseButton(0) && Input.GetKey(dragModifierKey))
                {
                    PerformDrag();
                }
                else
                {
                    EndDrag();
                }
            }
        }

        private void StartDrag()
        {
            _isDragging = true;
            GetCursorPos(out _dragStartCursorPos);
            GetWindowRect(TransparentWindowManager.Instance.WindowHandle, out _dragStartWindowRect);
        }

        private void PerformDrag()
        {
            POINT currentPos;
            GetCursorPos(out currentPos);

            int deltaX = currentPos.X - _dragStartCursorPos.X;
            int deltaY = currentPos.Y - _dragStartCursorPos.Y;

            int newX = _dragStartWindowRect.Left + deltaX;
            int newY = _dragStartWindowRect.Top + deltaY;

            // Clamp to screen bounds if enabled
            if (clampToScreen)
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);
                int windowWidth = _dragStartWindowRect.Right - _dragStartWindowRect.Left;
                int windowHeight = _dragStartWindowRect.Bottom - _dragStartWindowRect.Top;

                // Ensure at least minVisiblePixels of the window remains on screen
                newX = Mathf.Clamp(newX, -windowWidth + minVisiblePixels, screenWidth - minVisiblePixels);
                newY = Mathf.Clamp(newY, -windowHeight + minVisiblePixels, screenHeight - minVisiblePixels);
            }

            SetWindowPos(TransparentWindowManager.Instance.WindowHandle, IntPtr.Zero,
                newX, newY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        private void EndDrag()
        {
            _isDragging = false;
        }

        /// <summary>
        /// Programmatically start a drag operation (can be called from UI buttons).
        /// </summary>
        public void StartDragFromUI()
        {
            StartDrag();
        }

        /// <summary>
        /// Whether the window is currently being dragged.
        /// </summary>
        public bool IsDragging => _isDragging;

#else
        // Editor / non-Windows fallback
        private void Start()
        {
            Debug.Log("[WindowDragHandler] Window dragging only works in Windows Standalone builds. " +
                      $"Drag modifier key is set to: {dragModifierKey}");
        }

        public bool IsDragging => false;
#endif
    }
}
