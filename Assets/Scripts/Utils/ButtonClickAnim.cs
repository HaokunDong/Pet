using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.EventSystems;
using System;

public class ButtonClickAnim : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,IPointerExitHandler
{
    private bool isPress = false;
    public bool PlayAnim = true;
    public Transform Target = null;
    public ButtonClickAnim otherScript;
    public ButtonClickAnim OtherScript
    {
        set
        {
            if (value != null)
            {
                otherScript = value;
            }
            else
            {
                Debug.LogError(" set otherScript 失败 the obj is" + gameObject);
            }
        }
    }

    private Vector3 oldScale;

    public RectTransform[] unionObujects;

    private Transform _targetTransform;
    void Start()
    {
        _targetTransform = Target ? Target : this.transform;
        oldScale = _targetTransform.localScale;
    }

    void OnDown()
    {
        if(otherScript!=null)
            otherScript.OnDown();
        if (!isPress)
        {
            isPress = !isPress;

            if (!PlayAnim) return;

            _targetTransform.DOScale(oldScale * 0.9f, 0.15f);

            if (unionObujects != null)
            {
                foreach (var rectItem in unionObujects)
                {
                    rectItem.DOScale(oldScale * 0.9f, 0.15f);
                }
            }
        }
    }

    void OnUp()
    {
        if (otherScript != null)
            otherScript.OnUp();
        if (isPress)
        {
            isPress = !isPress;

            if (!PlayAnim) return;

            _targetTransform.DOScale(oldScale, 0.15f);

            if (unionObujects != null)
            {
                foreach (var rectItem in unionObujects)
                {
                    rectItem.DOScale(oldScale, 0.15f);
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDown();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnUp();
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        OnUp();
    }
}
