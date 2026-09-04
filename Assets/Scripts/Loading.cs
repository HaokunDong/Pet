using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PetGame.Network;

public class Loading : MonoBehaviour
{
    private bool canLoad = false;

    private Slider slider;
    // Start is called before the first frame update
    void Start()
    {
        slider = GameObject.Find("Canvas/Slider").GetComponent<Slider>();
        slider.value = 0;

        canLoad = true;
        Global.loadingRate = 0;

        LoadRes();
    }

    void LoadRes()
    {
        ResMgr.Instance.LoadRes<GameObject>("Prefabs/View",0.5f);
        ResMgr.Instance.LoadRes<Sprite>("Image", 0.4f);
        ResMgr.Instance.LoadRes<TextAsset>("Config",0.1f);
    }

    // Update is called once per frame
    void Update()
    {
        if (!canLoad) return;

        slider.value = Global.loadingRate;

        if (Global.loadingRate >= 1)
        {
            canLoad = false;

            // In multiplayer mode (host), use Mirror's ServerChangeScene
            // so all connected clients are synchronized to the same scene.
            var netMgr = MirrorNetworkManager.singleton;
            if (netMgr != null && netMgr.IsHostActive && netMgr.ConnectedPlayers.Count > 1)
            {
                Debug.Log("[Loading] Multiplayer host detected. Using ChangeSceneForAll to switch to Game scene.");
                netMgr.ChangeSceneForAll("Game");
            }
            else
            {
                // Single player or client-only: use standard scene load
                SceneManager.LoadScene("Game");
            }
        }
    }
}
