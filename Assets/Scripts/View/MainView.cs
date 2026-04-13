using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainView : BaseView
{
    private GameObject button1;
    private GameObject button2;

    private Text numText;
    private int num = 0;

    protected override void OnAwake()
    {
        //transform.Find("Button1").GetComponent<Button>().onClick.AddListener(OnButton1Click);
        button1 = transform.Find("Button1").gameObject;
        button2 = transform.Find("Button2").gameObject;
        EventTriggerListener.Get(button1).onClick = OnButtonClick;
        EventTriggerListener.Get(button2).onClick = OnButtonClick;

        numText = transform.Find("Text").GetComponent<Text>();
    }

    void OnButtonClick(GameObject go)
    {

        if (go == button1)
        {

        }
        else if (go == button2)
        {
            ResMgr.Instance.GetUI(UI.GameView,"122");
        }

    }

    protected override void AddEvent() {
        CommonDispatcher.Instance.AddEventListener((int)CommonEventId.AddNum, EventCallBack);
    }

    protected override void RemoveEvent() {
        CommonDispatcher.Instance.RemoveEventListener((int)CommonEventId.AddNum, EventCallBack);
    }

    private void EventCallBack(int eventType, string param)
    {
        Logger.Log("EventCallBack=" + eventType + "," + param);

        num += int.Parse(param);
        numText.text = num + "";
    }



    protected override void BeforeOnEnable() {
        Logger.Log("BeforeOnEnable MainView");

        ResMgr.Instance.SetTex("11", transform.Find("Image"));
    }
}
