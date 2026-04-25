using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Global settings for outline highlight and move-arrow indicator effects.
    /// Create a single asset via Assets > Create > Game > OutlineSettings and
    /// place it in a Resources folder so it can be loaded at runtime automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "OutlineSettings", menuName = "Game/OutlineSettings")]
    public class OutlineSettings : ScriptableObject
    {
        [Header("Outline Settings")]
        [Tooltip("Outline color (default red)")]
        public Color outlineColor = Color.red;

        [Tooltip("Outline thickness when mouse hovers over the character")]
        [Range(0.5f, 5f)]
        public float hoverThickness = 1.5f;

        [Tooltip("Outline thickness for bold pulse on right-click")]
        [Range(1f, 10f)]
        public float boldThickness = 3.5f;

        [Tooltip("Duration of the bold pulse effect in seconds")]
        [Range(0.05f, 1f)]
        public float boldDuration = 0.2f;

        [Header("Arrow Indicator Settings")]
        [Tooltip("Arrow indicator color")]
        public Color arrowColor = new Color(1f, 1f, 0f, 0.9f);

        [Tooltip("Arrow indicator scale")]
        [Range(0.1f, 2f)]
        public float arrowScale = 0.5f;

        [Tooltip("Arrow Y position in world coordinates")]
        [Range(-10f, 10f)]
        public float arrowYOffset = 0f;

        [Tooltip("Arrow animation duration")]
        [Range(0.1f, 2f)]
        public float arrowDuration = 0.5f;

        // =====================================================================
        // Singleton-style global access
        // =====================================================================

        private static OutlineSettings _instance;

        /// <summary>
        /// Global singleton instance. Automatically loads from Resources folder.
        /// If no asset is found, a default instance is created in memory.
        /// </summary>
        public static OutlineSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<OutlineSettings>("OutlineSettings");

                    if (_instance == null)
                    {
                        Debug.LogWarning("[OutlineSettings] No OutlineSettings asset found in Resources folder. " +
                            "Using default values. Create one via Assets > Create > Game > OutlineSettings " +
                            "and place it in a Resources folder.");
                        _instance = CreateInstance<OutlineSettings>();
                    }
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: allow direct assignment for testing without Resources folder.
        /// </summary>
        public static void SetInstanceForEditor(OutlineSettings settings)
        {
            _instance = settings;
        }
#endif
    }
}
