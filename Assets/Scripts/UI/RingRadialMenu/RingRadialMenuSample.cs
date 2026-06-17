using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Sample driver for <see cref="RingRadialMenu"/>.
    /// Demonstrates how to:
    ///   - assign a world-space center target at runtime
    ///   - register click callbacks for each of the 8 buttons
    ///   - subscribe to the parent menu's index-aware OnButtonClicked event
    ///   - subscribe to the OnRotated event (driven by mouse wheel)
    ///   - toggle scroll-rotation on/off at runtime
    ///   - swap icons dynamically
    ///
    /// Attach this component to the same GameObject as a <see cref="RingRadialMenu"/>,
    /// or set the <see cref="menu"/> reference manually in the inspector.
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Radial Menu Sample")]
    public class RingRadialMenuSample : MonoBehaviour
    {
        [Tooltip("The ring menu to drive. Auto-resolved from this GameObject if left null.")]
        [SerializeField] private RingRadialMenu menu;

        [Tooltip("World-space target for the menu to orbit. May be null.")]
        [SerializeField] private Transform centerTarget;

        [Tooltip("Optional camera for world-to-screen projection. Falls back to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Optional default icons applied to buttons 0..N-1 in order.")]
        [SerializeField] private Sprite[] icons;

        [Tooltip("Press this key at runtime to toggle scroll-driven rotation on/off.")]
        [SerializeField] private KeyCode toggleScrollRotateKey = KeyCode.R;

        private void Awake()
        {
            if (menu == null) menu = GetComponent<RingRadialMenu>();
        }

        private void Start()
        {
            if (menu == null)
            {
                Debug.LogWarning("[RingRadialMenuSample] No RingRadialMenu reference; sample disabled.", this);
                enabled = false;
                return;
            }

            menu.SetCenterTarget(centerTarget);
            if (worldCamera != null) menu.SetWorldCamera(worldCamera);

            // Apply default icons (if provided).
            if (icons != null)
            {
                for (int i = 0; i < icons.Length; ++i)
                {
                    menu.SetButtonIcon(i, icons[i]);
                }
            }

            // Per-button callbacks.
            for (int i = 0; i < menu.ButtonCount; ++i)
            {
                int captured = i; // capture by value
                menu.SetButtonCallback(captured, () => OnButtonInvoked(captured));
            }

            // Aggregate event.
            menu.OnButtonClicked += HandleAggregateClick;

            // Scroll-driven rotation event.
            menu.OnRotated += HandleRotated;
        }

        private void Update()
        {
            if (menu == null) return;
            if (Input.GetKeyDown(toggleScrollRotateKey))
            {
                bool next = !menu.IsScrollRotateEnabled;
                menu.SetScrollRotateEnabled(next);
                Debug.Log($"[RingRadialMenuSample] Scroll-rotate toggled: {next}");
            }
        }

        private void OnDestroy()
        {
            if (menu != null)
            {
                menu.OnButtonClicked -= HandleAggregateClick;
                menu.OnRotated -= HandleRotated;
            }
        }

        // -----------------------------------------------------------------
        // Sample handlers
        // -----------------------------------------------------------------

        private void OnButtonInvoked(int index)
        {
            Debug.Log($"[RingRadialMenuSample] Per-button callback invoked: index={index}");
        }

        private void HandleAggregateClick(int index)
        {
            Debug.Log($"[RingRadialMenuSample] Aggregate OnButtonClicked: index={index}");
        }

        private void HandleRotated(float accumulatedDegrees)
        {
            Debug.Log($"[RingRadialMenuSample] OnRotated: accumulated={accumulatedDegrees:F1}°");
        }
    }
}
