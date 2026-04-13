using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalInit : MonoBehaviour
{
    [Header("ÊÇ·ñ¿ªÆôLog")]
    public bool isDebug = true;

    [Header("ÓïÑÔ")]
    public LanguageType language;

    private void Awake()
    {
        Global.isDebug = isDebug;
        Global.language = language;

    }
}
