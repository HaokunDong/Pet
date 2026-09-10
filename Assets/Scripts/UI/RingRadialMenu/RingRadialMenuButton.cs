using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// A single button cell on a <see cref="RingRadialMenu"/>.
    /// Wraps a <see cref="RingSectorGraphic"/> background (the clickable hit area)
    /// and an optional icon <see cref="Image"/>.
    /// Click events are dispatched by the child <see cref="RingSectorGraphic"/> directly;
    /// this component acts as a logical container and provides convenience API for
    /// setting icons and callbacks.
    ///
    /// <para>
    /// Supports three visual states: <see cref="ButtonState.Idle"/>,
    /// <see cref="ButtonState.Suspended"/> (hover), and <see cref="ButtonState.Selected"/>.
    /// Clicking the sector toggles between Idle/Suspended and Selected.
    /// Multiple buttons on the same menu can be Selected simultaneously.
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Radial Menu Button")]
    [RequireComponent(typeof(RectTransform))]
    public class RingRadialMenuButton : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Button state
        // -----------------------------------------------------------------

        /// <summary>
        /// Visual/logical states for a ring radial menu button.
        /// </summary>
        public enum ButtonState
        {
            /// <summary>Default resting state.</summary>
            Idle,
            /// <summary>Mouse is hovering over the sector (pointer enter).</summary>
            Suspended,
            /// <summary>Button has been clicked and is "active" / toggled on.</summary>
            Selected
        }

        /// <summary>
        /// Current state of this button.
        /// </summary>
        public ButtonState State { get; private set; } = ButtonState.Idle;

        /// <summary>
        /// Fired whenever the button state changes.
        /// Arguments: (buttonIndex, oldState, newState).
        /// </summary>
        public event Action<int, ButtonState, ButtonState> OnStateChanged;

        // -----------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------

        [Tooltip("The sector-shaped raycast graphic that defines this button's hit area.")]
        [SerializeField] private RingSectorGraphic sectorGraphic;

        [Tooltip("Optional icon image displayed at the center of the button cell.")]
        [SerializeField] private Image iconImage;

        [Header("Icon State Sprites")]
        [Tooltip("Icon sprite displayed when the button is in Idle state.")]
        [SerializeField] private Sprite idleSprite;

        [Tooltip("Icon sprite displayed when the pointer hovers over the sector (Suspended state).")]
        [SerializeField] private Sprite suspendedSprite;

        [Tooltip("Icon sprite displayed when the button is toggled on (Selected state).")]
        [SerializeField] private Sprite selectedSprite;

        /// <summary>
        /// Index of this button within the parent <see cref="RingRadialMenu"/>.
        /// Assigned at layout-time by the parent menu.
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Cached reference to this button's RectTransform.
        /// </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null) cachedRect = (RectTransform)transform;
                return cachedRect;
            }
        }
        private RectTransform cachedRect;

        /// <summary>
        /// The clickable sector graphic. Auto-resolved on first access.
        /// </summary>
        public RingSectorGraphic SectorGraphic
        {
            get
            {
                if (sectorGraphic == null) sectorGraphic = GetComponentInChildren<RingSectorGraphic>(true);
                return sectorGraphic;
            }
        }

        /// <summary>
        /// Icon image. Auto-resolved on first access.
        /// </summary>
        public Image IconImage => iconImage;

        private void Awake()
        {
            ApplyVisualState(State);
        }

        private void OnEnable()
        {
            ApplyVisualState(State);
        }

        /// <summary>
        /// Initializes index and parent dispatcher. Called by RingRadialMenu.
        /// Delegates click handling to the child <see cref="RingSectorGraphic"/>.
        /// </summary>
        public void Initialize(int index, System.Action<int> dispatcher)
        {
            Index = index;
            // Delegate click events to the sector graphic.
            if (SectorGraphic != null)
            {
                SectorGraphic.InitializeClick(index, dispatcher);
                SectorGraphic.OwnerButton = this;
            }

            ApplyVisualState(State);
        }

        // -----------------------------------------------------------------
        // State management
        // -----------------------------------------------------------------

        /// <summary>
        /// Transitions the button to the given state, updates the sector sprite,
        /// and fires <see cref="OnStateChanged"/>.
        /// If the new state equals the current state, no event is fired.
        /// </summary>
        public void SetState(ButtonState newState)
        {
            if (State == newState) return;
            ButtonState old = State;
            State = newState;
            ApplyVisualState(newState);
            try { OnStateChanged?.Invoke(Index, old, newState); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void ApplyVisualState(ButtonState state)
        {
            if (SectorGraphic != null) SectorGraphic.ApplyStateSprite(state);
            ApplyIconStateSprite(state);
        }

        /// <summary>
        /// Applies the appropriate icon sprite based on the given button state.
        /// If the sprite for the given state is null, the current icon sprite is left unchanged.
        /// </summary>
        private void ApplyIconStateSprite(ButtonState state)
        {
            if (iconImage == null)
            {
                return;
            }

            Sprite target = state switch
            {
                ButtonState.Idle      => idleSprite,
                ButtonState.Suspended => suspendedSprite,
                ButtonState.Selected  => selectedSprite,
                _                     => null
            };

            if (target != null)
            {
                iconImage.sprite = target;
                iconImage.enabled = true;
            }
        }

        /// <summary>
        /// Toggles between Selected and the appropriate non-selected state.
        /// If currently Selected, transitions to Idle (or Suspended if the pointer
        /// is still hovering — the caller can pass <paramref name="isHovering"/> to hint).
        /// If currently Idle or Suspended, transitions to Selected.
        /// </summary>
        /// <param name="isHovering">True if the pointer is currently over this button's sector.</param>
        public void ToggleSelected(bool isHovering = false)
        {
            if (State == ButtonState.Selected)
            {
                SetState(isHovering ? ButtonState.Suspended : ButtonState.Idle);
            }
            else
            {
                SetState(ButtonState.Selected);
            }
        }

        /// <summary>
        /// Forces the button back to Idle state. Convenience method for external callers
        /// (e.g. when the associated panel/page is closed).
        /// </summary>
        public void Deselect()
        {
            if (State == ButtonState.Selected)
            {
                SetState(ButtonState.Idle);
            }
        }

        /// <summary>
        /// Called by <see cref="RingSectorGraphic"/> when the pointer enters the sector.
        /// Transitions to Suspended unless the button is already Selected.
        /// </summary>
        internal void NotifyPointerEnter()
        {
            if (State == ButtonState.Idle)
            {
                SetState(ButtonState.Suspended);
            }
        }

        /// <summary>
        /// Called by <see cref="RingSectorGraphic"/> when the pointer exits the sector.
        /// Transitions to Idle unless the button is Selected.
        /// </summary>
        internal void NotifyPointerExit()
        {
            if (State == ButtonState.Suspended)
            {
                SetState(ButtonState.Idle);
            }
        }

        /// <summary>
        /// Sets the icon sprite. Pass null to hide the icon.
        /// </summary>
        public void SetIcon(Sprite sprite)
        {
            if (iconImage == null)
            {
                return;
            }
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        /// <summary>
        /// Registers (or replaces) the per-button click callback.
        /// Delegates to the child <see cref="RingSectorGraphic"/>.
        /// </summary>
        public void SetCallback(UnityAction callback)
        {
            if (SectorGraphic != null)
            {
                SectorGraphic.SetCallback(callback);
            }
        }

        /// <summary>
        /// Removes the per-button click callback.
        /// Delegates to the child <see cref="RingSectorGraphic"/>.
        /// </summary>
        public void ClearCallback()
        {
            if (SectorGraphic != null)
            {
                SectorGraphic.ClearCallback();
            }
        }

        /// <summary>
        /// Whether this button is currently in the Selected state.
        /// </summary>
        public bool IsSelected => State == ButtonState.Selected;
    }
}
