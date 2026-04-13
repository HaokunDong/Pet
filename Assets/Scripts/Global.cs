using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LanguageType
{
    CH=0,
    EN,
    CHT
}

public enum UI
{
    MainView,   
    GameView,
}

public class Global 
{
    public static bool isDebug = true;

    public static LanguageType language;

    public static float loadingRate = 0;

    public static GameObject[] layer = new GameObject[5];

    private static Dictionary<UI, UIInfo> uiDic = new Dictionary<UI, UIInfo>
    {
        { UI.MainView,new UIInfo(UI.MainView.ToString(),1) },
        { UI.GameView,new UIInfo(UI.GameView.ToString(),2) },
    };

    public static UIInfo GetUIInfo(UI ui)
    {
        if (uiDic.ContainsKey(ui))
        {
            return uiDic[ui];
        }
        else
        {
            return null;
        }
    }
}

public class UIInfo
{
    public string name;
    public int layer;

    public UIInfo(string name, int layer)
    {
        this.name = name;
        this.layer = layer;
    }
}
