using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public static Game ins = null;
    private GameObject canvas;

    private GameObject[]  layer = new GameObject[5];

    // Start is called before the first frame update
    void Start()
    {
        ins = this;

        InitUI();

        ShowStart();

    }

    private void InitUI()
    {
        canvas = GameObject.Find("Canvas");
        for (var i = 0; i < 5; i++)
        {
            GameObject node = null;
            if (this.layer[i])
                node = this.layer[i];
            else
                node = PoolMgr.Instance.GetNode("Container", canvas.transform);

            node.name = "Layer" + i;
            node.transform.localPosition = Vector3.zero;
            node.transform.localScale = Vector3.one;
            //node.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);//左下角
            //node.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);//右上角
            node.GetComponent<RectTransform>().offsetMin = new Vector2(0, 0);//全屏显示
            node.GetComponent<RectTransform>().offsetMax = new Vector2(0, 0);
            Global.layer[i] = this.layer[i] = node;
        }

    }

    void ShowStart()
    {
        WindowManager.Instance.Init();

        ResMgr.Instance.GetUI(UI.MainView);

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
