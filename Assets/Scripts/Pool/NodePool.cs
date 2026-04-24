using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple object queue for pooling. Parent management is handled by PoolMgr.
/// </summary>
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

    public void PutObj(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
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
