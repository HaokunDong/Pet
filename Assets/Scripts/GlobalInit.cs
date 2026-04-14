using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalInit : MonoBehaviour
{
    [Header("是否开启Log")]
    public bool isDebug = true;

    [Header("语言")]
    public LanguageType language;

    private void Awake()
    {
        Global.isDebug = isDebug;
        Global.language = language;

    }
}
