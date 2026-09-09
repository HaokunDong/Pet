using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;
using PetGame.Network;
using System;

namespace PetGame
{
    /// <summary>
    /// Controls portal click interaction, drag movement, and animation.
    /// Attach this script to the portal prefab GameObject.
    ///
    /// === Portal Prefab Setup ===
    /// The portal prefab must be a UI element (RectTransform) with the following components:
    ///   1. Image              — displays the portal sprite
    ///   2. Animator           — plays portal animations (default state: "Idle")
    ///   3. AlphaHitTestImage  — enables shape-based click detection using sprite alpha channel
    ///   4. Button             — receives click events (OnClick wired to PortalController.OnPortalClicked)
    ///   5. PortalController   — this script, handles click/drag logic
    ///
    /// Note: The sprite texture MUST have Read/Write Enabled in import settings
    ///       for AlphaHitTestImage to work correctly.
    /// Portal is a local UI element (not a networked object). Each client spawns its own
    /// Portal instance on its own Canvas. A shared portalId is used for network identification.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public class PortalController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>
        /// Fired when a portal request produces user-facing feedback (success or failure).
        /// Parameter: message string to display to the user.
        /// </summary>
        public static event Action<string> OnPortalRequestFeedback;

        /// <summary>
        /// Unique portal ID assigned by the server. Used for network identification
        /// instead of NetworkIdentity.netId since Portal is a local UI element.
        /// </summary>
        [HideInInspector]
        public uint portalId;
        [Header("Special Level")]
        [Tooltip("SpecialLevelData associated with this portal for multiplayer level requests")]
        public SpecialLevelData specialLevelData;

        [Header("Drag Settings")]
        [Tooltip("Minimum drag distance (in pixels) to distinguish drag from click")]
        [Range(1f, 20f)]
        public float dragThreshold = 5f;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private Camera _canvasCamera;
        private Animator _animator;

        // Drag state
        private bool _isDragging;
        private Vector2 _dragStartPosition;
        private bool _dragExceededThreshold;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _animator = GetComponent<Animator>();

