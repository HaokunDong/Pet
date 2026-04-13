using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseView : MonoBehaviour
{

    [HideInInspector]
    public bool isShow = false;

    protected object data;

    void Awake()
    {
        OnAwake();
    }

    void Start()
    {
        OnStart();
    }

    void Update()
    {
        OnUpdate();
    }

    void OnDestroy()
    {
        RemoveEvent();
        BeforeOnDestroy();
    }

    void OnEnable()
    {
        this.isShow = true;

        //获取界面参数
        this.data = WindowManager.Instance.GetViewPara(gameObject.name);
        WindowManager.Instance.SetViewPara(gameObject.name, null);

        this.AddEvent();
        this.BeforeOnEnable();
    }

    void OnDisable()
    {
        this.isShow = false;

        this.RemoveEvent();
        this.BeforeOnDisable();
    }

    protected void Close()
    {
        BeforeClose();

        PoolMgr.Instance.PutNode(gameObject);
    }

    protected virtual void OnAwake() { }
    protected virtual void OnStart() { }

    protected virtual void BeforeOnEnable() { }

    protected virtual void BeforeOnDisable() { }
    protected virtual void OnUpdate() { }
    protected virtual void BeforeOnDestroy() { }
    protected virtual void BeforeClose() { }

    protected virtual void AddEvent() { }
    protected virtual void RemoveEvent() { }

    public void Show(bool isTop=false)
    {
        isShow = true;
        gameObject.SetActive(true);
        BeforeOnEnable();
        if (isTop)
        {
            //置顶显示
            transform.SetAsLastSibling();
        }
    }

}
