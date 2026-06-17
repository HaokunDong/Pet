using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// A single button cell on a <see cref="RingRadialMenu"/>.
    /// Wraps a <see cref="RingSectorGraphic"/> background (the clickable hit area)
    /// and an optional icon <see cref="Image"/>.
    /// Click events are dispatched both via a per-button <see cref="UnityAction"/> callback
    /// and via the parent menu's <c>OnButtonClicked(int)</c> event.
    /// </summary>
    [AddComponentMenu("UI/Ring Radial Menu/Ring Radial Menu Button")]
    [RequireComponent(typeof(RectTransform))]
    public class RingRadialMenuButton : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("The sector-shaped raycast graphic that defines this button's hit area.")]
        [SerializeField] private RingSectorGraphic sectorGraphic;

        [Tooltip("Optional icon image displayed at the center of the button cell.")]
        [SerializeField] private Image iconImage;

        /// <summary>
        /// Index of this button within the parent <see cref="RingRadialMenu"/>.
        /// Assigned at layout-time by the parent menu.
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Per-button click callback, registered via <see cref="SetCallback"/>.
        /// </summary>
        private UnityAction onClickCallback;

        /// <summary>
        /// Optional parent-level callback that also receives the button index.
        /// Set by <see cref="RingRadialMenu"/> when it builds buttons.
        /// </summary>
        private System.Action<int> onClickWithIndex;

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

        /// <summary>
        /// Initializes index and parent dispatcher. Called by RingRadialMenu.
        /// </summary>
        public void Initialize(int index, System.Action<int> dispatcher)
        {
            Index = index;
            onClickWithIndex = dispatcher;
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
        /// </summary>
        public void SetCallback(UnityAction callback)
        {
            onClickCallback = callback;
        }

        /// <summary>
        /// Removes the per-button click callback.
        /// </summary>
        public void ClearCallback()
        {
            onClickCallback = null;
        }

        /// <summary>
        /// IPointerClickHandler entry. Triggered only when <see cref="RingSectorGraphic.IsRaycastLocationValid"/>
        /// has already validated the hit, so any click here is guaranteed to be inside the sector.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            try
            {
                onClickCallback?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
            }

            try
            {
                onClickWithIndex?.Invoke(Index);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
