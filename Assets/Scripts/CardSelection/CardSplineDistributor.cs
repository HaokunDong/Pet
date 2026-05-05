using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using DG.Tweening;

namespace PetGame
{
    /// <summary>
    /// Manages the distribution of character cards along a Spline curve.
    /// Instantiates card prefabs based on CharacterData list and positions them
    /// at evenly-spaced points on the Spline. Uses SplineUtility to sample
    /// real positions and tangents from the SplineContainer.
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
        /// Public read-only access to the character data list.
        /// Used by BossFightManager to check if a CharacterData already exists.
        /// </summary>
        public CharacterData[] CharacterDataList => characterDataList;

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

        /// <summary>
        /// Cached reference to the first spline in the SplineContainer.
        /// </summary>
        private Spline spline;

        /// <summary>
        /// Cached reference to the CardFocusDisplay on the same GameObject.
        /// </summary>
        private CardFocusDisplay focusDisplay;

        /// <summary>
        /// Cached reference to the CardInertiaAndSnap on the same GameObject.
        /// </summary>
        private CardInertiaAndSnap inertiaAndSnap;

        /// <summary>
        /// Cached reference to the GameCharacterManager for character switching.
        /// </summary>
        private GameCharacterManager gameCharacterManager;

        /// <summary>
        /// Whether the card group is currently visible.
        /// </summary>
        public bool IsVisible { get; private set; }

        private void Start()
        {
            Initialize();
            Hide();
        }

        /// <summary>
        /// Initializes the card system: instantiates cards, adds CardDragHandler to each,
        /// and positions them along the Spline.
        /// </summary>
        public void Initialize()
        {
            ClearCards();

            if (splineContainer == null)
            {
                Debug.LogError("[CardSplineDistributor] SplineContainer is not assigned!");
                return;
            }

            spline = splineContainer.Spline;
            if (spline == null)
            {
                Debug.LogError("[CardSplineDistributor] SplineContainer has no spline!");
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

            // Cache the focus display reference for drag handler injection
            focusDisplay = GetComponent<CardFocusDisplay>();
            inertiaAndSnap = GetComponent<CardInertiaAndSnap>();

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

                // Add CardDragHandler to each card and inject references
                CardDragHandler dragHandler = cardObj.GetComponent<CardDragHandler>();
                if (dragHandler == null)
                    dragHandler = cardObj.AddComponent<CardDragHandler>();
                dragHandler.Initialize(this, focusDisplay, inertiaAndSnap);
            }

            currentOffset = 0f;
            UpdateCardPositions();
        }

        /// <summary>
        /// Recalculates all card positions and rotations by sampling the actual Spline path.
        /// Uses SplineUtility.EvaluatePosition for position and SplineUtility.EvaluateTangent
        /// for tangent-based rotation. The focus card receives an additional pop-up offset
        /// along the Spline normal direction.
        /// </summary>
        public void UpdateCardPositions()
        {
            if (cards.Count == 0 || spline == null) return;

            float focusT = Settings.focusT;
            float popUpOffset = Settings.focusPopUpOffset;
            float minDist = float.MaxValue;
            int newFocusIndex = 0;

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

            // Second pass: position all cards using Spline sampling
            for (int i = 0; i < cards.Count; i++)
            {
                float t = baseTs[i] + currentOffset;

                // Clamp t to valid range [0, 1]
                float clampedT = Mathf.Clamp01(t);

                // Sample position and tangent from the Spline in local space, then transform to world
                float3 localPos3 = SplineUtility.EvaluatePosition(spline, clampedT);
                float3 localTan3 = SplineUtility.EvaluateTangent(spline, clampedT);
                Vector3 worldPos3 = splineContainer.transform.TransformPoint(localPos3);
                Vector3 worldTan3 = splineContainer.transform.TransformDirection(math.normalize(localTan3));

                // Project tangent to 2D (XY plane) for rotation calculation
                Vector2 tangent2D = new Vector2(worldTan3.x, worldTan3.y);
                if (tangent2D.sqrMagnitude < 0.0001f)
                    tangent2D = Vector2.right; // fallback

                tangent2D.Normalize();

                // Normal is perpendicular to tangent (rotated 90 degrees counter-clockwise)
                Vector2 normal2D = new Vector2(-tangent2D.y, tangent2D.x);

                // Convert world position to cardParent local position
                Vector3 localPos = cardParent.InverseTransformPoint(worldPos3);
                Vector2 anchoredPos = new Vector2(localPos.x, localPos.y);

                // Apply pop-up offset for the focus card along the Spline normal
                if (i == newFocusIndex && popUpOffset > 0f)
                {
                    anchoredPos += normal2D * popUpOffset;
                }

                // Set card position
                RectTransform cardRect = cards[i].GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    cardRect.anchoredPosition = anchoredPos;
                }
                else
                {
                    cards[i].transform.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);
                }

