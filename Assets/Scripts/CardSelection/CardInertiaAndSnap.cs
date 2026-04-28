using DG.Tweening;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Handles inertia scrolling after drag release and snap-to-focus animation.
    /// Listens to CardDragHandler.OnDragEnded to receive the release velocity,
    /// then applies inertia deceleration followed by a DOTween snap animation
    /// to align the nearest card to the focus position.
    /// </summary>
    public class CardInertiaAndSnap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardSplineDistributor distributor;
        [SerializeField] private CardDragHandler dragHandler;
        [SerializeField] private CardFocusDisplay focusDisplay;

        private CardSelectionSettings Settings => CardSelectionSettings.Instance;

        /// <summary>
        /// Whether inertia scrolling is currently active.
        /// </summary>
        private bool isInertiaActive;

        /// <summary>
        /// Current inertia velocity (t-value per second).
        /// </summary>
        private float inertiaVelocity;

        /// <summary>
        /// The DOTween snap tween (if active).
        /// </summary>
        private Tween snapTween;

        private void OnEnable()
        {
            if (dragHandler != null)
                dragHandler.OnDragEnded += HandleDragEnded;
        }

        private void OnDisable()
        {
            if (dragHandler != null)
                dragHandler.OnDragEnded -= HandleDragEnded;
        }

        private void Update()
        {
            if (!isInertiaActive) return;

            // If user starts dragging again, cancel inertia
            if (dragHandler != null && dragHandler.IsDragging)
            {
                CancelInertia();
                return;
            }

            // Apply inertia deceleration
            float dt = Time.deltaTime;
            float damping = Settings.inertiaDamping;

            // Exponential decay
            inertiaVelocity *= Mathf.Exp(-damping * dt);

            // Apply velocity to offset
            distributor.CurrentOffset += inertiaVelocity * dt;
            distributor.ClampOffset();

            // Update visuals during inertia
            if (focusDisplay != null)
                focusDisplay.RefreshVisualsImmediate();

            // Check if velocity is below threshold
            if (Mathf.Abs(inertiaVelocity) < Settings.inertiaMinVelocity)
            {
                isInertiaActive = false;
                SnapToFocus();
            }
        }

        /// <summary>
        /// Called when drag ends. Starts inertia if velocity is significant,
        /// otherwise snaps immediately.
        /// </summary>
        private void HandleDragEnded(float velocity)
        {
            // Kill any existing snap tween
            KillSnapTween();

            // Keep focus display in "dragging" mode during inertia + snap
            // to prevent animated focus transitions from fighting with position updates
            if (focusDisplay != null)
                focusDisplay.NotifyDragStart();

            if (Mathf.Abs(velocity) > Settings.inertiaMinVelocity)
            {
                // Start inertia
                inertiaVelocity = velocity;
                isInertiaActive = true;
            }
            else
            {
                // No significant velocity, snap immediately
                SnapToFocus();
            }
        }

        /// <summary>
        /// Animates the cards to snap the current focus card to the exact focus position.
        /// Uses DOTween for smooth animation.
        /// </summary>
        private void SnapToFocus()
        {
            if (distributor == null || distributor.CardCount == 0) return;

            KillSnapTween();

            float snapDelta = distributor.GetSnapOffsetForFocus();

            if (Mathf.Abs(snapDelta) < 0.0001f)
            {
                // Already at focus, just finalize visuals
                FinalizeSnap();
                return;
            }

            float startOffset = distributor.CurrentOffset;
            float targetOffset = startOffset + snapDelta;

            snapTween = DOTween.To(
                () => distributor.CurrentOffset,
                x =>
                {
                    distributor.CurrentOffset = x;
                    distributor.ClampOffset();
                    if (focusDisplay != null)
                        focusDisplay.RefreshVisualsImmediate();
                },
                targetOffset,
                Settings.snapDuration
            )
            .SetEase(Settings.snapEase)
            .SetTarget(this)
            .OnComplete(FinalizeSnap);
        }

        /// <summary>
        /// Called when snap animation completes. Triggers the final focus display update with animation.
        /// </summary>
        private void FinalizeSnap()
        {
            // End the "dragging" suppression mode
            if (focusDisplay != null)
            {
                focusDisplay.NotifyDragEnd();

                if (distributor.FocusIndex >= 0)
                {
                    focusDisplay.UpdateVisuals(distributor.FocusIndex, true);
                }
            }
        }

        /// <summary>
        /// Cancels any active inertia scrolling.
        /// </summary>
        private void CancelInertia()
        {
            isInertiaActive = false;
            inertiaVelocity = 0f;
            KillSnapTween();
        }

        /// <summary>
        /// Kills the active snap tween if any.
        /// </summary>
        private void KillSnapTween()
        {
            if (snapTween != null && snapTween.IsActive())
            {
                snapTween.Kill();
                snapTween = null;
            }
        }

        private void OnDestroy()
        {
            KillSnapTween();
            DOTween.Kill(this);
        }
    }
}
