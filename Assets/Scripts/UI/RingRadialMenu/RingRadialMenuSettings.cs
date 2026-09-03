using System;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Serializable settings for a Ring Radial Menu.
    /// Controls the geometry (inner/outer radius), button count and starting angle.
    /// </summary>
    [Serializable]
    public class RingRadialMenuSettings
    {
        [Tooltip("Inner radius of the ring, in canvas (RectTransform) units. Must be >= 0 and < outerRadius.")]
        [Min(0f)]
        public float innerRadius = 80f;

        [Tooltip("Outer radius of the ring, in canvas (RectTransform) units. Must be > innerRadius.")]
        [Min(0f)]
        public float outerRadius = 160f;

        [Tooltip("Number of buttons distributed evenly on the ring. Default 8. Must be > 0.")]
        [Min(1)]
        public int buttonCount = 8;

        [Tooltip("Starting angle in degrees for the first button. 0 = right (+X), 90 = up (+Y). Default is 90 (top).")]
        public float startAngleDegrees = 90f;

        [Tooltip("If true, buttons are placed clockwise; otherwise counter-clockwise.")]
        public bool clockwise = true;

        [Header("Sector Size")]
        [Tooltip("Width of each sector's RectTransform (in canvas units). 0 means stretch to fill the button cell.")]
        [Min(0f)]
        public float sectorWidth = 0f;

        [Tooltip("Height of each sector's RectTransform (in canvas units). 0 means stretch to fill the button cell.")]
        [Min(0f)]
        public float sectorHeight = 0f;

        [Header("Scroll Rotation")]
        [Tooltip("If true, scrolling the mouse wheel while the cursor is inside the outer circle rotates the wheel.")]
        public bool enableScrollRotate = true;

        [Tooltip("Rotation step (in degrees) applied per single mouse-wheel notch. Default 15.")]
        [Min(0f)]
        public float scrollRotateStepDegrees = 15f;

        /// <summary>
        /// Validates the settings, auto-fixing illegal values and emitting console warnings.
        /// Called by RingRadialMenu in OnValidate / runtime initialization.
        /// </summary>
        /// <returns>True if the values were already legal; false if any value was auto-corrected.</returns>
        public bool Validate()
        {
            bool ok = true;

            if (innerRadius < 0f)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] innerRadius ({innerRadius}) cannot be negative. Clamped to 0.");
                innerRadius = 0f;
                ok = false;
            }

            if (outerRadius < 0f)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] outerRadius ({outerRadius}) cannot be negative. Clamped to 1.");
                outerRadius = 1f;
                ok = false;
            }

            if (innerRadius >= outerRadius)
            {
                float fixedOuter = Mathf.Max(innerRadius + 1f, outerRadius);
                Debug.LogWarning($"[RingRadialMenuSettings] innerRadius ({innerRadius}) must be < outerRadius ({outerRadius}). Auto-fixed outerRadius to {fixedOuter}.");
                outerRadius = fixedOuter;
                ok = false;
            }

            if (buttonCount < 1)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] buttonCount ({buttonCount}) must be >= 1. Clamped to 1.");
                buttonCount = 1;
                ok = false;
            }

            if (scrollRotateStepDegrees < 0f)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] scrollRotateStepDegrees ({scrollRotateStepDegrees}) cannot be negative. Clamped to 0.");
                scrollRotateStepDegrees = 0f;
                ok = false;
            }

            if (sectorWidth < 0f)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] sectorWidth ({sectorWidth}) cannot be negative. Clamped to 0.");
                sectorWidth = 0f;
                ok = false;
            }

            if (sectorHeight < 0f)
            {
                Debug.LogWarning($"[RingRadialMenuSettings] sectorHeight ({sectorHeight}) cannot be negative. Clamped to 0.");
                sectorHeight = 0f;
                ok = false;
            }

            return ok;
        }

        /// <summary>
        /// Returns the radius at which button centers should be placed (mid-ring).
        /// </summary>
        public float GetButtonCenterRadius()
        {
            return (innerRadius + outerRadius) * 0.5f;
        }

        /// <summary>
        /// Returns the angular size (in degrees) of one sector.
        /// </summary>
        public float GetSectorAngleSize()
        {
            return 360f / Mathf.Max(1, buttonCount);
        }

        /// <summary>
        /// Returns the center angle (in degrees, in standard math convention:
        /// 0 = +X, 90 = +Y) for the button at the given index.
        /// </summary>
        public float GetButtonAngleDegrees(int index)
        {
            float step = GetSectorAngleSize();
            float dir = clockwise ? -1f : 1f;
            return startAngleDegrees + dir * step * index;
        }
    }
}
