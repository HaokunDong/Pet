using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// ScriptableObject that stores window configuration for the desktop transparent window system.
    /// Create via: Assets > Create > PetGame > Window Config
    /// </summary>
    [CreateAssetMenu(fileName = "WindowConfig", menuName = "PetGame/Window Config")]
    public class WindowConfig : ScriptableObject
    {
        [Header("Window Mode")]
        [Tooltip("If true, the window covers the entire screen (transparent background). " +
                 "If false, uses a fixed-size window.")]
        public bool fullScreenTransparent = true;

        [Header("Fixed Window Size (only used when fullScreenTransparent = false)")]
        [Tooltip("Window width in pixels.")]
        public int windowWidth = 800;

        [Tooltip("Window height in pixels.")]
        public int windowHeight = 600;

        [Tooltip("Initial X position of the window on screen. -1 = center.")]
        public int initialPositionX = -1;

        [Tooltip("Initial Y position of the window on screen. -1 = center.")]
        public int initialPositionY = -1;

        [Header("Behavior")]
        [Tooltip("Whether the window should always stay on top.")]
        public bool alwaysOnTop = true;

        [Tooltip("Whether to enable intelligent click-through (transparent areas pass clicks).")]
        public bool enableClickThrough = true;

        [Tooltip("Alpha threshold for click-through detection (0-255). " +
                 "Pixels with alpha <= this value will be click-through.")]
        [Range(0, 255)]
        public int clickThroughAlphaThreshold = 10;

        [Tooltip("How often to check pixel alpha for click-through (in frames).")]
        [Range(1, 5)]
        public int clickThroughCheckInterval = 2;

        [Header("Drag Settings")]
        [Tooltip("Whether window dragging is enabled.")]
        public bool enableDrag = true;

        [Tooltip("Key modifier required to drag the window.")]
        public KeyCode dragModifierKey = KeyCode.LeftAlt;

        [Tooltip("Clamp window position to screen bounds when dragging.")]
        public bool clampToScreen = true;
    }
}
