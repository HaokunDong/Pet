using System;
using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragEvent : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    private string _key = string.Empty;//不需要设置，lua端会主动设置

    private const string DRAG_BEGIN = ".OnBeginDrag";
    private const string DRAG_END = ".OnEndDrag";

    private string eventKey = "DragViewEvent";
    private string eventType;
    private object passData;

    private Vector3 initPos;
    private RectTransform rectTransform;
    private Graphic image;
    private RectTransform pRectTransform;
    private Transform parent;
    private Transform windowParent;

    private bool isDraging = false;

    public bool isArea = false;//是否有区域限制

    //public int directionLimit = 0;//--拖拽方向限制  0：无限制 1：只能上下 2：只能左右

    //private int checkLimitTimes = 0;

    //private bool isLimit = false;

    /// <summary>
    /// 开始触摸的手指的位置
    /// </summary>
    private Vector3 _startTouchPos;
    private PointerEventData _pointerEventData;

    protected void Awake()
    {
        //eventKey = gameObject.name;//lua端会将名字设置为DragEvent
        parent = transform.parent;
        // //pRectTransform = transform.parent.GetComponent<RectTransform>();
        // rectTransform = transform.GetComponent<RectTransform>();
        // image = transform.GetComponent<Graphic>();
        // //image.raycastTarget = false;

        Transform curParent = parent;
        while (curParent != null && !curParent.name.Contains("Window"))
        {
            //继续向上遍历
            curParent = curParent.parent;


        }
        windowParent = curParent;
        pRectTransform = windowParent.parent.GetComponent<RectTransform>();

    }

    private Action<int,Vector2> dargFunc;
    private Action<int> startFunc;
    private Action<GameObject> endFunc;

    public void SetCallFunc(Action<int,Vector2> dargFunc,Action<int> startFunc,Action<GameObject> endFunc){
        this.dargFunc=dargFunc;
        this.startFunc=startFunc;
        this.endFunc=endFunc;
    }

    public void OnDrag(PointerEventData eventData)
    {
        //if (isLimit)
        //    return;

        Vector2 result;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pRectTransform,//一定要填写当前Item的父节点的RectTransfom
            eventData.position,
            eventData.pressEventCamera,
            out result);

        if(dargFunc!=null){
            // dargFunc(1,eventData.position);
            dargFunc(1,result);
        }

        // if(isArea) //区域限制
        // {
        //     if (RectTransformUtility.RectangleContainsScreenPoint(pRectTransform, eventData.position, eventData.pressEventCamera))
        //     {
        //         rectTransform.anchoredPosition = result;
        //     }
        // }
        // else
        // {
        //     rectTransform.anchoredPosition = result;
        // }

       

    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDraging)
            return;

        if(startFunc!=null){
            // dargFunc(1,eventData.position);
            startFunc(int.Parse(name));
        }

        //checkLimitTimes = 0;
        //isLimit = false;

        // _pointerEventData = eventData;
        // _startTouchPos = _pointerEventData.position;


        // initPos = transform.position;
        // //Debug.Log(pRectTransform);
        isDraging = true;
        // transform.parent = windowParent;
        // transform.SetAsLastSibling();
        // image.raycastTarget = false;
        // eventType = DRAG_BEGIN;
        // CallChangeMethond(eventData);

        if(dargFunc!=null){
            // dargFunc(1,eventData.position);
            dargFunc(0,new Vector2(0,0));
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {

        isDraging = false;
        // //isLimit = false;
        // transform.parent = parent;
        // image.raycastTarget = true;
        // //if(!isArea) //如果没有区域限制，返回到原来位置
        //     transform.position = initPos;
        // //Debug.LogError("eventData===>>" + eventData.pressPosition+ eventData.position);
        // if (eventData == null || eventData.pointerEnter == null)
        // {
        //     //return;
        //     eventData = null;
        // }
        // //checkLimitTimes = 0;
        // eventType = DRAG_END;
        // CallChangeMethond(eventData);

        if(dargFunc!=null){
            // dargFunc(1,eventData.position);
            dargFunc(2,new Vector2(0,0));
        }

        if(endFunc!=null){
            GameObject obj = eventData.pointerEnter;
            // dargFunc(1,eventData.position);
            endFunc(obj);
        }
    }

    // private Action<int, PointerEventData> callBack;
    // public void SetCallBackData(Action<int, PointerEventData> callBack)
    // {
    //     this.callBack = callBack;
    // }

    private float StartMouseLocationX;
    private float StartMouseLocationY;
    private float EndMouseLocationX;
    private float EndMouseLocationY;



    void Destroy()
    {
        rectTransform = null;
        pRectTransform = null;
        image = null;
        parent = null;
        windowParent = null;

        // _CallBeginMethond = null;
        // _CallEndMethond = null;
        passData = null;
    }




}
