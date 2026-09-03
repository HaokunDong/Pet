using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PetGame;
using PetGame.UI;
using PetGame.Network;

public class MainView : BaseView
{
    private GameObject ringRadialMenu;

    private GameObject blackHole;

    // CharacterButton on the RingRadialMenu replaces the legacy Menus/Button1.
    private RingRadialMenuButton characterButton;
    // ControlButton on the RingRadialMenu replaces the legacy Menus/Button2.
    private RingRadialMenuButton controlButton;
    // FriendsButton on the RingRadialMenu replaces the legacy Menus/Button3.
    private RingRadialMenuButton friendsButton;
    // SettingButton on the RingRadialMenu replaces the legacy Menus/Button4.
    private RingRadialMenuButton settingButton;
    // SpecialLevelListButton on the RingRadialMenu opens the multiplayer level request list.
    private RingRadialMenuButton specialLevelListButton;

    [Header("Lobby")]
    [Tooltip("Path to LobbyPanel prefab in Resources folder")]
    private const string LOBBY_PANEL_PREFAB_PATH = "Prefabs/UI/LobbyPanel";

    // Cached LobbyPanel instance (dynamically loaded)
    private LobbyPanel lobbyPanelInstance;

    // Cached SpecialLevelListView instance
    private SpecialLevelListView specialLevelListView;

    // Map buttons to their sprite animation components
    private Dictionary<GameObject, ButtonSpriteAnimation> _btnAnimMap = new Dictionary<GameObject, ButtonSpriteAnimation>();

    private int num = 0;

    // Cached reference to the card selection system
    private PetGame.CardSplineDistributor cardDistributor;

    protected override void OnAwake()
    {
        ringRadialMenu = transform.Find("RingRadialMenu").gameObject;

        blackHole = transform.Find("BlackHole").gameObject;
        EventTriggerListener.Get(blackHole).onClick = OnButtonClick;
        EventTriggerListener.Get(blackHole).onEnter = OnBlackHolePointerEnter;
        EventTriggerListener.Get(blackHole).onExit = OnBlackHolePointerExit;

        // Bind RingRadialMenu buttons (replaces legacy Menus/Button1..4):
        //   CharacterButton -> Button1 (toggle card-spline distributor)
        //   ControlButton   -> Button2
        //   FriendsButton   -> Button3 (toggle LobbyPanel)
        //   SettingButton   -> Button4
        characterButton      = BindRingButton("CharacterButton",      OnCharacterButtonClick);
        controlButton        = BindRingButton("ControlButton",        OnControlButtonClick);
        friendsButton        = BindRingButton("FriendsButton",        OnFriendsButtonClick);
        settingButton        = BindRingButton("SettingButton",        OnSettingButtonClick);
        specialLevelListButton = BindRingButton("SpecialLevelListButton", OnSpecialLevelListButtonClick);

        // Register existing ButtonSpriteAnimation component (sprites configured in Inspector)
        InitButtonSpriteAnim(blackHole);

        // Initialize circular click area on BlackHole (add CircleClickArea if not already present)
        InitCircleClickArea(blackHole);

        ringRadialMenu.SetActive(false);
    }

    /// <summary>
    /// Initialize ButtonSpriteAnimation component on a button GameObject.
    /// </summary>
    private void InitButtonSpriteAnim(GameObject btn)
    {
        ButtonSpriteAnimation anim = btn.GetComponent<ButtonSpriteAnimation>();
        if (anim == null)
        {
            Debug.LogWarning($"[MainView] No ButtonSpriteAnimation found on {btn.name}. Skipping.");
            return;
        }
        _btnAnimMap[btn] = anim;
    }

