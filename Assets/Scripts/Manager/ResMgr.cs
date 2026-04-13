using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResMgr : Singleton<ResMgr>
{
    private Dictionary<string, Sprite> textureDic = new Dictionary<string, Sprite>();

    public void LoadRes<T>(string path, float ratio = 0) where T : Object
    {
        T[] objs= Resources.LoadAll<T>(path);


        if (typeof(T) == typeof(UnityEngine.GameObject))
        {
            for (int i = 0; i < objs.Length; i++)
            {
                Logger.Log("GameObject name==" + objs[i].name);
                PoolMgr.Instance.SetPrefab(objs[i].name, objs[i] as GameObject);
            }

        }
        else if (typeof(T) == typeof(UnityEngine.Sprite))
        {
            for (int i = 0; i < objs.Length; i++)
            {
                Logger.Log("Sprite name==" + objs[i].name);
                if (!this.textureDic.ContainsKey(objs[i].name)) this.textureDic[objs[i].name] = (objs[i] as Sprite);
            }
        }
        else if (typeof(T) == typeof(UnityEngine.TextAsset))
        {
            for (int i = 0; i < objs.Length; i++)
            {
                Logger.Log("TextAsset name==" + objs[i].name);
                //Logger.Log("TextAsset text==" + (objs[i] as TextAsset).text);

            }
        }


        Global.loadingRate += ratio;

    }

    #region 显示图片
    public void SetTex(string name,Image image)
    {
        if (textureDic.ContainsKey(name))
        {
            image.sprite = textureDic[name];

            image.SetNativeSize();
        }
    }

    public void SetTex(string name, Transform tra)
    {
        Image image = tra.GetComponent<Image>();
        if (!image) return;

        SetTex(name, image);
    }
    #endregion

    public GameObject GetPrefab(string name,GameObject parent)
    {

        if (PoolMgr.Instance.GetPrefab(name))
        {
            return PoolMgr.Instance.GetNode(name, parent.transform);
        }
        else
        {
            return null;
        }
        
        return PoolMgr.Instance.GetNode(name, parent.transform);

    }

    public GameObject GetUI(UI ui,object para=null, GameObject parent=null)
    {
        UIInfo uiInfo = Global.GetUIInfo(ui);
        if (uiInfo==null) return null;

        WindowManager.Instance.SetViewPara(uiInfo.name, para);

        GameObject view =WindowManager.Instance.GetWindow(ui);
        if (view)
        {
            //已存在的 刷新显示
            BaseView bv = view.GetComponent<BaseView>();
            if (bv && bv.isShow) {
                bv.Show();
                return view;
            }
        }

        GameObject parentNode = parent ? parent : Global.layer[uiInfo.layer];
        GameObject node= GetPrefab(uiInfo.name, parentNode);
        node.transform.localPosition = Vector3.zero;
        node.transform.localScale = Vector3.one;
        node.GetComponent<RectTransform>().offsetMin = new Vector2(0, 0);//全屏显示
        node.GetComponent<RectTransform>().offsetMax = new Vector2(0, 0);
        node.name = uiInfo.name;

        return node;
    }
}
