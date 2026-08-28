using UnityEngine;
using UnityEngine.UI;
using PetGame.Network;

namespace PetGame.UI
{
    /// <summary>
    /// UI controller for the SpecialLevelListForMultiplayer panel.
    /// Displays up to 4 option slots, each showing level info with Yes/No buttons.
    /// Listens to SpecialLevelListManager's SyncList changes to refresh display.
    /// </summary>
    public class SpecialLevelListView : MonoBehaviour
    {
        #region Constants

        private const int SLOT_COUNT = 4;

        // Child naming conventions for auto-discovery
        private const string LEVEL_IMAGE_NAME = "LevelImage";
        private const string REWARD_DESC_NAME = "RewardDescription";
        private const string YES_BUTTON_NAME = "YesButton";
        private const string NO_BUTTON_NAME = "NoButton";

        #endregion

        #region Private Fields

        private GameObject[] optionSlots = new GameObject[SLOT_COUNT];
        private Image[] levelImages = new Image[SLOT_COUNT];
        private Text[] rewardDescriptions = new Text[SLOT_COUNT];
        private Button[] yesButtons = new Button[SLOT_COUNT];
        private Button[] noButtons = new Button[SLOT_COUNT];

        private SpecialLevelListManager _manager;
        private bool _isHost;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Auto-discover option slots from direct children
            AutoDiscoverSlots();

            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Automatically find option slot GameObjects from direct children of this transform,
        /// and resolve their internal components (LevelImage, RewardDescription, YesButton, NoButton).
        /// Slots are assigned in sibling order (first 4 direct children are treated as option slots).
        /// </summary>
        private void AutoDiscoverSlots()
        {
            int slotIndex = 0;
            for (int i = 0; i < transform.childCount && slotIndex < SLOT_COUNT; i++)
            {
                Transform child = transform.GetChild(i);
                optionSlots[slotIndex] = child.gameObject;

                // Find LevelImage
                Transform levelImgTrans = child.Find(LEVEL_IMAGE_NAME);
                if (levelImgTrans != null)
                    levelImages[slotIndex] = levelImgTrans.GetComponent<Image>();

                // Find RewardDescription
                Transform rewardDescTrans = child.Find(REWARD_DESC_NAME);
                if (rewardDescTrans != null)
                    rewardDescriptions[slotIndex] = rewardDescTrans.GetComponent<Text>();

                // Find YesButton
                Transform yesBtnTrans = child.Find(YES_BUTTON_NAME);
                if (yesBtnTrans != null)
                    yesButtons[slotIndex] = yesBtnTrans.GetComponent<Button>();

                // Find NoButton
                Transform noBtnTrans = child.Find(NO_BUTTON_NAME);
                if (noBtnTrans != null)
                    noButtons[slotIndex] = noBtnTrans.GetComponent<Button>();

                slotIndex++;
            }

            Debug.Log($"[SpecialLevelListView] Auto-discovered {slotIndex} option slots from children.");
        }

        private void OnEnable()
        {
            // Find the manager
            _manager = SpecialLevelListManager.Instance;
            if (_manager == null)
            {
                _manager = FindObjectOfType<SpecialLevelListManager>();
            }

            if (_manager != null)
            {
                _manager.OnOptionListChanged += RefreshUI;
            }

            // Determine host status
            var lobbyMgr = SteamLobbyManager.Instance;
            _isHost = lobbyMgr != null && lobbyMgr.IsHost;

            // Bind button callbacks
            BindButtons();

            // Initial refresh
            RefreshUI();
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.OnOptionListChanged -= RefreshUI;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle the visibility of this panel.
        /// </summary>
        public void ToggleVisibility()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }

        #endregion

        #region UI Refresh

        /// <summary>
        /// Refresh all option slots based on current SyncList data.
        /// </summary>
        private void RefreshUI()
        {
            if (_manager == null)
            {
                _manager = SpecialLevelListManager.Instance;
                if (_manager == null) return;
            }

            // Update host status (may change if host migrates)
            var lobbyMgr = SteamLobbyManager.Instance;
            _isHost = lobbyMgr != null && lobbyMgr.IsHost;

            int optionCount = _manager.OptionList.Count;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (optionSlots[i] == null) continue;

                if (i < optionCount)
                {
                    // Show this slot with data
                    optionSlots[i].SetActive(true);

                    SpecialLevelOptionData optionData = _manager.OptionList[i];
                    SpecialLevelData levelData = _manager.GetLevelData(optionData.specialLevelDataIndex);

                    if (levelData != null)
                    {
                        // Set level image
                        if (levelImages[i] != null)
                        {
                            levelImages[i].sprite = levelData.LevelImage;
                            levelImages[i].enabled = levelData.LevelImage != null;
                        }

                        // Set reward description
                        if (rewardDescriptions[i] != null)
                        {
                            rewardDescriptions[i].text = levelData.RewardDescription;
                        }
                    }
                    else
                    {
                        // Clear display if no data
                        if (levelImages[i] != null)
                            levelImages[i].enabled = false;
                        if (rewardDescriptions[i] != null)
                            rewardDescriptions[i].text = "";
                    }

                    // Set button interactability based on host status
                    if (yesButtons[i] != null)
                        yesButtons[i].interactable = _isHost;
                    if (noButtons[i] != null)
                        noButtons[i].interactable = _isHost;
                }
                else
                {
                    // Hide unused slots
                    optionSlots[i].SetActive(false);
                }
            }
        }

        #endregion

        #region Button Binding

        /// <summary>
        /// Bind Yes/No button click events for all slots.
        /// </summary>
        private void BindButtons()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                int index = i; // Capture for closure

                if (yesButtons[i] != null)
                {
                    yesButtons[i].onClick.RemoveAllListeners();
                    yesButtons[i].onClick.AddListener(() => OnYesButtonClicked(index));
                }

                if (noButtons[i] != null)
                {
                    noButtons[i].onClick.RemoveAllListeners();
                    noButtons[i].onClick.AddListener(() => OnNoButtonClicked(index));
                }
            }
        }

        /// <summary>
        /// Called when a Yes button is clicked. Only host can trigger this.
        /// </summary>
        private void OnYesButtonClicked(int slotIndex)
        {
            if (!_isHost)
            {
                Debug.LogWarning("[SpecialLevelListView] Only the host can approve requests.");
                return;
            }

            if (_manager == null) return;

            if (slotIndex >= 0 && slotIndex < _manager.OptionList.Count)
            {
                _manager.CmdApproveOption(slotIndex);
            }
        }

        /// <summary>
        /// Called when a No button is clicked. Only host can trigger this.
        /// </summary>
        private void OnNoButtonClicked(int slotIndex)
        {
            if (!_isHost)
            {
                Debug.LogWarning("[SpecialLevelListView] Only the host can reject requests.");
                return;
            }

            if (_manager == null) return;

            if (slotIndex >= 0 && slotIndex < _manager.OptionList.Count)
            {
                _manager.CmdRejectOption(slotIndex);
            }
        }

        #endregion
    }
}
