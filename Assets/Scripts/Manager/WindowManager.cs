using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindowManager : Singleton<WindowManager>
{
    private Dictionary<string, object> paraMap = new Dictionary<string, object>();

    public void Init()
    {

    }

    public void SetViewPara(string viewName, object para)
    {
        viewName = viewName.Replace("(Clone)", "");//È¥µô¿ËÂ¡Ãû×Ö
        //Debug.Log("SetViewPara==" + viewName + "," + para);
        this.paraMap[viewName] = para;
    }

    public object GetViewPara(string viewName)
    {
        viewName = viewName.Replace("(Clone)", "");
        if (!paraMap.ContainsKey(viewName)) return null;
        return this.paraMap[viewName];
    }

    public GameObject GetWindow(UI ui)
    {
        UIInfo uiInfo = Global.GetUIInfo(ui);
        if (uiInfo == null) return null;

        int childCount=Global.layer[uiInfo.layer].transform.childCount;
        for (int i = 0; i < childCount; i++)
        { Transform tra = Global.layer[uiInfo.layer].transform.GetChild(i);
            if (tra.name== uiInfo.name)
            {
                return tra.gameObject;
            }
        }

        return null;
    }


}
