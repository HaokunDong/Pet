using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolMgr : Singleton<PoolMgr>
{
    private Dictionary<string, NodePool> dictPool = new Dictionary<string, NodePool>();
    private Dictionary<string, GameObject> dictPrefab = new Dictionary<string, GameObject>();

    public GameObject GetNode(string prefab,Transform parent=null)
    {
        GameObject tempPre = this.dictPrefab[prefab];
        string name = prefab;
        if (!tempPre)
        {
            Logger.Log("Pool invalid prefab name = "+ name);
            return null;
        }

        GameObject node = null;
        if (this.dictPool.ContainsKey(name))
        {
            //已有对应的对象池
            NodePool pool = this.dictPool[name];
            if (pool.GetSize() > 0)
            {
                node = pool.GetObj();
            }
            else
            {
                node = GameObject.Instantiate(tempPre);
            }
        } else {
            //没有对应对象池，创建他！
            NodePool pool = new NodePool();
            this.dictPool[name] = pool;

            node = GameObject.Instantiate(tempPre);
        }

        if (parent)
        {
            node.transform.parent = parent;
            node.SetActive(true);
            node.transform.position = Vector3.zero;
        }

        node.name = prefab;
        return node;

    }

    public void PutNode(GameObject node,Transform parent = null)
    {
        if (!node) return;

        string name = node.name;
        NodePool pool = null;
        if (dictPool.ContainsKey(name)){
            //已有对应的对象池
            pool = this.dictPool[name];
        }
        else
        {
            //没有对应对象池，创建他！
            pool = new NodePool();
            this.dictPool[name] = pool;
        }

        pool.PutObj(node, parent);

    }

    public void ClearPool(string name)
    {
        if (this.dictPool.ContainsKey(name))
        {
            NodePool pool = this.dictPool[name];
            pool.Clear();
        }
    }

    public void SetPrefab(string name,GameObject prefab) {

        this.dictPrefab[name] = prefab;
    }

    public GameObject GetPrefab(string name)  {
        return this.dictPrefab[name];
    }

}