                // Rotate card so its local up aligns with the Spline normal (perpendicular to tangent).
                // For a hand-of-cards fan layout, the card should stand upright along the normal.
                float angleDeg = Mathf.Atan2(normal2D.y, normal2D.x) * Mathf.Rad2Deg - 90f;
                cards[i].transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

                // Hide cards that are outside the visible Spline range (near endpoints)
                float edgeMargin = Settings.splineEdgeMargin;
                bool isVisible = t > edgeMargin && t < (1f - edgeMargin);
                cards[i].gameObject.SetActive(isVisible);
            }

            // Notify focus system to update sorting
            if (focusChanged)
            {
                OnFocusChanged?.Invoke(FocusIndex);
            }
        }

        /// <summary>
        /// Returns the base anchored position for a card on the Spline (without any pop-up offset).
        /// Used by CardFocusDisplay to calculate the pop-up target position.
        /// </summary>
        public Vector2 GetCardBaseAnchoredPosition(int index)
        {
            if (index < 0 || index >= baseTs.Count || spline == null) return Vector2.zero;

            float t = baseTs[index] + currentOffset;
            float clampedT = Mathf.Clamp01(t);

            float3 spLocalPos3 = SplineUtility.EvaluatePosition(spline, clampedT);
            Vector3 worldPosition = splineContainer.transform.TransformPoint(spLocalPos3);
            Vector3 localPos = cardParent.InverseTransformPoint(worldPosition);
            return new Vector2(localPos.x, localPos.y);
        }

        /// <summary>
        /// Returns the normal direction (perpendicular to tangent) at a card's current Spline position.
        /// Used for pop-up offset direction calculation.
        /// </summary>
        public Vector2 GetCardNormalDirection(int index)
        {
            if (index < 0 || index >= baseTs.Count || spline == null) return Vector2.up;

            float t = baseTs[index] + currentOffset;
            float clampedT = Mathf.Clamp01(t);

            float3 spLocalTan3 = SplineUtility.EvaluateTangent(spline, clampedT);
            Vector3 worldTan = splineContainer.transform.TransformDirection(math.normalize(spLocalTan3));

            Vector2 tangent2D = new Vector2(worldTan.x, worldTan.y);
            if (tangent2D.sqrMagnitude < 0.0001f)
                return Vector2.up;

            tangent2D.Normalize();
            // Normal is perpendicular to tangent (rotated 90 degrees counter-clockwise)
            return new Vector2(-tangent2D.y, tangent2D.x);
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

        /// <summary>
        /// Event fired when the focus card is clicked, passing the CharacterData of the clicked card.
        /// Used to trigger character switching.
        /// </summary>
        public event System.Action<CharacterData> OnFocusCardClicked;

        /// <summary>
        /// Event fired when any card's drag ends, passing the final velocity for inertia handling.
        /// Used to decouple CardDragHandler from CardInertiaAndSnap.
        /// </summary>
        public event System.Action<float> OnDragEnded;

        /// <summary>
        /// Whether any card is currently being dragged.
        /// </summary>
        public bool IsDragging { get; set; }

        /// <summary>
        /// Shows the card group by activating the card parent container.
        /// Ensures card positions and focus state are correct after showing.
        /// </summary>
        public void Show()
        {
            if (cardParent != null)
                cardParent.gameObject.SetActive(true);

            IsVisible = true;

            // Refresh card positions and focus state
            UpdateCardPositions();

            if (focusDisplay != null && FocusIndex >= 0)
            {
                focusDisplay.UpdateVisuals(FocusIndex, animate: false);
            }
        }

        /// <summary>
        /// Hides the card group by deactivating the card parent container.
        /// Stops all ongoing animations and cancels any active drag operations.
        /// </summary>
        public void Hide()
        {
            // Cancel any active drag on all cards
            CancelAllDrags();

            // Stop inertia and snap animations
            if (inertiaAndSnap != null)
                inertiaAndSnap.StopAll();

            // Kill all DOTween animations on cards
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    DOTween.Kill(cards[i].transform);
            }

            // Kill any DOTween targeting this distributor
            DOTween.Kill(this);

            IsDragging = false;
            IsVisible = false;

            if (cardParent != null)
                cardParent.gameObject.SetActive(false);
        }

        /// <summary>
        /// Toggles the card group visibility between shown and hidden.
        /// </summary>
        public void ToggleVisibility()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Cancels drag operations on all card drag handlers.
        /// </summary>
        private void CancelAllDrags()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    CardDragHandler dragHandler = cards[i].GetComponent<CardDragHandler>();
                    if (dragHandler != null)
                        dragHandler.CancelDrag();
                }
            }
        }

        /// <summary>
        /// Returns the index of the given card in the cards list, or -1 if not found.
        /// </summary>
        public int GetCardIndex(CharacterCard card)
        {
            if (card == null) return -1;
            return cards.IndexOf(card);
        }

        /// <summary>
        /// Called by CardDragHandler when the focus card is clicked.
        /// Triggers the OnFocusCardClicked event and handles character switching.
        /// </summary>
        public void NotifyFocusCardClicked(CharacterData data)
        {
            if (data == null) return;

            // Fire the event for external subscribers
            OnFocusCardClicked?.Invoke(data);

            // Perform character switch via GameCharacterManager
            if (gameCharacterManager == null)
                gameCharacterManager = FindObjectOfType<GameCharacterManager>();

            if (gameCharacterManager != null)
            {
                bool success = gameCharacterManager.SwitchPlayerCharacter(data);
                if (success)
                {
                    // Close the card panel after successful switch
                    Hide();
                }
            }
            else
            {
                Debug.LogError("[CardSplineDistributor] GameCharacterManager not found in scene!");
            }
        }

        /// <summary>
        /// Forwards a drag-ended event from any CardDragHandler to subscribers (e.g. CardInertiaAndSnap).
        /// </summary>
        public void NotifyDragEnded(float velocity)
        {
            OnDragEnded?.Invoke(velocity);
        }

        private void Update()
        {
            // Listen for Esc key to hide the card group
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        private void OnDestroy()
        {
            ClearCards();
        }

        /// <summary>
        /// Dynamically adds a new CharacterData to the card list and refreshes all cards.
        /// Used by BossFightManager to add defeated Boss characters to the player's deck.
        /// </summary>
        /// <param name="data">The CharacterData to add.</param>
        /// <returns>True if added successfully, false if data is null or already exists.</returns>
        public bool AddCharacterData(CharacterData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[CardSplineDistributor] Cannot add null CharacterData.");
                return false;
            }

            // Check if already exists
            if (characterDataList != null)
            {
                foreach (var existing in characterDataList)
                {
                    if (existing == data)
                    {
                        Debug.LogWarning($"[CardSplineDistributor] CharacterData '{data.characterName}' already exists in the list.");
                        return false;
                    }
                }
            }

            // Append to array
            int oldLength = characterDataList != null ? characterDataList.Length : 0;
            CharacterData[] newList = new CharacterData[oldLength + 1];
            if (characterDataList != null)
            {
                characterDataList.CopyTo(newList, 0);
            }
            newList[oldLength] = data;
            characterDataList = newList;

            // Re-initialize to refresh all cards
            Initialize();

            Debug.Log($"[CardSplineDistributor] Added '{data.characterName}' to card list. Total cards: {characterDataList.Length}");
            return true;
        }
    }
}
