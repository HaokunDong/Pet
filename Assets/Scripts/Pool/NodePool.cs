using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodePool 
{
    private Queue<GameObject> objQueue;

    public NodePool()
    {
        objQueue = new Queue<GameObject>();
    }

    public GameObject GetObj()
    {
        if (objQueue.Count > 0)
        {
            return objQueue.Dequeue();
        }

        return null;
    }

    public void PutObj(GameObject obj,Transform parent=null)
    {
        if (obj == null) return;

        obj.SetActive(false);

        if (!parent)
        {
            //默认回到所属的画布下
            if(GameObject.Find("Canvas"))
                obj.transform.SetParent(GameObject.Find("Canvas").transform);
        }
        else
        {
            obj.transform.SetParent(parent);
        }

        objQueue.Enqueue(obj);
    }

    public int GetSize()
    {
        return objQueue.Count;
    }

    public void Clear()
    {
        objQueue.Clear();
    }
}
