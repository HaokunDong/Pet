using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Global settings for the Portal System.
    /// Create a single asset via Assets > Create > Game > PortalSettings and
    /// place it in a Resources folder so it can be loaded at runtime automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "PortalSettings", menuName = "Game/PortalSettings")]
    public class PortalSettings : ScriptableObject
    {
        [Header("Spawn Area Settings")]
        [Tooltip("Center position of the spawn area in world space")]
        public Vector2 spawnAreaCenter = Vector2.zero;

        [Tooltip("Width and height of the spawn area in world space")]
        public Vector2 spawnAreaSize = new Vector2(6f, 4f);

        [Header("Portal Prefab")]
        [Tooltip("The portal prefab to instantiate. Must contain: Image, Animator, AlphaHitTestImage, Button, PortalController")]
        public GameObject portalPrefab;

        [Header("Spawn Button Settings")]
        [Tooltip("Anchor position of the spawn button on screen (normalized 0-1). Default is top-center.")]
        public Vector2 spawnButtonPosition = new Vector2(0.5f, 0.95f);

        // =====================================================================
        // Singleton-style global access
        // =====================================================================

        private static PortalSettings _instance;

        /// <summary>
        /// Global singleton instance. Automatically loads from Resources folder.
        /// If no asset is found, a default instance is created in memory.
        /// </summary>
        public static PortalSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PortalSettings>("PortalSettings");

                    if (_instance == null)
                    {
                        Debug.LogWarning("[PortalSettings] No PortalSettings asset found in Resources folder. " +
                            "Using default values. Create one via Assets > Create > Game > PortalSettings " +
                            "and place it in a Resources folder.");
                        _instance = CreateInstance<PortalSettings>();
                    }
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: allow direct assignment for testing without Resources folder.
        /// </summary>
        public static void SetInstanceForEditor(PortalSettings settings)
        {
            _instance = settings;
        }
#endif
    }
}
