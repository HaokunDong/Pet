using UnityEngine;
using UnityEngine.EventSystems;

namespace PetGame.UI
{
    /// <summary>
    /// Adds mouse-drag movement to the BlackHole UI element.
    /// Attach this to the BlackHole GameObject (it must live under a Canvas and have a Graphic
    /// with Raycast Target enabled, so the EventSystem dispatches drag events to it).
    ///
    /// Click vs Drag:
    ///   The Unity EventSystem only fires <c>OnPointerClick</c> when the pointer was NOT dragged
    ///   beyond the EventSystem's drag threshold, so the existing
    ///   <c>EventTriggerListener.onClick</c> handler on BlackHole keeps working without any
    ///   special "ignore click after drag" logic here.
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
        private RectTransform _parentRect;
        private Canvas _parentCanvas;
        private Camera _canvasCamera;

        // Captured at OnBeginDrag so the cursor stays at the same offset relative to the
        // BlackHole's pivot for the entire drag (prevents the element snapping under the cursor).
        private Vector2 _dragLocalOffset;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _parentRect = _rectTransform.parent as RectTransform;

            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _canvasCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _parentCanvas.worldCamera;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_parentRect == null) return;

            Vector2 pointerLocal;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect,
                    eventData.position,
                    _canvasCamera,
                    out pointerLocal))
            {
                // Distance from current anchored position to the cursor's local position.
                _dragLocalOffset = (Vector2)_rectTransform.localPosition - pointerLocal;
            }
            else
            {
                _dragLocalOffset = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_parentRect == null) return;

            Vector2 pointerLocal;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect,
                    eventData.position,
                    _canvasCamera,
                    out pointerLocal))
            {
                return;
            }

            Vector2 target = pointerLocal + _dragLocalOffset;
            Vector3 lp = _rectTransform.localPosition;
            lp.x = target.x;
            lp.y = target.y;
            _rectTransform.localPosition = lp;

            if (clampToCanvas)
                ClampToCanvasBounds();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Nothing to clean up; click suppression is handled automatically by EventSystem.
        }

        /// <summary>
        /// Clamp the BlackHole's localPosition so its rect stays inside the parent Canvas rect.
        /// </summary>
        private void ClampToCanvasBounds()
        {
            if (_parentCanvas == null) return;

            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            Vector2 halfSize = _rectTransform.rect.size * 0.5f;
            Vector2 canvasHalf = canvasRect.rect.size * 0.5f;

            Vector3 lp = _rectTransform.localPosition;
            float clampedX = Mathf.Clamp(lp.x, -canvasHalf.x + halfSize.x, canvasHalf.x - halfSize.x);
            float clampedY = Mathf.Clamp(lp.y, -canvasHalf.y + halfSize.y, canvasHalf.y - halfSize.y);
            _rectTransform.localPosition = new Vector3(clampedX, clampedY, lp.z);
        }
    }
}
