using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetGame
{
    /// <summary>
    /// Handles mouse drag input on individual cards to scroll all cards along the Spline.
    /// Also detects click (non-drag) on the focus card to trigger character switching.
    /// Attach to each card prefab (or added dynamically at runtime).
    /// Implements IBeginDragHandler, IDragHandler, IEndDragHandler for UI-based drag detection.
    /// Implements IPointerDownHandler, IPointerUpHandler for click detection.
    /// Requires a Graphic component (e.g. Image) with Raycast Target enabled on the card.
    /// </summary>
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>
        /// The CardSplineDistributor that manages card positions. Injected via Initialize().
        /// </summary>
        private CardSplineDistributor distributor;

        /// <summary>
        /// The CardFocusDisplay that manages card visuals. Injected via Initialize().
        /// </summary>
        private CardFocusDisplay focusDisplay;

        /// <summary>
        /// The CardInertiaAndSnap used to check animation state. Injected via Initialize().
        /// </summary>
        private CardInertiaAndSnap inertiaAndSnap;

        /// <summary>
        /// Whether this handler has been properly initialized with references.
        /// </summary>
        private bool isInitialized;

        /// <summary>
        /// Whether the user is currently dragging via this card.
        /// </summary>
        public bool IsDragging { get; private set; }

        /// <summary>
        /// Current drag velocity (in t-value per second), used for inertia calculation.
        /// </summary>
        public float DragVelocity { get; private set; }

        private CardSelectionSettings Settings => CardSelectionSettings.Instance;

        // Velocity tracking
        private float previousDragOffset;
        private float velocitySampleTime;
        private const int VelocitySampleCount = 5;
        private readonly float[] velocitySamples = new float[VelocitySampleCount];
        private int velocitySampleIndex;

        // Click detection fields
        /// <summary>
        /// Whether a drag was initiated during the current pointer press.
        /// </summary>
        private bool dragInitiated;

        /// <summary>
        /// Screen position where the pointer was pressed down.
        /// </summary>
        private Vector2 pointerDownPosition;

        /// <summary>
        /// Drag distance threshold (in pixels) below which a pointer interaction is treated as a click.
        /// </summary>
        private const float ClickDistanceThreshold = 10f;

        /// <summary>
        /// Initializes this drag handler with the required references.
        /// Called by CardSplineDistributor after instantiating the card.
        /// </summary>
        public void Initialize(CardSplineDistributor distributor, CardFocusDisplay focusDisplay, CardInertiaAndSnap inertiaAndSnap)
        {
            this.distributor = distributor;
            this.focusDisplay = focusDisplay;
            this.inertiaAndSnap = inertiaAndSnap;
            isInitialized = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragInitiated = false;
            pointerDownPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // If a drag was initiated, this is not a click
            if (dragInitiated) return;

            // Check pointer travel distance to distinguish click from micro-drag
            float distance = Vector2.Distance(pointerDownPosition, eventData.position);
            if (distance > ClickDistanceThreshold) return;

            // Only respond when the card group is visible
            if (!isInitialized || distributor == null || !distributor.IsVisible) return;

            // Don't respond during inertia/snap animation
            if (inertiaAndSnap != null && inertiaAndSnap.IsAnimating) return;

            // Only the focus card can be clicked to trigger character switch
            int myIndex = distributor.GetCardIndex(GetComponent<CharacterCard>());
            if (myIndex < 0 || myIndex != distributor.FocusIndex) return;

            // Get the CharacterData from this card
            CharacterCard card = GetComponent<CharacterCard>();
            if (card == null || card.Data == null) return;

            // Play click feedback animation (scale punch) then trigger character switch
            CharacterData clickedData = card.Data;
            DOTween.Kill("clickFeedback");
            transform.DOPunchScale(Vector3.one * -0.1f, 0.2f, 6, 0.5f)
                .SetId("clickFeedback")
                .OnComplete(() =>
                {
                    if (distributor != null)
                        distributor.NotifyFocusCardClicked(clickedData);
                });
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isInitialized || distributor == null || distributor.CardCount <= 1) return;

            dragInitiated = true;
            IsDragging = true;
            distributor.IsDragging = true;
            DragVelocity = 0f;
            previousDragOffset = 0f;
            velocitySampleTime = Time.unscaledTime;

            // Notify focus display to suppress animations during drag
            if (focusDisplay != null)
                focusDisplay.NotifyDragStart();

            // Kill any active DOTween animations on cards to prevent fighting
            for (int i = 0; i < distributor.CardCount; i++)
            {
                CharacterCard card = distributor.Cards[i];
                if (card != null)
                {
                    DOTween.Kill(card.transform);
                }
            }

            // Kill any active snap/inertia tween on the whole system
            DOTween.Kill(distributor);

            // Reset velocity samples
            for (int i = 0; i < VelocitySampleCount; i++)
                velocitySamples[i] = 0f;
            velocitySampleIndex = 0;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || distributor == null) return;

            // Convert horizontal mouse delta to Spline t-value offset
            float deltaX = eventData.delta.x;
            float tDelta = deltaX * Settings.dragSensitivity;

            // Apply offset
            distributor.CurrentOffset += tDelta;
            distributor.ClampOffset();

            // Track velocity
            float now = Time.unscaledTime;
            float dt = now - velocitySampleTime;
            if (dt > 0f)
            {
                float velocity = tDelta / dt;
                velocitySamples[velocitySampleIndex % VelocitySampleCount] = velocity;
                velocitySampleIndex++;
                velocitySampleTime = now;
            }

            // Update visuals during drag (immediate, no animation)
            if (focusDisplay != null)
            {
                focusDisplay.RefreshVisualsImmediate();
            }
        }

        /// <summary>
        /// Forcefully cancels any active drag operation without triggering inertia.
        /// Called externally when the card group is being hidden during a drag.
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging) return;

            IsDragging = false;
            DragVelocity = 0f;

            if (distributor != null)
                distributor.IsDragging = false;

            if (focusDisplay != null)
                focusDisplay.NotifyDragEnd();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            IsDragging = false;
            if (distributor != null)
                distributor.IsDragging = false;

            // Notify focus display that drag ended
            if (focusDisplay != null)
                focusDisplay.NotifyDragEnd();

            // Calculate average velocity from samples
            float totalVelocity = 0f;
            int sampleCount = Mathf.Min(velocitySampleIndex, VelocitySampleCount);
            if (sampleCount > 0)
            {
                for (int i = 0; i < sampleCount; i++)
                    totalVelocity += velocitySamples[i];
                DragVelocity = totalVelocity / sampleCount;
            }
            else
            {
                DragVelocity = 0f;
            }

            // Forward drag-ended event through the distributor for centralized handling
            if (distributor != null)
                distributor.NotifyDragEnded(DragVelocity);
        }
    }
}
