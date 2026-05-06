using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Manages intelligent click-through behavior based on pixel alpha at mouse position.
    /// Transparent areas allow clicks to pass through to the desktop/other apps,
    /// while opaque areas receive normal mouse input.
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

            // Create a 1x1 texture for reading single pixel
            _pixelTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Create a small render texture that mirrors the camera output
            _renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            // Set camera to render to our render texture as well
            // We use OnPostRender to read pixels instead of changing targetTexture
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

            // Read the pixel at mouse position from the screen
            bool shouldClickThrough = CheckPixelAlpha(x, y);

            // Only update window style if state changed (avoid unnecessary API calls)
            if (shouldClickThrough != _lastClickThroughState)
            {
                _lastClickThroughState = shouldClickThrough;
                TransparentWindowManager.Instance.SetClickThrough(shouldClickThrough);
            }
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
