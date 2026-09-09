using UnityEngine;
using PetGame.UI;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Manages intelligent click-through behavior based on pixel alpha at mouse position.
    /// Transparent areas allow clicks to pass through to the desktop/other apps,
    /// while opaque areas receive normal mouse input.
    /// Additionally supports a game area collider: if the mouse is over a collider
    /// on the designated GameArea layer, clicks are always captured by the game.
    /// </summary>
    public class ClickThroughManager : MonoBehaviour
    {
        [Header("Click-Through Settings")]
        [Tooltip("Alpha threshold (0-255). Pixels with alpha <= this value will be click-through.")]
        [Range(0, 255)]
        [SerializeField] private int alphaThreshold = 10;

        [Tooltip("How often to check pixel alpha (in frames). Higher = better performance, lower = more responsive.")]
        [Range(1, 5)]
        [SerializeField] private int checkInterval = 2;

        [Header("Game Area Settings")]
        [Tooltip("Enable game area collider-based click capture. When the mouse is over a collider on this layer, clicks are never passed through.")]
        [SerializeField] private bool useGameAreaCollider = true;

        [Tooltip("Layer mask for the game area colliders. Assign the 'GameArea' layer here.")]
        [SerializeField] private LayerMask gameAreaLayer;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        private Camera _mainCamera;
        private Texture2D _pixelTexture;
        private RenderTexture _renderTexture;
        private int _frameCounter = 0;
        private bool _lastClickThroughState = false;

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[ClickThroughManager] Main Camera not found! Click-through detection disabled.");
                enabled = false;
                return;
            }

            // Auto-configure gameAreaLayer if not set (e.g., when added via code at runtime)
            if (gameAreaLayer.value == 0 && useGameAreaCollider)
            {
                int layer = LayerMask.NameToLayer("GameArea");
                if (layer >= 0)
                {
                    gameAreaLayer = 1 << layer;
                    Debug.Log($"[ClickThroughManager] Auto-configured gameAreaLayer to 'GameArea' (Layer {layer}).");
                }
                else
                {
                    Debug.LogWarning("[ClickThroughManager] 'GameArea' layer not found. Game area collider detection disabled.");
                    useGameAreaCollider = false;
                }
            }

            // Create a 1x1 texture for reading single pixel
            _pixelTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Create a small render texture that mirrors the camera output
            _renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }

        private void Update()
        {
            _frameCounter++;
            if (_frameCounter < checkInterval) return;
            _frameCounter = 0;

            if (TransparentWindowManager.Instance == null) return;

            // Get mouse position in screen coordinates
            Vector3 mousePos = Input.mousePosition;

            // Clamp to screen bounds
            int x = Mathf.Clamp((int)mousePos.x, 0, Screen.width - 1);
            int y = Mathf.Clamp((int)mousePos.y, 0, Screen.height - 1);

            // Check explicit interactive areas before falling back to rendered pixel alpha.
            bool shouldClickThrough;
            if (useGameAreaCollider && IsMouseOverGameArea(mousePos))
            {
                // Mouse is over game area collider — never click through
                shouldClickThrough = false;
            }
            else if (CircleClickArea.ContainsAnyActiveArea(mousePos))
            {
                // Mouse is over circular UI such as MainView/BlackHole — never click through.
                shouldClickThrough = false;
            }
            else
            {
                // Fall back to pixel alpha check
                shouldClickThrough = CheckPixelAlpha(x, y);
            }

            // Only update window style if state changed (avoid unnecessary API calls)
            if (shouldClickThrough != _lastClickThroughState)
            {
                _lastClickThroughState = shouldClickThrough;
                TransparentWindowManager.Instance.SetClickThrough(shouldClickThrough);
            }
        }

        /// <summary>
        /// Checks whether the mouse position (in screen space) is over a 2D collider
        /// on the GameArea layer using Physics2D.OverlapPoint.
        /// </summary>
        private bool IsMouseOverGameArea(Vector3 screenPos)
        {
            if (_mainCamera == null) return false;

            Vector2 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(worldPos, gameAreaLayer);
            return hit != null;
        }

        /// <summary>
        /// Checks the alpha value of the pixel at the given screen position.
        /// Returns true if the pixel is transparent (should click through).
        /// </summary>
        private bool CheckPixelAlpha(int x, int y)
        {
            // Use ReadPixels from the active render texture or screen
            RenderTexture currentRT = RenderTexture.active;

            // Render camera to our render texture
            if (_mainCamera.targetTexture == null)
            {
                _mainCamera.targetTexture = _renderTexture;
                _mainCamera.Render();
                _mainCamera.targetTexture = null;
            }

            RenderTexture.active = _renderTexture;
            _pixelTexture.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            _pixelTexture.Apply();
            RenderTexture.active = currentRT;

            Color pixel = _pixelTexture.GetPixel(0, 0);
            int alpha = (int)(pixel.a * 255f);

            return alpha <= alphaThreshold;
        }

        private void OnDestroy()
        {
            if (_pixelTexture != null)
                Destroy(_pixelTexture);

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }

        /// <summary>
        /// Handles screen resolution changes by recreating the render texture.
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            RecreateRenderTexture();
        }

        private void RecreateRenderTexture()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            _renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }

#else
        // Editor / non-Windows fallback
        private void Start()
        {
            Debug.Log("[ClickThroughManager] Click-through detection only works in Windows Standalone builds.");
        }
#endif
    }
}
