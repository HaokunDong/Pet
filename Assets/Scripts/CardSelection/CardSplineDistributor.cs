using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace PetGame
{
    /// <summary>
    /// Manages the distribution of character cards along a Spline curve.
    /// Instantiates card prefabs based on CharacterData list and positions them
    /// at evenly-spaced points on the Spline. Provides methods to update card
    /// positions when the global offset changes (e.g. during drag).
    /// </summary>
    public class CardSplineDistributor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The SplineContainer that defines the card layout path")]
        [SerializeField] private SplineContainer splineContainer;

        [Tooltip("Card prefab with CharacterCard component attached")]
        [SerializeField] private GameObject cardPrefab;

        [Tooltip("Parent transform for instantiated cards (should be a UI Canvas or panel)")]
        [SerializeField] private RectTransform cardParent;

        [Header("Data")]
        [Tooltip("List of character data to generate cards for")]
        [SerializeField] private CharacterData[] characterDataList;

        /// <summary>
        /// All instantiated card instances, in order.
        /// </summary>
        private readonly List<CharacterCard> cards = new List<CharacterCard>();

        /// <summary>
        /// The base t-value for each card (before global offset is applied).
        /// </summary>
        private readonly List<float> baseTs = new List<float>();

        /// <summary>
        /// Current global offset applied to all card t-values.
        /// Modified by drag interaction.
        /// </summary>
        private float currentOffset;

        /// <summary>
        /// Index of the card currently closest to the focus position.
        /// </summary>
        public int FocusIndex { get; private set; } = -1;

        /// <summary>
        /// Total number of cards.
        /// </summary>
        public int CardCount => cards.Count;

        /// <summary>
        /// Read-only access to all card instances.
        /// </summary>
        public IReadOnlyList<CharacterCard> Cards => cards;

        /// <summary>
        /// Current global t-offset.
        /// </summary>
        public float CurrentOffset
        {
            get => currentOffset;
            set
            {
                currentOffset = value;
                UpdateCardPositions();
            }
        }

        private CardSelectionSettings Settings => CardSelectionSettings.Instance;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes the card system: instantiates cards and positions them along the Spline.
        /// </summary>
        public void Initialize()
        {
            ClearCards();

            if (splineContainer == null)
            {
                Debug.LogError("[CardSplineDistributor] SplineContainer is not assigned!");
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogError("[CardSplineDistributor] Card prefab is not assigned!");
                return;
            }

            if (characterDataList == null || characterDataList.Length == 0)
            {
                Debug.LogWarning("[CardSplineDistributor] No CharacterData provided. No cards will be generated.");
                return;
            }

            float spacing = Settings.cardSpacing;
            float focusT = Settings.focusT;
            int count = characterDataList.Length;

            // Calculate base t-values so that the middle card sits at focusT
            float totalSpan = (count - 1) * spacing;
            float startT = focusT - totalSpan * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = startT + i * spacing;
                baseTs.Add(t);

                // Instantiate card
                GameObject cardObj = Instantiate(cardPrefab, cardParent != null ? cardParent : transform);
                CharacterCard card = cardObj.GetComponent<CharacterCard>();

                if (card == null)
                {
                    Debug.LogError($"[CardSplineDistributor] Card prefab is missing CharacterCard component!");
                    Destroy(cardObj);
                    continue;
                }

                card.SetData(characterDataList[i]);
                cards.Add(card);
            }

            currentOffset = 0f;
            UpdateCardPositions();
        }

        /// <summary>
        /// Recalculates all card positions using a hand-fan arc layout in the
        /// cardParent's local coordinate space. The fan circle center is placed
        /// below the parent's local origin by fanCenterYOffset, and cards are
        /// distributed on the arc at fanRadius distance from that center.
        /// The focus card receives an additional pop-up offset along its local up direction.
        /// </summary>
        public void UpdateCardPositions()
        {
            if (cards.Count == 0) return;

            float focusT = Settings.focusT;
            float minDist = float.MaxValue;
            int newFocusIndex = 0;

            // Fan layout parameters (all in cardParent local space / UI units)
            float anglePerCard = Settings.fanAnglePerCard;
            float radius = Settings.fanRadius;
            float centerYOffset = Settings.fanCenterYOffset;
            float popUpOffset = Settings.focusPopUpOffset;

            // The fan circle center is below the parent's local origin
            Vector2 fanCenterLocal = new Vector2(0f, -centerYOffset);

            // First pass: determine focus index
            for (int i = 0; i < cards.Count; i++)
            {
                float t = baseTs[i] + currentOffset;
                float dist = Mathf.Abs(t - focusT);
                if (dist < minDist)
                {
                    minDist = dist;
                    newFocusIndex = i;
                }
            }

            bool focusChanged = FocusIndex != newFocusIndex;
            FocusIndex = newFocusIndex;

            // Second pass: position all cards
            for (int i = 0; i < cards.Count; i++)
            {
                float t = baseTs[i] + currentOffset;

                // Calculate how many "card slots" away from focus this card is
                float slotsFromFocus = (t - focusT) / Settings.cardSpacing;

                // Calculate the angle for this card on the fan arc
                float angleDeg = slotsFromFocus * anglePerCard;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Position on the arc in local space:
                float localX = fanCenterLocal.x + radius * Mathf.Sin(angleRad);
                float localY = fanCenterLocal.y + radius * Mathf.Cos(angleRad);

                // Apply pop-up offset for the focus card along its local up direction
                if (i == newFocusIndex && popUpOffset > 0f)
                {
                    Vector2 localUp = new Vector2(-Mathf.Sin(angleRad), Mathf.Cos(angleRad));
                    localX += localUp.x * popUpOffset;
                    localY += localUp.y * popUpOffset;
                }

                // Set card local position within the parent
                RectTransform cardRect = cards[i].GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    cardRect.anchoredPosition = new Vector2(localX, localY);
                }
                else
                {
                    cards[i].transform.localPosition = new Vector3(localX, localY, 0f);
                }

                // Rotate the card to be tangent to the arc
                cards[i].transform.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);

                // Performance optimization: hide cards that are far outside the visible range
                bool isVisible = Mathf.Abs(slotsFromFocus) <= cards.Count;
                cards[i].gameObject.SetActive(isVisible);
            }

            // Notify focus system to update scale and sorting
            if (focusChanged)
            {
                OnFocusChanged?.Invoke(FocusIndex);
            }
        }

        /// <summary>
        /// Returns the base anchored position for a card on the fan arc (without any pop-up offset).
        /// Used by CardFocusDisplay to calculate the pop-up target position.
        /// </summary>
        public Vector2 GetCardBaseAnchoredPosition(int index)
        {
            if (index < 0 || index >= baseTs.Count) return Vector2.zero;

            float t = baseTs[index] + currentOffset;
            float slotsFromFocus = (t - Settings.focusT) / Settings.cardSpacing;
            float angleDeg = slotsFromFocus * Settings.fanAnglePerCard;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float radius = Settings.fanRadius;
            float centerYOffset = Settings.fanCenterYOffset;
            Vector2 fanCenterLocal = new Vector2(0f, -centerYOffset);

            float localX = fanCenterLocal.x + radius * Mathf.Sin(angleRad);
            float localY = fanCenterLocal.y + radius * Mathf.Cos(angleRad);
            return new Vector2(localX, localY);
        }

        /// <summary>
        /// Returns the t-value offset needed to snap the given card index to the focus position.
        /// </summary>
        public float GetSnapOffset(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= baseTs.Count) return 0f;
            float focusT = Settings.focusT;
            float currentT = baseTs[cardIndex] + currentOffset;
            return focusT - currentT;
        }

        /// <summary>
        /// Returns the t-value offset needed to snap the current focus card to the focus position.
        /// </summary>
        public float GetSnapOffsetForFocus()
        {
            return GetSnapOffset(FocusIndex);
        }

        /// <summary>
        /// Returns the actual t-value of a card (base + offset).
        /// </summary>
        public float GetCardT(int index)
        {
            if (index < 0 || index >= baseTs.Count) return 0f;
            return baseTs[index] + currentOffset;
        }

        /// <summary>
        /// Clamps the current offset so that at least one card remains within the valid Spline range.
        /// </summary>
        public void ClampOffset()
        {
            if (cards.Count == 0) return;

            float focusT = Settings.focusT;

            // The first card's t should not go beyond focusT (can't scroll past the first card)
            float maxOffset = focusT - baseTs[0];
            // The last card's t should not go below focusT (can't scroll past the last card)
            float minOffset = focusT - baseTs[baseTs.Count - 1];

            currentOffset = Mathf.Clamp(currentOffset, minOffset, maxOffset);
        }

        /// <summary>
        /// Destroys all instantiated cards and resets state.
        /// </summary>
        public void ClearCards()
        {
            foreach (var card in cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            cards.Clear();
            baseTs.Clear();
            currentOffset = 0f;
            FocusIndex = -1;
        }

        /// <summary>
        /// Event fired when the focus card index changes.
        /// </summary>
        public event System.Action<int> OnFocusChanged;

        private void OnDestroy()
        {
            ClearCards();
        }
    }
}
