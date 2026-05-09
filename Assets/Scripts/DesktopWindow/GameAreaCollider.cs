using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Defines the clickable game area boundary using a BoxCollider2D.
    /// Place this on a GameObject with a BoxCollider2D set to the "GameArea" layer.
    /// The collider acts as an invisible boundary that prevents mouse clicks
    /// from passing through to the desktop when the window background is transparent.
    /// 
    /// Setup:
    /// 1. Create an empty GameObject in your scene.
    /// 2. Set its Layer to "GameArea" (Layer 10).
    /// 3. Attach this script — it will auto-add a BoxCollider2D (trigger).
    /// 4. Adjust the collider size in the Inspector to cover your desired play area.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class GameAreaCollider : MonoBehaviour
    {
        [Header("Auto-Size Settings")]
        [Tooltip("If true, automatically sizes the collider to match the camera's visible area on Start.")]
        [SerializeField] private bool autoSizeToCamera = false;

        [Tooltip("Padding to add/subtract from the auto-sized area (in world units).")]
        [SerializeField] private Vector2 padding = Vector2.zero;

        private BoxCollider2D _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true; // Ensure it doesn't affect physics

            // Ensure correct layer
            if (gameObject.layer != LayerMask.NameToLayer("GameArea"))
            {
                gameObject.layer = LayerMask.NameToLayer("GameArea");
                Debug.Log("[GameAreaCollider] Layer automatically set to 'GameArea'.");
            }
        }

        private void Start()
        {
            if (autoSizeToCamera)
            {
                SizeToCamera();
            }
        }

        /// <summary>
        /// Sizes the BoxCollider2D to match the main camera's orthographic visible area.
        /// </summary>
        public void SizeToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                Debug.LogWarning("[GameAreaCollider] Main camera not found or not orthographic. Cannot auto-size.");
                return;
            }

            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;

            _collider.size = new Vector2(width + padding.x, height + padding.y);
            _collider.offset = Vector2.zero;

            // Center the GameObject at the camera's position (XY only)
            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, 0f);

            Debug.Log($"[GameAreaCollider] Auto-sized to camera: {_collider.size.x:F1} x {_collider.size.y:F1} world units.");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw the game area boundary in the Scene view for easy visualization.
        /// </summary>
        private void OnDrawGizmos()
        {
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = new Vector3(col.size.x, col.size.y, 0.01f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
