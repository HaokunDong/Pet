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

        [Header("Spline Edge Visibility")]
        [Tooltip("Cards with t-value within this margin from the Spline endpoints (0 or 1) will be hidden to avoid ugly edge rotation")]
        [Range(0f, 0.3f)]
        public float splineEdgeMargin = 0.05f;

        [Header("Focus Card Pop-up")]
        [Tooltip("How far the focused card pops up along the Spline normal (in UI units). Simulates the card rising out of the deck")]
        [Range(0f, 300f)]
        public float focusPopUpOffset = 50f;

        [Tooltip("Duration of the pop-up / pop-down animation")]
        [Range(0.05f, 1f)]
        public float focusPopUpDuration = 0.25f;

        [Tooltip("Ease curve for the pop-up animation")]
        public Ease focusPopUpEase = Ease.OutBack;

        [Header("Focus Card Scale")]
        [Tooltip("Scale multiplier applied to the focused card (1 = no change)")]
        [Range(1f, 2f)]
        public float focusScale = 1.2f;

        [Tooltip("Duration of the scale animation in seconds")]
        [Range(0.05f, 1f)]
        public float focusScaleDuration = 0.25f;

        [Tooltip("Ease curve for the scale animation")]
        public Ease focusScaleEase = Ease.OutBack;

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
