using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object pool manager that works for both UI and non-UI objects.
/// Each pool has a dedicated hidden container under a root "Pool" node.
/// When getting an object, pass a parent to place it under a specific transform,
/// or leave parent null to place it at the scene root.
/// </summary>
public class PoolMgr : Singleton<PoolMgr>
{
    private Dictionary<string, NodePool> dictPool = new Dictionary<string, NodePool>();
    private Dictionary<string, GameObject> dictPrefab = new Dictionary<string, GameObject>();

    /// <summary>
    /// Hidden root container that holds all pool sub-containers.
    /// </summary>
    private Transform poolRoot;

    /// <summary>
    /// Per-pool containers so recycled objects are organized in the hierarchy.
    /// </summary>
    private Dictionary<string, Transform> dictContainer = new Dictionary<string, Transform>();

    /// <summary>
    /// Ensure the pool root exists in the scene.
    /// </summary>
    private Transform GetPoolRoot()
    {
        if (poolRoot == null)
        {
            GameObject rootObj = new GameObject("[Pool]");
            rootObj.SetActive(true);
            Object.DontDestroyOnLoad(rootObj);
            poolRoot = rootObj.transform;
        }
        return poolRoot;
    }

    /// <summary>
    /// Get or create a hidden container for a specific pool.
    /// </summary>
    private Transform GetContainer(string poolName)
    {
        if (!dictContainer.TryGetValue(poolName, out Transform container) || container == null)
        {
            GameObject containerObj = new GameObject(poolName);
            containerObj.SetActive(true);
            containerObj.transform.SetParent(GetPoolRoot());
            container = containerObj.transform;
            dictContainer[poolName] = container;
        }
        return container;
    }

    /// <summary>
    /// Get an object from the pool.
    /// If parent is provided, the object will be placed under that parent (useful for UI).
    /// If parent is null, the object will be placed at the scene root (useful for game objects).
    /// </summary>
    public GameObject GetNode(string prefab, Transform parent = null)
    {
        GameObject tempPre = this.dictPrefab[prefab];
        string name = prefab;
        if (!tempPre)
        {
            Logger.Log("Pool invalid prefab name = " + name);
            return null;
        }

        GameObject node = null;
        if (this.dictPool.ContainsKey(name))
        {
            NodePool pool = this.dictPool[name];
            // Keep trying to get a valid (non-destroyed) object from the pool
            while (pool.GetSize() > 0)
            {
                node = pool.GetObj();
                if (node != null) break;
                // Object was destroyed externally (e.g. network despawn), skip it
                node = null;
            }
            if (node == null)
            {
                node = GameObject.Instantiate(tempPre);
            }
        }
        else
        {
            NodePool pool = new NodePool();
            this.dictPool[name] = pool;
            node = GameObject.Instantiate(tempPre);
        }

        // Place under the specified parent, or detach to scene root
        if (parent != null)
        {
            node.transform.SetParent(parent, false);
        }
        else
        {
            node.transform.SetParent(null);
        }

        node.name = prefab;
        return node;
    }

    /// <summary>
    /// Return an object to the pool.
    /// If parent is provided, the object will be stored under that parent.
    /// If parent is null, the object will be stored under the pool's dedicated container.
    /// </summary>
    public void PutNode(GameObject node, Transform parent = null)
    {
        if (!node) return;

        string name = node.name;
        NodePool pool = null;
        if (dictPool.ContainsKey(name))
        {
            pool = this.dictPool[name];
        }
        else
        {
            pool = new NodePool();
            this.dictPool[name] = pool;
        }

        node.SetActive(false);

        // Store under specified parent, or under the pool's dedicated container
        if (parent != null)
        {
            node.transform.SetParent(parent);
        }
        else
        {
            node.transform.SetParent(GetContainer(name));
        }

        pool.PutObj(node);
    }

    public void ClearPool(string name)
    {
        if (this.dictPool.ContainsKey(name))
        {
            NodePool pool = this.dictPool[name];
            pool.Clear();
        }

        // Also destroy the container
        if (dictContainer.TryGetValue(name, out Transform container) && container != null)
        {
            Object.Destroy(container.gameObject);
            dictContainer.Remove(name);
        }
    }

    /// <summary>
    /// Check if a prefab with the given name is registered in the pool.
    /// </summary>
    public bool HasPrefab(string name)
    {
        return dictPrefab.ContainsKey(name) && dictPrefab[name] != null;
    }

    public void SetPrefab(string name, GameObject prefab)
    {
        this.dictPrefab[name] = prefab;
    }

    public GameObject GetPrefab(string name)
    {
        return this.dictPrefab[name];
    }
}