    /// <summary>
    /// Ensure the given button has a CircleClickArea component for circular hit-testing.
    /// If the GameObject already has a plain Image, it will be replaced by CircleClickArea.
    /// </summary>
    private void InitCircleClickArea(GameObject btn)
    {
        var circleArea = btn.GetComponent<PetGame.UI.CircleClickArea>();
        if (circleArea == null)
        {
            // If there is an existing Image component, copy its sprite/color before replacing
            var existingImage = btn.GetComponent<Image>();
            Sprite existingSprite = null;
            Color existingColor = Color.white;
            if (existingImage != null && !(existingImage is PetGame.UI.CircleClickArea))
            {
                existingSprite = existingImage.sprite;
                existingColor = existingImage.color;
                DestroyImmediate(existingImage);
            }

            circleArea = btn.AddComponent<PetGame.UI.CircleClickArea>();
            if (existingSprite != null)
            {
                circleArea.sprite = existingSprite;
                circleArea.color = existingColor;
            }
        }
    }

    /// <summary>
    /// Called when the mouse pointer enters the BlackHole area.
    /// Plays the suspend (hover) loop animation.
    /// Does not interrupt OnClick animation.
    /// </summary>
    private void OnBlackHolePointerEnter(GameObject go)
    {
        if (_btnAnimMap.ContainsKey(go))
        {
            var anim = _btnAnimMap[go];
            if (anim.CurrentState == ButtonSpriteAnimation.AnimState.OnClick) return;
            anim.PlaySuspend();
        }
    }

    /// <summary>
    /// Called when the mouse pointer exits the BlackHole area.
    /// Reverts to the idle loop animation.
    /// Does not interrupt OnClick animation.
    /// </summary>
    private void OnBlackHolePointerExit(GameObject go)
    {
        if (_btnAnimMap.ContainsKey(go))
        {
            var anim = _btnAnimMap[go];
            if (anim.CurrentState == ButtonSpriteAnimation.AnimState.OnClick) return;
            anim.PlayIdle();
        }
    }

    void OnButtonClick(GameObject go)
    {
        if (go == blackHole)
        {
            // Toggle animation between Idle and OnClick on each click
            if (_btnAnimMap.ContainsKey(go))
            {
                var anim = _btnAnimMap[go];
                if (anim.CurrentState == ButtonSpriteAnimation.AnimState.OnClick)
                {
                    anim.PlayIdle();
                }
                else
                {
                    anim.PlayOnClick();
                }
            }
            ringRadialMenu.SetActive(!ringRadialMenu.activeSelf);
        }
    }

