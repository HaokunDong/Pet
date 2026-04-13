using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameView : BaseView
{
    private GameObject button1;
    private GameObject button2;

    protected override void OnAwake()
    {
        //transform.Find("Button1").GetComponent<Button>().onClick.AddListener(OnButton1Click);
        button1 = transform.Find("Button1").gameObject;
        button2 = transform.Find("Button2").gameObject;
        EventTriggerListener.Get(button1).onClick = OnButtonClick;
        EventTriggerListener.Get(button2).onClick = OnButtonClick;
    }

    void OnButtonClick(GameObject go)
    {

        if (go == button1)
        {
            //¹Ø±Õ
            Close();
        }
        else if (go == button2)
        {
            //Ôö¼Ó
            CommonDispatcher.Instance.Dispatch((int)CommonEventId.AddNum, "10");
        }

    }


    protected override void BeforeOnEnable()
    {
        Debug.Log("BeforeOnEnable GameView"+this.data);
    }
}
