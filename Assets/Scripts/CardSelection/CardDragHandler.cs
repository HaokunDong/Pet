using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetGame
{
    /// <summary>
    /// Handles mouse drag input to scroll cards along the Spline.
    /// Attach to a transparent UI panel that covers the card interaction area.
    /// Implements IBeginDragHandler, IDragHandler, IEndDragHandler for UI-based drag detection.
    /// </summary>
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("References")]
        [Tooltip("The CardSplineDistributor that manages card positions")]
        [SerializeField] private CardSplineDistributor distributor;

        [Tooltip("The CardFocusDisplay that manages card visuals")]
        [SerializeField] private CardFocusDisplay focusDisplay;

        /// <summary>
        /// Whether the user is currently dragging.
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

        /// <summary>
        /// Event fired when drag ends, passing the final velocity for inertia handling.
        /// </summary>
        public event System.Action<float> OnDragEnded;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (distributor == null || distributor.CardCount <= 1) return;

            IsDragging = true;
            DragVelocity = 0f;
            previousDragOffset = 0f;
            velocitySampleTime = Time.unscaledTime;

            // Notify focus display to suppress animations during drag
            if (focusDisplay != null)
                focusDisplay.NotifyDragStart();

            // Kill any active DOTween scale animations on cards to prevent fighting
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

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            IsDragging = false;

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

            OnDragEnded?.Invoke(DragVelocity);
        }
    }
}
