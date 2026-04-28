using DG.Tweening;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Global settings for the card-based character selection system.
    /// Create a single asset via Assets > Create > Game > CardSelectionSettings and
    /// place it in a Resources folder so it can be loaded at runtime automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "CardSelectionSettings", menuName = "Game/CardSelectionSettings")]
    public class CardSelectionSettings : ScriptableObject
    {
        [Header("DOTween Snap Animation")]
        [Tooltip("Duration of the snap-to-center animation in seconds")]
        [Range(0.05f, 2f)]
        public float snapDuration = 0.3f;

        [Tooltip("Ease curve for the snap animation")]
        public Ease snapEase = Ease.OutCubic;

        [Header("Inertia")]
        [Tooltip("Inertia deceleration coefficient. Higher = stops faster")]
        [Range(1f, 20f)]
        public float inertiaDamping = 8f;

        [Tooltip("Minimum velocity threshold to trigger inertia scrolling")]
        [Range(0f, 0.5f)]
        public float inertiaMinVelocity = 0.01f;

        [Header("Card Spacing")]
        [Tooltip("Spacing between cards along the Spline (in t-value, 0~1)")]
        [Range(0.01f, 0.5f)]
        public float cardSpacing = 0.1f;

        [Header("Focus Position")]
        [Tooltip("The Spline t-value that represents the focus/center position")]
        [Range(0f, 1f)]
        public float focusT = 0.5f;

        [Header("Drag Sensitivity")]
        [Tooltip("How much mouse drag pixels translate to Spline t-value offset")]
        [Range(0.0001f, 0.01f)]
        public float dragSensitivity = 0.001f;

        [Header("Hand Fan Layout")]
        [Tooltip("Angle (degrees) between adjacent cards in the fan. Positive = spread wider")]
        [Range(0f, 30f)]
        public float fanAnglePerCard = 5f;

        [Tooltip("Radius of the virtual circle that cards fan around (in world units). Larger = flatter arc")]
        [Range(100f, 5000f)]
        public float fanRadius = 1200f;

        [Tooltip("Vertical offset of the fan circle center below the focus point (in local units)")]
        [Range(0f, 3000f)]
        public float fanCenterYOffset = 800f;

        [Tooltip("Duration of the rotation transition when focus changes")]
        [Range(0.05f, 1f)]
        public float rotationTransitionDuration = 0.2f;

        [Tooltip("Ease curve for the rotation transition")]
        public Ease rotationTransitionEase = Ease.OutQuad;

        [Header("Focus Card Pop-up")]
        [Tooltip("How far the focused card pops up from the fan arc (in UI units). Simulates the card rising out of the deck")]
        [Range(0f, 300f)]
        public float focusPopUpOffset = 50f;

        [Tooltip("Duration of the pop-up / pop-down animation")]
        [Range(0.05f, 1f)]
        public float focusPopUpDuration = 0.25f;

        [Tooltip("Ease curve for the pop-up animation")]
        public Ease focusPopUpEase = Ease.OutBack;

        // =====================================================================
        // Singleton-style global access
        // =====================================================================

        private static CardSelectionSettings _instance;

        /// <summary>
        /// Global singleton instance. Automatically loads from Resources folder.
        /// If no asset is found, a default instance is created in memory.
        /// </summary>
        public static CardSelectionSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CardSelectionSettings>("CardSelectionSettings");

                    if (_instance == null)
                    {
                        Debug.LogWarning("[CardSelectionSettings] No CardSelectionSettings asset found in Resources folder. " +
                            "Using default values. Create one via Assets > Create > Game > CardSelectionSettings " +
                            "and place it in a Resources folder.");
                        _instance = CreateInstance<CardSelectionSettings>();
                    }
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: allow direct assignment for testing without Resources folder.
        /// </summary>
        public static void SetInstanceForEditor(CardSelectionSettings settings)
        {
            _instance = settings;
        }
#endif
    }
}
