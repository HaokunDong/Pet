using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public class MainView : BaseView
{
    private GameObject menus;

    private GameObject blackHole;
    private GameObject button1;
    private GameObject button2;
    private GameObject button3;
    private GameObject button4;

    // Map buttons to their sprite animation components
    private Dictionary<GameObject, ButtonSpriteAnimation> _btnAnimMap = new Dictionary<GameObject, ButtonSpriteAnimation>();

    private int num = 0;

    protected override void OnAwake()
    {
        //transform.Find("Button1").GetComponent<Button>().onClick.AddListener(OnButton1Click);
        menus = transform.Find("Menus").gameObject;

        blackHole = transform.Find("BlackHole").gameObject;
        button1 = transform.Find("Menus/Button1").gameObject;
        button2 = transform.Find("Menus/Button2").gameObject;
        button3 = transform.Find("Menus/Button3").gameObject;
        button4 = transform.Find("Menus/Button4").gameObject;
        EventTriggerListener.Get(blackHole).onClick = OnButtonClick;
        EventTriggerListener.Get(button1).onClick = OnButtonClick;
        EventTriggerListener.Get(button2).onClick = OnButtonClick;
        EventTriggerListener.Get(button3).onClick = OnButtonClick;
        EventTriggerListener.Get(button4).onClick = OnButtonClick;

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
        // InitButtonSpriteAnim(button1, idleSprites, onClickSprites);
        // InitButtonSpriteAnim(button2, idleSprites, onClickSprites);
        // InitButtonSpriteAnim(button3, idleSprites, onClickSprites);
        // InitButtonSpriteAnim(button4, idleSprites, onClickSprites);

        menus.SetActive(false);
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
            menus.SetActive(!menus.activeSelf);
        }
        else if (go == button1)
        {
            Debug.Log("button1");
        }
        else if (go == button2)
        {
            Debug.Log("button2");
        }
        else if (go == button3)
        {
            Debug.Log("button3");
        }
        else if (go == button4)
        {
            Debug.Log("button4");
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