    /// <summary>
    /// Find a RingRadialMenuButton by child name under "RingRadialMenu" and bind its click callback.
    /// Returns null (and logs a warning) if the node or component is missing.
    /// </summary>
    private RingRadialMenuButton BindRingButton(string childName, UnityEngine.Events.UnityAction onClick)
    {
        Transform tr = transform.Find("RingRadialMenu/" + childName);
        if (tr == null)
        {
            Debug.LogWarning($"[MainView] RingRadialMenu/{childName} not found under MainView.");
            return null;
        }

        RingRadialMenuButton btn = tr.GetComponent<RingRadialMenuButton>();
        if (btn == null)
        {
            Debug.LogWarning($"[MainView] RingRadialMenu/{childName} has no RingRadialMenuButton component.");
            return null;
        }

        btn.SetCallback(onClick);
        return btn;
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's CharacterButton.
    /// Migrated from the legacy Menus/Button1: toggles the card-spline distributor visibility.
    /// </summary>
    private void OnCharacterButtonClick()
    {
        if (cardDistributor == null)
            cardDistributor = FindObjectOfType<PetGame.CardSplineDistributor>();
        if (cardDistributor != null)
            cardDistributor.ToggleVisibility();
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's ControlButton.
    /// Toggles between Manual control and AI auto control for the current player character.
    /// </summary>
    private void OnControlButtonClick()
    {
        var manager = FindObjectOfType<PetGame.GameCharacterManager>();
        if (manager == null || manager.PlayerCharacters == null || manager.PlayerCharacters.Count == 0)
        {
            Debug.LogWarning("[MainView] No player character found to toggle control mode.");
            return;
        }

        var entity = manager.PlayerCharacters[0];
        if (entity == null) return;

        var controlMode = entity.GetComponent<PetGame.ControlModeManager>();
        if (controlMode != null)
        {
            controlMode.ToggleControlMode();
        }
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's FriendsButton.
    /// Dynamically instantiates LobbyPanel prefab under GamePanel, or toggles visibility.
    /// </summary>
    private void OnFriendsButtonClick()
    {
        if (lobbyPanelInstance != null)
        {
            // Toggle: if already active, hide; otherwise show
            if (lobbyPanelInstance.gameObject.activeSelf)
                lobbyPanelInstance.Hide();
            else
                lobbyPanelInstance.Show();
        }
        else
        {
            // Dynamically load and instantiate LobbyPanel prefab
            GameObject prefab = Resources.Load<GameObject>(LOBBY_PANEL_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[MainView] LobbyPanel prefab not found at: Resources/{LOBBY_PANEL_PREFAB_PATH}");
                return;
            }

            // Find GamePanel as parent (Canvas > GamePanel)
            Transform gamePanel = FindGamePanel();
            if (gamePanel == null)
            {
                Debug.LogError("[MainView] GamePanel not found. Using MainView as parent.");
                gamePanel = transform;
            }

            GameObject instance = Instantiate(prefab, gamePanel);
            lobbyPanelInstance = instance.GetComponent<LobbyPanel>();

            if (lobbyPanelInstance != null)
            {
                lobbyPanelInstance.Show();
            }
            else
            {
                Debug.LogError("[MainView] LobbyPanel component not found on instantiated prefab!");
                Destroy(instance);
            }
        }
    }

    /// <summary>
    /// Find the GamePanel transform in the scene hierarchy.
    /// Searches for a child named "GamePanel" under the Canvas.
    /// </summary>
    private Transform FindGamePanel()
    {
        // Try to find GamePanel as sibling or parent
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform gp = canvas.transform.Find("GamePanel");
            if (gp != null) return gp;
        }

        // Fallback: search in scene
        GameObject gpObj = GameObject.Find("GamePanel");
        if (gpObj != null) return gpObj.transform;

        return null;
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's SettingButton.
    /// Migrated from the legacy Menus/Button4.
    /// </summary>
    private void OnSettingButtonClick()
    {
        Debug.Log("SettingButton");
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's SpecialLevelListButton.
    /// Only responds when in a multiplayer lobby. Toggles the SpecialLevelListForMultiplayer panel.
    /// </summary>
    private void OnSpecialLevelListButtonClick()
    {
        // Only respond in multiplayer mode
        var lobbyMgr = SteamLobbyManager.Instance;
        if (lobbyMgr == null || !lobbyMgr.InLobby)
        {
            Debug.Log("[MainView] SpecialLevelListButton: Not in a lobby. Ignoring.");
            return;
        }

        // Find or cache the SpecialLevelListView
        if (specialLevelListView == null)
        {
            specialLevelListView = FindObjectOfType<SpecialLevelListView>(true);
        }

        if (specialLevelListView != null)
        {
            specialLevelListView.ToggleVisibility();
        }
        else
        {
            Debug.LogWarning("[MainView] SpecialLevelListView not found in scene.");
        }
    }

    // protected override void AddEvent() {
    //     CommonDispatcher.Instance.AddEventListener((int)CommonEventId.AddNum, EventCallBack);
    // }

    // protected override void RemoveEvent() {
    //     CommonDispatcher.Instance.RemoveEventListener((int)CommonEventId.AddNum, EventCallBack);
    // }

    // private void EventCallBack(int eventType, string param)
    // {
    //     Logger.Log("EventCallBack=" + eventType + "," + param);

    //     num += int.Parse(param);

    // }



    // protected override void BeforeOnEnable() {
    //     Logger.Log("BeforeOnEnable MainView");

    //     ResMgr.Instance.SetTex("11", transform.Find("Image"));
    // }
}
