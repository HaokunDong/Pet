using UnityEngine;
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
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Sector Graphic")]
    public class RingSectorGraphic : Image
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