            // Find the parent Canvas
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                // For Screen Space - Camera or World Space canvas, use the canvas camera
                _canvasCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _parentCanvas.worldCamera;
            }

            // Subscribe to server rejection events for user feedback
            var levelListMgr = SpecialLevelListManager.Instance;
            if (levelListMgr == null)
                levelListMgr = FindObjectOfType<SpecialLevelListManager>();
            if (levelListMgr != null)
            {
                levelListMgr.OnRequestRejected += HandleRequestRejected;
            }
        }

        void Start()
        {
            // Play default Idle animation if Animator is present
            if (_animator != null)
            {
                _animator.Play("Idle");
            }
        }

        // =====================================================================
        // Click Handling
        // =====================================================================

        /// <summary>
        /// Called by the Button component's OnClick event.
        /// Only triggers if the interaction was a click (not a drag).
        /// Starts the Boss fight and passes this portal reference to BossFightManager.
        /// </summary>
        public void OnPortalClicked()
        {
            // If we just finished a drag, ignore this click
            if (_dragExceededThreshold)
            {
                return;
            }

            // Check if we are in multiplayer mode
            var lobbyMgr = SteamLobbyManager.Instance;
            if ((lobbyMgr != null && lobbyMgr.InLobby) ||
                (NetworkClient.active && !NetworkServer.active))
            {
                // Multiplayer mode: submit a request to the SpecialLevelListManager
                HandleMultiplayerPortalClick();
            }
            else
            {
                // Single player mode: directly start boss fight
                HandleSinglePlayerPortalClick();
            }
        }

        /// <summary>
        /// Handle portal click in single player mode - directly starts boss fight.
        /// </summary>
        private void HandleSinglePlayerPortalClick()
        {
            Debug.Log("[PortalController] Portal clicked (single player), triggering Boss fight.");

            BossFightManager bossFightManager = FindObjectOfType<BossFightManager>();
            if (bossFightManager != null)
            {
                bossFightManager.StartBossFight(gameObject, portalId);
            }
            else
            {
                Debug.LogWarning("[PortalController] BossFightManager not found. Boss fight will not be triggered.");
            }
        }

        /// <summary>
        /// Handle portal click in multiplayer mode - submits a request to the level list.
        /// </summary>
        private void HandleMultiplayerPortalClick()
        {
            Debug.Log("[PortalController] Portal clicked (multiplayer), submitting level request.");

            var manager = SpecialLevelListManager.Instance;
            if (manager == null)
            {
                manager = FindObjectOfType<SpecialLevelListManager>();
            }

            if (manager == null)
            {
                string msg = "Network system not ready. Please try again.";
                Debug.LogWarning($"[PortalController] SpecialLevelListManager not found. Cannot submit request.");
                OnPortalRequestFeedback?.Invoke(msg);
                return;
            }

            // Get the level data index for this portal
            if (specialLevelData == null)
            {
                string msg = "Portal data not configured. Cannot submit request.";
                Debug.LogWarning($"[PortalController] No SpecialLevelData assigned to this portal (portalId={portalId}).");
                OnPortalRequestFeedback?.Invoke(msg);
                return;
            }

            int levelDataIndex = manager.GetLevelDataIndex(specialLevelData);
            if (levelDataIndex < 0)
            {
                string msg = "Portal level data not registered. Cannot submit request.";
                Debug.LogWarning("[PortalController] SpecialLevelData not registered in SpecialLevelListManager.");
                OnPortalRequestFeedback?.Invoke(msg);
                return;
            }

            // Validate portal has a valid ID
            if (portalId == 0)
            {
                string msg = "Portal not properly initialized. Cannot submit request.";
                Debug.LogWarning("[PortalController] Portal has no valid portalId. Cannot submit request.");
                OnPortalRequestFeedback?.Invoke(msg);
                return;
            }

            // Get the local player's NetworkPlayer netId
            var localPlayer = MirrorNetworkManager.singleton?.LocalPlayer;
            if (localPlayer == null)
            {
                string msg = "Player not connected. Please wait and try again.";
                Debug.LogWarning("[PortalController] Local NetworkPlayer not found. Cannot submit request.");
                OnPortalRequestFeedback?.Invoke(msg);
                return;
            }

            // Submit the request via Command
            manager.CmdRequestAddOption(portalId, localPlayer.netId, levelDataIndex);
            OnPortalRequestFeedback?.Invoke("Challenge request submitted!");
        }

        /// <summary>
        /// Called when the server rejects a portal request.
        /// </summary>
        private void HandleRequestRejected(string reason)
        {
            OnPortalRequestFeedback?.Invoke($"Request rejected: {reason}");
        }

        // =====================================================================
        // Drag Handling (IBeginDragHandler, IDragHandler, IEndDragHandler)
        // =====================================================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragExceededThreshold = false;
            _dragStartPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // Check if drag distance exceeds threshold
            float distance = Vector2.Distance(eventData.position, _dragStartPosition);
            if (distance >= dragThreshold)
            {
                _dragExceededThreshold = true;
            }

            if (!_dragExceededThreshold) return;

            // Move the portal to follow the mouse
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                _canvasCamera,
                out localPoint))
            {
                _rectTransform.localPosition = localPoint;
            }

            // Clamp position within camera visible bounds
            ClampToScreenBounds();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;

            // Reset drag flag after a short delay so the Button OnClick
            // (which fires after OnEndDrag) can check it
            if (_dragExceededThreshold)
            {
                // Use Invoke to reset the flag next frame, after Button.OnClick has been processed
                Invoke(nameof(ResetDragFlag), 0.05f);
            }
        }

        private void ResetDragFlag()
        {
            _dragExceededThreshold = false;
        }

        // =====================================================================
        // Screen Bounds Clamping
        // =====================================================================

        /// <summary>
        /// Clamp the portal's position so it stays within the camera's visible area.
        /// </summary>
        private void ClampToScreenBounds()
        {
            if (_parentCanvas == null) return;

            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            // Get the portal's rect size (half-extents)
            Vector2 portalHalfSize = _rectTransform.rect.size * 0.5f;

            // Get the canvas rect bounds (half-extents)
            Vector2 canvasHalfSize = canvasRect.rect.size * 0.5f;

            // Current local position relative to canvas
            Vector3 localPos = _rectTransform.localPosition;

            // Clamp within canvas bounds, accounting for portal size
            float clampedX = Mathf.Clamp(localPos.x,
                -canvasHalfSize.x + portalHalfSize.x,
                canvasHalfSize.x - portalHalfSize.x);
            float clampedY = Mathf.Clamp(localPos.y,
                -canvasHalfSize.y + portalHalfSize.y,
                canvasHalfSize.y - portalHalfSize.y);

            _rectTransform.localPosition = new Vector3(clampedX, clampedY, localPos.z);
        }

        // =====================================================================
        // Cleanup on Destroy
        // =====================================================================

        /// <summary>
        /// When this portal is destroyed, notify SpecialLevelListManager to remove
        /// any associated option from the list (server-side only).
        /// Also unsubscribes from rejection events.
        /// </summary>
        private void OnDestroy()
        {
            // Unsubscribe from rejection events
            var levelListMgr = SpecialLevelListManager.Instance;
            if (levelListMgr == null)
                levelListMgr = FindObjectOfType<SpecialLevelListManager>();
            if (levelListMgr != null)
            {
                levelListMgr.OnRequestRejected -= HandleRequestRejected;
            }

            // Only run server-side cleanup
            if (!NetworkServer.active) return;

            if (portalId == 0) return;

            var manager = SpecialLevelListManager.Instance;
            if (manager == null)
                manager = FindObjectOfType<SpecialLevelListManager>();

            if (manager != null)
            {
                manager.RemoveOptionsByPortal(portalId);
            }
        }
    }
}
