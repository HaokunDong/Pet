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
        /// Suppresses animated focus transitions during drag.
        /// </summary>
        public void NotifyDragStart()
        {
            isDragging = true;
        }

        /// <summary>
        /// Notifies the focus display that dragging has ended.
        /// </summary>
        public void NotifyDragEnd()
        {
            isDragging = false;
        }

        /// <summary>
        /// Called whenever the distributor detects a new focus card index.
        /// During drag, only updates sibling order (no animation).
        /// </summary>
        private void HandleFocusChanged(int newFocusIndex)
        {
            if (isDragging)
            {
                // During drag, only update sibling order, no animations
                lastVisualFocusIndex = newFocusIndex;
                UpdateSiblingOrder(newFocusIndex);
                return;
            }
            UpdateVisuals(newFocusIndex, animate: true);
        }

        /// <summary>
        /// Updates all card visuals (sorting only) based on the current focus.
        /// Position and rotation are always driven by CardSplineDistributor.UpdateCardPositions().
        /// Cards are never scaled — they always remain at their original size.
        /// </summary>
        public void UpdateVisuals(int focusIndex, bool animate)
        {
            if (distributor.CardCount == 0) return;

            lastVisualFocusIndex = focusIndex;

            // Update sibling order only — no scale changes
            UpdateSiblingOrder(focusIndex);
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
            // No cleanup needed since we no longer use DOTween in this class
        }
    }
}
