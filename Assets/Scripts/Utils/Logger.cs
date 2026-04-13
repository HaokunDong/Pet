using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Logger 
{

    public static string GetCurrTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static void Log(object str)
    {
        if (Global.isDebug)
        {
            Debug.Log("["+GetCurrTime() + "]" + str);
        }
    }

    public static void LogWarning(object str)
    {
        if (Global.isDebug)
        {
            Debug.LogWarning("[" + GetCurrTime() + "]" + str);
        }
    }

    public static void LogError(object str)
    {
        if (Global.isDebug)
        {
            Debug.LogError("[" + GetCurrTime() + "]" + str);
        }
    }

}
