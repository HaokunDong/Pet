using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame.UI
{
    /// <summary>
    /// Adds mouse-drag movement to the BlackHole UI element.
    /// Attach this to the BlackHole GameObject (it must live under a Canvas and have a Graphic
    /// with Raycast Target enabled, so the EventSystem dispatches drag events to it).
    ///
    /// Click vs Drag:
    ///   The drag/click distinction is handled by EventTriggerListener's _isDragging flag.
    ///   When a drag occurs, EventTriggerListener suppresses the subsequent OnPointerClick.
    ///
    /// Follow effect:
    ///   The RingRadialMenu's center-target follow logic (see RingRadialMenu.centerTarget)
    ///   automatically keeps the menu anchored to BlackHole's screen position, so dragging
    ///   BlackHole drags the whole radial menu along with it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BlackHoleDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Bounds")]
        [Tooltip("If true, clamp the BlackHole's anchored position so it stays inside the parent Canvas rect.")]
        [SerializeField] private bool clampToCanvas = true;

        private RectTransform _rectTransform;
        private RectTransform _canvasRect;
        private Canvas _parentCanvas;
        private Camera _canvasCamera;
        private Graphic _graphic;

        // Captured at OnBeginDrag so the cursor stays at the same offset relative to the
        // BlackHole's pivot for the entire drag (prevents the element snapping under the cursor).
        private Vector2 _dragLocalOffset;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _graphic = GetComponent<Graphic>();

            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                // Use the Canvas RectTransform as the coordinate reference instead of
                // the direct parent (MainView), which has a small size and is anchored
                // to the bottom-right corner, causing drag to be limited to the right half.
                _canvasRect = _parentCanvas.GetComponent<RectTransform>();
                _canvasCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _parentCanvas.worldCamera;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvasRect == null) return;

            // Convert current BlackHole world position to Canvas local position
            Vector2 currentLocalInCanvas = GetLocalPositionInCanvas();

            // Convert pointer screen position to Canvas local position
            Vector2 pointerLocalInCanvas;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    eventData.position,
                    _canvasCamera,
                    out pointerLocalInCanvas))
            {
                // Offset = BlackHole's canvas-local position minus pointer's canvas-local position
                _dragLocalOffset = currentLocalInCanvas - pointerLocalInCanvas;
            }
            else
            {
                _dragLocalOffset = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvasRect == null) return;

            Vector2 pointerLocalInCanvas;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    eventData.position,
                    _canvasCamera,
                    out pointerLocalInCanvas))
            {
                return;
            }

            // Target position in Canvas local space
            Vector2 targetInCanvas = pointerLocalInCanvas + _dragLocalOffset;

            // Convert Canvas local position back to BlackHole's parent local position
            SetPositionFromCanvasLocal(targetInCanvas);

            if (clampToCanvas)
                ClampToCanvasBounds();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Nothing to clean up; click suppression is handled by EventTriggerListener.
        }

        /// <summary>
        /// Get the BlackHole's current position in Canvas local coordinates.
        /// </summary>
        private Vector2 GetLocalPositionInCanvas()
        {
            // Convert BlackHole's world position to Canvas local position
            Vector3 worldPos = _rectTransform.position;
            Vector2 localInCanvas = _canvasRect.InverseTransformPoint(worldPos);
            return localInCanvas;
        }

        /// <summary>
        /// Set the BlackHole's position given a target in Canvas local coordinates.
        /// Converts from Canvas local space to the BlackHole's parent local space.
        /// </summary>
        private void SetPositionFromCanvasLocal(Vector2 canvasLocalPos)
        {
            // Convert Canvas local position to world position
            Vector3 worldPos = _canvasRect.TransformPoint(canvasLocalPos);

            // Convert world position to BlackHole's parent local position
            Transform parent = _rectTransform.parent;
            if (parent != null)
            {
                Vector3 localPos = parent.InverseTransformPoint(worldPos);
                _rectTransform.localPosition = localPos;
            }
            else
            {
                _rectTransform.position = worldPos;
            }
        }

        /// <summary>
        /// Clamp the BlackHole's position so that its Raycast Padding area (the effective
        /// interaction region, shown as the green box in the editor) stays inside the Canvas.
        /// This allows the visual element to partially go off-screen, as long as the
        /// raycast-padded region remains fully within bounds.
        /// </summary>
        private void ClampToCanvasBounds()
        {
            if (_canvasRect == null) return;

            // Get current position in Canvas local space
            Vector2 currentInCanvas = GetLocalPositionInCanvas();

            // Get the raycast padding (left, bottom, right, top) from the Graphic component.
            // In Unity, positive raycastPadding values SHRINK the raycast area (inset edges).
            // Format: x=left, y=bottom, z=right, w=top
            Vector4 padding = Vector4.zero;
            if (_graphic != null)
            {
                padding = _graphic.raycastPadding;
            }

            // Calculate the raycast-padded rect's half-size in Canvas local units.
            // The rect size is the RectTransform's size, and padding shrinks it.
            // Padding values are in the element's local space, so we need to account for scale.
            Vector2 rectSize = _rectTransform.rect.size;

            // Padded size in element local space:
            // In Unity, positive raycastPadding values SHRINK (inset) the raycast area.
            // paddedWidth = rectSize.x - left - right
            // paddedHeight = rectSize.y - bottom - top
            float paddedWidth = rectSize.x - padding.x - padding.z;
            float paddedHeight = rectSize.y - padding.y - padding.w;

            // Convert padded half-size to Canvas local units using scale ratio
            Vector3 elementScale = _rectTransform.lossyScale;
            Vector3 canvasScale = _canvasRect.lossyScale;
            Vector2 halfSize = new Vector2(
                paddedWidth * elementScale.x / canvasScale.x * 0.5f,
                paddedHeight * elementScale.y / canvasScale.y * 0.5f
            );
            halfSize = new Vector2(Mathf.Abs(halfSize.x), Mathf.Abs(halfSize.y));

            Vector2 canvasHalf = _canvasRect.rect.size * 0.5f;

            // Clamp so the padded area stays within canvas
            float minX = -canvasHalf.x + halfSize.x;
            float maxX = canvasHalf.x - halfSize.x;
            float minY = -canvasHalf.y + halfSize.y;
            float maxY = canvasHalf.y - halfSize.y;

            // If padded area is larger than canvas, allow free movement
            if (minX > maxX) { minX = -canvasHalf.x; maxX = canvasHalf.x; }
            if (minY > maxY) { minY = -canvasHalf.y; maxY = canvasHalf.y; }

            float clampedX = Mathf.Clamp(currentInCanvas.x, minX, maxX);
            float clampedY = Mathf.Clamp(currentInCanvas.y, minY, maxY);

            // Only update if clamping actually changed the position
            if (!Mathf.Approximately(clampedX, currentInCanvas.x) || !Mathf.Approximately(clampedY, currentInCanvas.y))
            {
                SetPositionFromCanvasLocal(new Vector2(clampedX, clampedY));
            }
        }
    }
}
