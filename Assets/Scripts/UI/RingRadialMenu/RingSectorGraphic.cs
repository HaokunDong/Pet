using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// A custom UI Image whose raycast hit-test is restricted to a ring sector
    /// (the intersection of an annulus and an angular wedge).
    ///
    /// <para>
    /// Used as the clickable background of a single button on a <see cref="RingRadialMenu"/>.
    /// Even when the rectTransform of this graphic overlaps with the inner hole or with
    /// neighbour buttons, only points whose distance to <see cref="ringCenterWorld"/> falls
    /// within [innerRadius, outerRadius] AND whose angle falls inside the assigned sector
    /// are considered a valid hit; all other clicks pass through to the layer below.
    /// </para>
    ///
    /// <para>
    /// Hit-test is performed in world-space of the rectTransform, using squared-distance
    /// comparison to avoid Mathf.Sqrt and any heap allocation.
    /// </para>
    ///
    /// <para>
    /// This component also serves as the click event source for the button cell.
    /// It implements <see cref="IPointerClickHandler"/> so that click events are dispatched
    /// directly from the sector graphic rather than from the parent button GameObject.
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Sector Graphic")]
    public class RingSectorGraphic : Image, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Ring Sector Hit-Test")]
        [Tooltip("Inner radius (in the ring center's local rect-space units). Clicks closer than this are ignored.")]
        [SerializeField] private float innerRadius = 80f;

        [Tooltip("Outer radius (in the ring center's local rect-space units). Clicks farther than this are ignored.")]
        [SerializeField] private float outerRadius = 160f;

        [Tooltip("Center angle of this sector, in degrees (0 = +X, 90 = +Y).")]
        [SerializeField] private float sectorCenterDegrees = 90f;

        [Tooltip("Angular size of this sector, in degrees (e.g. 360/8 = 45).")]
        [SerializeField] private float sectorAngleSize = 45f;

        [Tooltip("If false, the angular check is skipped and only the radial annulus is used. Enable when multiple sectors share the ring.")]
        [SerializeField] private bool useSectorAngleCheck = true;

        /// <summary>
        /// Reference to the rectTransform that defines the ring center.
        /// Usually set to the parent <see cref="RingRadialMenu"/>'s rectTransform.
        /// If null, this graphic's own rectTransform is used (treats it as the center).
        /// </summary>
        [Tooltip("RectTransform whose pivot represents the ring center. If null, this graphic's rectTransform is used.")]
        [SerializeField] private RectTransform ringCenter;

        [Header("Linked Panel")]
        [Tooltip("Optional reference to the panel/page GameObject that this button opens. " +
                 "When this GameObject is deactivated (SetActive(false)), the button automatically " +
                 "reverts from Selected back to Idle.")]
        [SerializeField] private GameObject linkedPanel;

        [Header("State Sprites")]
        [Tooltip("Sprite displayed when the button is in Idle state.")]
        [SerializeField] private Sprite idleSprite;

        [Tooltip("Sprite displayed when the pointer hovers over the sector (Suspended state).")]
        [SerializeField] private Sprite suspendedSprite;

        [Tooltip("Sprite displayed when the button is toggled on (Selected state).")]
        [SerializeField] private Sprite selectedSprite;

        // -----------------------------------------------------------------
        // Public read-only accessors (used by the custom Editor for Scene gizmos)
        // -----------------------------------------------------------------

        /// <summary>Inner radius of the ring sector.</summary>
        public float InnerRadius => innerRadius;

        /// <summary>Outer radius of the ring sector.</summary>
        public float OuterRadius => outerRadius;

        /// <summary>Center angle of this sector in degrees.</summary>
        public float SectorCenterDegrees => sectorCenterDegrees;

        /// <summary>Angular size of this sector in degrees.</summary>
        public float SectorAngleSize => sectorAngleSize;

        /// <summary>Whether the angular check is enabled.</summary>
        public bool UseSectorAngleCheck => useSectorAngleCheck;

        /// <summary>The RectTransform that defines the ring center.</summary>
        public RectTransform RingCenter => ringCenter;

        // -----------------------------------------------------------------
        // Click event delegation
        // -----------------------------------------------------------------

        /// <summary>
        /// Back-reference to the owning <see cref="RingRadialMenuButton"/>.
        /// Set by <see cref="RingRadialMenuButton.Initialize"/> during layout.
        /// Used to forward hover and toggle-select events.
        /// </summary>
        internal RingRadialMenuButton OwnerButton { get; set; }

        /// <summary>
        /// Tracks whether the pointer is currently inside this sector.
        /// Used to determine the correct fallback state when toggling off Selected.
        /// </summary>
        private bool isPointerInside;

        /// <summary>
        /// Tracks whether the linked panel was active on the previous frame.
        /// Used to detect the transition from active → inactive.
        /// </summary>
        private bool linkedPanelWasActive;

        /// <summary>
        /// Per-button click callback, registered via <see cref="SetCallback"/>.
        /// </summary>
        private UnityAction onClickCallback;

        /// <summary>
        /// Parent-level callback that also receives the button index.
        /// Set by <see cref="RingRadialMenu"/> when it builds buttons.
        /// </summary>
        private System.Action<int> onClickWithIndex;

        /// <summary>
        /// Index of the owning button within the parent <see cref="RingRadialMenu"/>.
        /// Assigned at layout-time.
        /// </summary>
        public int ButtonIndex { get; private set; }

        /// <summary>
        /// Initializes the button index and parent dispatcher for click events.
        /// Called by <see cref="RingRadialMenu"/> during layout.
        /// </summary>
        public void InitializeClick(int index, System.Action<int> dispatcher)
        {
            ButtonIndex = index;
            onClickWithIndex = dispatcher;
        }

        /// <summary>
        /// Registers (or replaces) the per-button click callback.
        /// </summary>
        public void SetCallback(UnityAction callback)
        {
            onClickCallback = callback;
        }

        /// <summary>
        /// Removes the per-button click callback.
        /// </summary>
        public void ClearCallback()
        {
            onClickCallback = null;
        }

        /// <summary>
        /// IPointerClickHandler entry. Triggered only when <see cref="IsRaycastLocationValid"/>
        /// has already validated the hit, so any click here is guaranteed to be inside the sector.
        ///
        /// <para>
        /// Toggle behaviour: clicking toggles the owning button between Selected and
        /// Idle/Suspended. The user callback is always invoked regardless of the toggle direction.
        /// </para>
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // Toggle the owning button's selected state.
            if (OwnerButton != null)
            {
                OwnerButton.ToggleSelected(isPointerInside);
            }

            try
            {
                onClickCallback?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
            }

            try
            {
                onClickWithIndex?.Invoke(ButtonIndex);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>
        /// IPointerEnterHandler entry. Notifies the owning button that the pointer
        /// has entered the sector area, triggering a transition to Suspended state
        /// (unless the button is already Selected).
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            if (OwnerButton != null)
            {
                OwnerButton.NotifyPointerEnter();
            }
        }

        /// <summary>
        /// IPointerExitHandler entry. Notifies the owning button that the pointer
        /// has left the sector area, triggering a transition back to Idle state
        /// (unless the button is Selected).
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            if (OwnerButton != null)
            {
                OwnerButton.NotifyPointerExit();
            }
        }

        // -----------------------------------------------------------------
        // Sector configuration
        // -----------------------------------------------------------------

        /// <summary>
        /// Configures the sector parameters at runtime. Called by RingRadialMenu when laying out.
        /// </summary>
        public void Configure(RectTransform center, float innerR, float outerR, float sectorCenterDeg, float sectorSizeDeg, bool useAngle)
        {
            ringCenter = center;
            innerRadius = innerR;
            outerRadius = outerR;
            sectorCenterDegrees = sectorCenterDeg;
            sectorAngleSize = sectorSizeDeg;
            useSectorAngleCheck = useAngle;
        }

        /// <summary>
        /// World-space center of the ring (read-only convenience).
        /// </summary>
        public Vector3 ringCenterWorld
        {
            get
            {
                RectTransform rt = ringCenter != null ? ringCenter : rectTransform;
                return rt.position;
            }
        }

        /// <summary>
        /// Overrides Unity's UI raycast hit-test to enforce the ring-sector shape.
        /// </summary>
        /// <param name="screenPoint">Screen-space point of the pointer.</param>
        /// <param name="eventCamera">UI event camera (may be null for ScreenSpaceOverlay).</param>
        /// <returns>True if the point lies inside the configured ring sector.</returns>
        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // First let the base class do its standard rect/alpha checks. If the
            // base already rejects (e.g. raycastTarget is false), keep that result.
            if (!base.IsRaycastLocationValid(screenPoint, eventCamera))
            {
                return false;
            }

            RectTransform centerRT = ringCenter != null ? ringCenter : rectTransform;

            // Convert the screen point to the local space of the ring-center rect.
            // In that local space the ring center is the origin (0,0) (assuming the
            // center rect has its pivot at the visual center).
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(centerRT, screenPoint, eventCamera, out localPoint))
            {
                return false;
            }

            // Radial check using squared distances (no Sqrt, no allocation).
            float sqrDist = localPoint.x * localPoint.x + localPoint.y * localPoint.y;
            float innerSqr = innerRadius * innerRadius;
            float outerSqr = outerRadius * outerRadius;
            if (sqrDist < innerSqr || sqrDist > outerSqr)
            {
                return false;
            }

            // Angular check (optional). Uses Atan2; only the radial check
            // is on the truly hot path for fast-rejects.
            if (useSectorAngleCheck && sectorAngleSize < 360f)
            {
                float pointAngleDeg = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(sectorCenterDegrees, pointAngleDeg);
                if (Mathf.Abs(delta) > sectorAngleSize * 0.5f)
                {
                    return false;
                }
            }

            return true;
        }

        // -----------------------------------------------------------------
        // State sprite switching
        // -----------------------------------------------------------------

        /// <summary>
        /// Applies the appropriate sprite based on the given button state.
        /// If the sprite for the given state is null, the current sprite is left unchanged.
        /// Called by <see cref="RingRadialMenuButton.SetState"/>.
        /// </summary>
        internal void ApplyStateSprite(RingRadialMenuButton.ButtonState state)
        {
            Sprite target = state switch
            {
                RingRadialMenuButton.ButtonState.Idle      => idleSprite,
                RingRadialMenuButton.ButtonState.Suspended => suspendedSprite,
                RingRadialMenuButton.ButtonState.Selected  => selectedSprite,
                _                                          => null
            };

            if (target != null)
            {
                sprite = target;
            }
        }

        // -----------------------------------------------------------------
        // Linked panel auto-deselect
        // -----------------------------------------------------------------

        /// <summary>
        /// The panel/page GameObject linked to this sector's button.
        /// When set, the sector monitors this object's active state and automatically
        /// deselects the owning button when the panel is deactivated.
        /// Can be assigned in the Inspector or at runtime via <see cref="SetLinkedPanel"/>.
        /// </summary>
        public GameObject LinkedPanel
        {
            get => linkedPanel;
            set => linkedPanel = value;
        }

        /// <summary>
        /// Sets the linked panel at runtime. When this panel's GameObject is deactivated,
        /// the owning button automatically reverts from Selected to Idle.
        /// Pass null to remove the link.
        /// </summary>
        public void SetLinkedPanel(GameObject panel)
        {
            linkedPanel = panel;
            linkedPanelWasActive = panel != null && panel.activeInHierarchy;
        }

        private void Update()
        {
            MonitorLinkedPanel();
        }

        /// <summary>
        /// Monitors the linked panel's active state. When the panel transitions
        /// from active to inactive while the owning button is Selected, the button
        /// automatically reverts to Idle.
        /// </summary>
        private void MonitorLinkedPanel()
        {
            if (linkedPanel == null) return;

            bool isActive = linkedPanel.activeInHierarchy;

            // Detect active → inactive transition.
            if (linkedPanelWasActive && !isActive)
            {
                if (OwnerButton != null && OwnerButton.IsSelected)
                {
                    OwnerButton.Deselect();
                }
            }

            linkedPanelWasActive = isActive;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (innerRadius < 0f) innerRadius = 0f;
            if (outerRadius < innerRadius + 1f) outerRadius = innerRadius + 1f;
            if (sectorAngleSize < 0f) sectorAngleSize = 0f;
            if (sectorAngleSize > 360f) sectorAngleSize = 360f;
        }
#endif
    }
}
