using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// Controls portal click interaction, drag movement, and animation.
    /// Attach this script to the portal prefab GameObject.
    ///
    /// === Portal Prefab Setup ===
    /// The portal prefab must be a UI element (RectTransform) with the following components:
    ///   1. Image              — displays the portal sprite
    ///   2. Animator           — plays portal animations (default state: "Idle")
    ///   3. AlphaHitTestImage  — enables shape-based click detection using sprite alpha channel
    ///   4. Button             — receives click events (OnClick wired to PortalController.OnPortalClicked)
    ///   5. PortalController   — this script, handles click/drag logic
    ///
    /// Note: The sprite texture MUST have Read/Write Enabled in import settings
    ///       for AlphaHitTestImage to work correctly.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public class PortalController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag Settings")]
        [Tooltip("Minimum drag distance (in pixels) to distinguish drag from click")]
        [Range(1f, 20f)]
        public float dragThreshold = 5f;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private Camera _canvasCamera;
        private Animator _animator;

        // Drag state
        private bool _isDragging;
        private Vector2 _dragStartPosition;
        private bool _dragExceededThreshold;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _animator = GetComponent<Animator>();

            // Find the parent Canvas
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                // For Screen Space - Camera or World Space canvas, use the canvas camera
                _canvasCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _parentCanvas.worldCamera;
            }
        }

        void Start()
        {
            // Play default Idle animation if Animator is present
            if (_animator != null)
            {
                _animator.Play("Idle");
            }
        }

        // =====================================================================
        // Click Handling
        // =====================================================================

        /// <summary>
        /// Called by the Button component's OnClick event.
        /// Only triggers if the interaction was a click (not a drag).
        /// Starts the Boss fight and passes this portal reference to BossFightManager.
        /// </summary>
        public void OnPortalClicked()
        {
            // If we just finished a drag, ignore this click
            if (_dragExceededThreshold)
            {
                return;
            }

            Debug.Log("[PortalController] Portal clicked, triggering Boss fight.");

            // Find BossFightManager and start Boss fight with this portal reference
            BossFightManager bossFightManager = FindObjectOfType<BossFightManager>();
            if (bossFightManager != null)
            {
                bossFightManager.StartBossFight(gameObject);
            }
            else
            {
                Debug.LogWarning("[PortalController] BossFightManager not found. Boss fight will not be triggered.");
            }
        }

        // =====================================================================
        // Drag Handling (IBeginDragHandler, IDragHandler, IEndDragHandler)
        // =====================================================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragExceededThreshold = false;
            _dragStartPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // Check if drag distance exceeds threshold
            float distance = Vector2.Distance(eventData.position, _dragStartPosition);
            if (distance >= dragThreshold)
            {
                _dragExceededThreshold = true;
            }

            if (!_dragExceededThreshold) return;

            // Move the portal to follow the mouse
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                _canvasCamera,
                out localPoint))
            {
                _rectTransform.localPosition = localPoint;
            }

            // Clamp position within camera visible bounds
            ClampToScreenBounds();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;

            // Reset drag flag after a short delay so the Button OnClick
            // (which fires after OnEndDrag) can check it
            if (_dragExceededThreshold)
            {
                // Use Invoke to reset the flag next frame, after Button.OnClick has been processed
                Invoke(nameof(ResetDragFlag), 0.05f);
            }
        }

        private void ResetDragFlag()
        {
            _dragExceededThreshold = false;
        }

        // =====================================================================
        // Screen Bounds Clamping
        // =====================================================================

        /// <summary>
        /// Clamp the portal's position so it stays within the camera's visible area.
        /// </summary>
        private void ClampToScreenBounds()
        {
            if (_parentCanvas == null) return;

            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            // Get the portal's rect size (half-extents)
            Vector2 portalHalfSize = _rectTransform.rect.size * 0.5f;

            // Get the canvas rect bounds (half-extents)
            Vector2 canvasHalfSize = canvasRect.rect.size * 0.5f;

            // Current local position relative to canvas
            Vector3 localPos = _rectTransform.localPosition;

            // Clamp within canvas bounds, accounting for portal size
            float clampedX = Mathf.Clamp(localPos.x,
                -canvasHalfSize.x + portalHalfSize.x,
                canvasHalfSize.x - portalHalfSize.x);
            float clampedY = Mathf.Clamp(localPos.y,
                -canvasHalfSize.y + portalHalfSize.y,
                canvasHalfSize.y - portalHalfSize.y);

            _rectTransform.localPosition = new Vector3(clampedX, clampedY, localPos.z);
        }
    }
}
