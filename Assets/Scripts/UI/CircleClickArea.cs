using UnityEngine;
using UnityEngine.UI;

namespace PetGame.UI
{
    /// <summary>
    /// Makes the click/touch area of a UI element circular with an adjustable radius.
    /// Attach this component to a UI GameObject to replace the default rectangular Image raycast.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CircleClickArea : Image
    {
        [Header("Circle Click Settings")]
        [Tooltip("Pixel radius of the clickable circle area. 0 = auto (use half of the smallest dimension of the RectTransform).")]
        [SerializeField]
        [Min(0f)]
        private float clickRadius = 0f;

        /// <summary>
        /// Gets or sets the click radius in pixels.
        /// Set to 0 for auto mode (half of the smallest RectTransform dimension).
        /// </summary>
        public float ClickRadius
        {
            get => clickRadius;
            set => clickRadius = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Returns the effective radius in local-space pixels.
        /// When clickRadius is 0, uses half of the smallest dimension.
        /// </summary>
        private float GetEffectiveRadius()
        {
            if (clickRadius > 0f)
                return clickRadius;
            Rect rect = rectTransform.rect;
            return Mathf.Min(rect.width, rect.height) * 0.5f;
        }

        /// <summary>
        /// Determines whether a given screen point falls within the circular click area.
        /// </summary>
        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out localPoint))
            {
                return false;
            }

            // Calculate pixel distance from center and compare with effective radius
            float effectiveRadius = GetEffectiveRadius();
            float distance = localPoint.magnitude;
            return distance <= effectiveRadius;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw the circle gizmo in Scene view for easy visual debugging.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (rectTransform == null) return;

            Rect rect = rectTransform.rect;
            Vector3 center = rectTransform.TransformPoint(rect.center);

            // Use the effective radius for gizmo drawing
            float effectiveRadius = (clickRadius > 0f)
                ? clickRadius
                : Mathf.Min(rect.width, rect.height) * 0.5f;
            float worldRadius = effectiveRadius * rectTransform.lossyScale.x;

            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            DrawGizmoCircle(center, worldRadius, 64);
        }

        private static void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
#endif
    }
}
