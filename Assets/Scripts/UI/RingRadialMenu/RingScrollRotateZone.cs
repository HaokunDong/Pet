using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// Invisible UGUI graphic placed at the ring center that captures mouse-scroll
    /// events whose pointer lies inside the outer circle of a <see cref="RingRadialMenu"/>.
    ///
    /// <para>
    /// Behaviour:
    /// <list type="bullet">
    /// <item>Raycast hit-test covers the whole disk (<c>distance &lt;= outerRadius</c>) so
    /// that scroll-wheel input anywhere inside the outer circle reaches this component.</item>
    /// <item>Click / drag events that hit this zone (i.e. they were not absorbed by the
    /// per-button <see cref="RingSectorGraphic"/> on the annulus) are re-dispatched to the
    /// next raycast target underneath, effectively making the inner hole click-through.</item>
    /// <item>When <see cref="RingRadialMenu.IsScrollRotateEnabled"/> is false the
    /// hit-test always returns false so the scroll event passes through directly.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The component derives from <see cref="Graphic"/> instead of <see cref="Image"/>
    /// because it has no visual; <see cref="OnPopulateMesh"/> is overridden to emit
    /// no geometry, keeping draw-call cost at zero while still letting the UI raycaster
    /// route events through it.
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Scroll Rotate Zone")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class RingScrollRotateZone : Graphic,
        IScrollHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Tooltip("The Ring Radial Menu this zone routes scroll events to. Auto-resolved from the parent if left null.")]
        [SerializeField] private RingRadialMenu menu;

        // Reused per-frame buffer for RaycastAll, to avoid GC allocations.
        private static readonly List<RaycastResult> s_RaycastBuffer = new List<RaycastResult>(8);

        protected override void Awake()
        {
            base.Awake();
            ResolveMenu();
            // We never want to render anything; this graphic is purely for hit-testing.
            this.color = new Color(0f, 0f, 0f, 0f);
            this.raycastTarget = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveMenu();
        }
#endif

        /// <summary>
        /// Allows external code to inject the menu reference (used by editor tools that
        /// build the prefab hierarchy programmatically).
        /// </summary>
        public void SetMenu(RingRadialMenu target)
        {
            menu = target;
        }

        private void ResolveMenu()
        {
            if (menu == null)
            {
                menu = GetComponentInParent<RingRadialMenu>();
            }
        }

        /// <summary>
        /// Suppress mesh generation so the zone never produces visible pixels.
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        /// <summary>
        /// Restrict raycast hits to the disk inside <c>outerRadius</c> around the menu center.
        ///
        /// <para>
        /// Note: <see cref="Graphic"/> implements <see cref="ICanvasRaycastFilter"/> but does
        /// not declare this method as <c>virtual</c>, so we must re-implement the interface
        /// here (using <c>new</c>) instead of <c>override</c>.
        /// </para>
        /// </summary>
        public new bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (menu == null)
            {
                ResolveMenu();
                if (menu == null) return false;
            }

            // Honor the master toggle: when disabled we let the scroll event pass through.
            if (!menu.IsScrollRotateEnabled)
            {
                return false;
            }

            // Convert the screen point into the menu center's local space (the menu's
            // RectTransform pivot is the ring center).
            RectTransform centerRT = menu.RectTransform;
            if (centerRT == null) return false;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(centerRT, screenPoint, eventCamera, out localPoint))
            {
                return false;
            }

            float outerR = menu.OuterRadius;
            float sqrDist = localPoint.x * localPoint.x + localPoint.y * localPoint.y;
            return sqrDist <= outerR * outerR;
        }

        /// <summary>
        /// Forward the wheel delta to the menu only after the raycast accepted us — UGUI
        /// guarantees this callback fires only when <see cref="IsRaycastLocationValid"/>
        /// returned true.
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (menu == null || eventData == null) return;
            if (!menu.IsScrollRotateEnabled) return;

            // Forward the vertical scroll delta. The menu owns the direction convention
            // (positive y -> clockwise, negative y -> counter-clockwise) so that we
            // keep that policy in a single place.
            float deltaY = eventData.scrollDelta.y;
            if (Mathf.Approximately(deltaY, 0f)) return;

            menu.HandleScrollRotate(deltaY);
            eventData.Use();
        }

        // ------------------------------------------------------------------
        // Click-through forwarding
        //
        // A pointer event reaches this component only when no button's
        // RingSectorGraphic accepted the hit -- which means the cursor is on
        // the inner hole, on a gap between sectors, or on the disk's outer
        // margin. In every such case the menu must NOT consume the event;
        // instead we re-dispatch it to the next UI/3D target underneath, so
        // things like the BlackHole / pet beneath the menu can still react.
        // ------------------------------------------------------------------

        public void OnPointerDown(PointerEventData eventData)         => ForwardEvent(eventData, ExecuteEvents.pointerDownHandler);
        public void OnPointerUp(PointerEventData eventData)           => ForwardEvent(eventData, ExecuteEvents.pointerUpHandler);
        public void OnPointerClick(PointerEventData eventData)        => ForwardEvent(eventData, ExecuteEvents.pointerClickHandler);
        public void OnInitializePotentialDrag(PointerEventData ed)    => ForwardEvent(ed,        ExecuteEvents.initializePotentialDrag);
        public void OnBeginDrag(PointerEventData eventData)           => ForwardEvent(eventData, ExecuteEvents.beginDragHandler);
        public void OnDrag(PointerEventData eventData)                => ForwardEvent(eventData, ExecuteEvents.dragHandler);
        public void OnEndDrag(PointerEventData eventData)             => ForwardEvent(eventData, ExecuteEvents.endDragHandler);

        /// <summary>
        /// Re-dispatches <paramref name="eventData"/> to the first raycast target located
        /// beneath this zone, using the supplied <paramref name="handler"/> to invoke the
        /// matching interface method on it. If no underlying target wants the event, this
        /// is a no-op (the event silently passes through, which is exactly what we want
        /// for the "inner-hole click-through" requirement).
        /// </summary>
        private void ForwardEvent<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
        {
            if (eventData == null || handler == null) return;
            if (EventSystem.current == null) return;

            s_RaycastBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, s_RaycastBuffer);

            GameObject self = gameObject;
            for (int i = 0; i < s_RaycastBuffer.Count; ++i)
            {
                GameObject hit = s_RaycastBuffer[i].gameObject;
                if (hit == null) continue;
                // Skip ourselves and any descendants of ourselves so we never re-enter.
                if (hit == self || hit.transform.IsChildOf(self.transform)) continue;

                // ExecuteHierarchy walks up parents looking for the first object that
                // implements <T>; that mirrors UGUI's normal dispatch semantics.
                GameObject handled = ExecuteEvents.ExecuteHierarchy(hit, eventData, handler);
                if (handled != null)
                {
                    // Update pointerPress / selectedObject bookkeeping so subsequent
                    // up/click events get routed to the same target.
                    if (handler == (object)ExecuteEvents.pointerDownHandler)
                    {
                        eventData.pointerPress = handled;
                        eventData.rawPointerPress = handled;
                    }
                    break;
                }
            }

            s_RaycastBuffer.Clear();
        }
    }
}

