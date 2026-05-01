using DG.Tweening;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages the visual focus effect for cards: adjusts sibling order for proper layering
    /// and handles the focus card pop-up offset.
    /// Attach to the same GameObject as CardSplineDistributor.
    /// </summary>
    [RequireComponent(typeof(CardSplineDistributor))]
    public class CardFocusDisplay : MonoBehaviour
    {
        private CardSplineDistributor distributor;

        /// <summary>
        /// The index of the card that was last visually focused.
        /// </summary>
        private int lastVisualFocusIndex = -1;

        /// <summary>
        /// Whether the user is currently dragging. When true, focus change
        /// animations are suppressed to avoid fighting with direct position updates.
        /// </summary>
        private bool isDragging;

        private void Awake()
        {
            distributor = GetComponent<CardSplineDistributor>();
        }

        private void OnEnable()
        {
            distributor.OnFocusChanged += HandleFocusChanged;
        }

        private void OnDisable()
        {
            distributor.OnFocusChanged -= HandleFocusChanged;
        }

        /// <summary>
        /// Notifies the focus display that dragging has started.
        /// Focus scale animations remain active during drag.
        /// </summary>
        public void NotifyDragStart()
        {
            isDragging = true;
        }

        /// <summary>
        /// Notifies the focus display that dragging has ended.
        /// Re-applies the focus scale animation to the current focus card.
        /// </summary>
        public void NotifyDragEnd()
        {
            isDragging = false;

            // Re-apply focus scale to the current focus card after drag ends
            int focusIndex = distributor.FocusIndex;
            if (focusIndex >= 0)
            {
                lastVisualFocusIndex = focusIndex;
                ApplyFocusScale(focusIndex, -1, animate: true);
            }
        }

        /// <summary>
        /// Called whenever the distributor detects a new focus card index.
        /// Always updates sibling order and applies focus scale animation,
        /// including during drag.
        /// </summary>
        private void HandleFocusChanged(int newFocusIndex)
        {
            UpdateVisuals(newFocusIndex, animate: true);
        }

        /// <summary>
        /// Updates all card visuals based on the current focus.
        /// Position and rotation are always driven by CardSplineDistributor.UpdateCardPositions().
        /// The focus card gets a scale-up animation; other cards stay at normal scale.
        /// </summary>
        public void UpdateVisuals(int focusIndex, bool animate)
        {
            if (distributor.CardCount == 0) return;

            int previousFocus = lastVisualFocusIndex;
            lastVisualFocusIndex = focusIndex;

            UpdateSiblingOrder(focusIndex);
            ApplyFocusScale(focusIndex, previousFocus, animate);
        }

        /// <summary>
        /// Immediately refreshes sibling order without animation (used during drag/inertia).
        /// Position and rotation are handled by UpdateCardPositions.
        /// Scale is NOT modified during drag to avoid jitter.
        /// </summary>
        public void RefreshVisualsImmediate()
        {
            int focusIndex = distributor.FocusIndex;
            if (focusIndex < 0) return;

            UpdateSiblingOrder(focusIndex);
        }

        /// <summary>
        /// Applies scale effect to the focus card and resets the previous focus card.
        /// Only the focus card is scaled up; all other cards remain at Vector3.one.
        /// </summary>
        private void ApplyFocusScale(int focusIndex, int previousFocusIndex, bool animate)
        {
            var settings = CardSelectionSettings.Instance;
            float targetScale = settings.focusScale;
            float duration = settings.focusScaleDuration;
            Ease ease = settings.focusScaleEase;

            // Reset previous focus card to normal scale
            if (previousFocusIndex >= 0 && previousFocusIndex < distributor.CardCount && previousFocusIndex != focusIndex)
            {
                CharacterCard prevCard = distributor.Cards[previousFocusIndex];
                if (prevCard != null)
                {
                    prevCard.transform.DOKill(complete: false);
                    if (animate)
                        prevCard.transform.DOScale(Vector3.one, duration).SetEase(ease);
                    else
                        prevCard.transform.localScale = Vector3.one;
                }
            }

            // Scale up the new focus card
            if (focusIndex >= 0 && focusIndex < distributor.CardCount)
            {
                CharacterCard focusCard = distributor.Cards[focusIndex];
                if (focusCard != null)
                {
                    focusCard.transform.DOKill(complete: false);
                    Vector3 target = Vector3.one * targetScale;
                    if (animate)
                        focusCard.transform.DOScale(target, duration).SetEase(ease);
                    else
                        focusCard.transform.localScale = target;
                }
            }
        }

        /// <summary>
        /// Adjusts the sibling index of each card so that cards are layered
        /// from left to right, but the focus card is always on top of the pile.
        /// </summary>
        private void UpdateSiblingOrder(int focusIndex)
        {
            int count = distributor.CardCount;
            if (count == 0) return;

            // Sort cards by their current t-value (left to right on the arc).
            // Lower t-value = further left = lower sibling index (rendered behind).
            var sortedIndices = new int[count];
            var tValues = new float[count];

            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
                tValues[i] = distributor.GetCardT(i);
            }

            System.Array.Sort(tValues, sortedIndices);

            // Assign sibling indices: left-to-right order, but skip the focus card first.
            // Then place the focus card at the very top (highest sibling index).
            int siblingIdx = 0;
            for (int s = 0; s < count; s++)
            {
                int cardIdx = sortedIndices[s];
                if (cardIdx == focusIndex) continue; // skip focus card for now

                CharacterCard card = distributor.Cards[cardIdx];
                if (card != null)
                {
                    card.transform.SetSiblingIndex(siblingIdx);
                    siblingIdx++;
                }
            }

            // Focus card always on top
            if (focusIndex >= 0 && focusIndex < count)
            {
                CharacterCard focusCard = distributor.Cards[focusIndex];
                if (focusCard != null)
                {
                    focusCard.transform.SetSiblingIndex(count - 1);
                }
            }
        }

        private void OnDestroy()
        {
            // Kill any active scale tweens on cards
            if (distributor != null)
            {
                for (int i = 0; i < distributor.CardCount; i++)
                {
                    CharacterCard card = distributor.Cards[i];
                    if (card != null)
                        card.transform.DOKill();
                }
            }
        }
    }
}
