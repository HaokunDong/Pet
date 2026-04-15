using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Awake()
    {
        PoolMgr.Instance.SetPrefab("PlayerPrefab", Resources.Load<GameObject>("Prefabs/Entity/Characters/PlayerPrefab"));
        PoolMgr.Instance.SetPrefab("EnemyPrefab", Resources.Load<GameObject>("Prefabs/Entity/Enemies/MinorEnemy/EnemyPrefab"));
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
