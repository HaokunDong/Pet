using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using PetGame;
using PetGame.UI;

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

    [Header("Lobby")]
    [SerializeField] private LobbyPanel lobbyPanel;

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

        // Bind RingRadialMenu buttons (replaces legacy Menus/Button1..4):
        //   CharacterButton -> Button1 (toggle card-spline distributor)
        //   ControlButton   -> Button2
        //   FriendsButton   -> Button3 (toggle LobbyPanel)
        //   SettingButton   -> Button4
        characterButton = BindRingButton("CharacterButton", OnCharacterButtonClick);
        controlButton   = BindRingButton("ControlButton",   OnControlButtonClick);
        friendsButton   = BindRingButton("FriendsButton",   OnFriendsButtonClick);
        settingButton   = BindRingButton("SettingButton",   OnSettingButtonClick);

        // Load sprite sheets from Resources
        Sprite[] idleSprites = Resources.LoadAll<Sprite>("UI/MainMenu/idle")
            .OrderBy(s => {
                string numStr = s.name.Replace("idle_", "");
                int.TryParse(numStr, out int n);
                return n;
            }).ToArray();

        Sprite[] onClickSprites = Resources.LoadAll<Sprite>("UI/MainMenu/onclicked")
            .OrderBy(s => {
                string numStr = s.name.Replace("onclicked_", "");
                int.TryParse(numStr, out int n);
                return n;
            }).ToArray();

        // Initialize sprite animation for each button
        InitButtonSpriteAnim(blackHole, idleSprites, onClickSprites);

        ringRadialMenu.SetActive(false);
    }

    /// <summary>
    /// Initialize ButtonSpriteAnimation component on a button GameObject.
    /// </summary>
    private void InitButtonSpriteAnim(GameObject btn, Sprite[] idleSprites, Sprite[] onClickSprites)
    {
        ButtonSpriteAnimation anim = btn.GetComponent<ButtonSpriteAnimation>();
        if (anim == null)
        {
            anim = btn.AddComponent<ButtonSpriteAnimation>();
        }
        anim.idleSprites = idleSprites;
        anim.onClickSprites = onClickSprites;
        anim.autoPlayIdle = true;
        _btnAnimMap[btn] = anim;
    }

    void OnButtonClick(GameObject go)
    {
        // Trigger OnClick sprite animation if available

        if (go == blackHole)
        {
            if (_btnAnimMap.ContainsKey(go))
            {
                _btnAnimMap[go].PlayOnClick();
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
    /// Migrated from the legacy Menus/Button2.
    /// </summary>
    private void OnControlButtonClick()
    {
        Debug.Log("ControlButton");
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's FriendsButton.
    /// Migrated from the legacy Menus/Button3: toggles the LobbyPanel visibility.
    /// </summary>
    private void OnFriendsButtonClick()
    {
        if (lobbyPanel != null)
        {
            // Toggle: if already active, hide; otherwise show
            if (lobbyPanel.gameObject.activeSelf)
                lobbyPanel.Hide();
            else
                lobbyPanel.Show();
        }
        else
        {
            Debug.LogWarning("[MainView] LobbyPanel reference not set!");
        }
    }

    /// <summary>
    /// Click handler for the RingRadialMenu's SettingButton.
    /// Migrated from the legacy Menus/Button4.
    /// </summary>
    private void OnSettingButtonClick()
    {
        Debug.Log("SettingButton");
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
