using UnityEngine;
using UnityEditor;
using Mirror;
using Mirror.FizzySteam;
using PetGame.Network;
using System.IO;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create the MirrorNetworkManager prefab with FizzySteamworks transport.
    /// Accessible via: Tools > PetGame > Create MirrorNetworkManager Prefab
    /// </summary>
    public static class CreateMirrorNetworkManagerPrefab
    {
        private const string PREFAB_DIR = "Assets/Resources/Prefabs/Network";
        private const string PREFAB_NAME = "MirrorNetworkManager";

        [MenuItem("Tools/PetGame/Create MirrorNetworkManager Prefab")]
        public static void CreatePrefab()
        {
            // Ensure directory exists
            if (!Directory.Exists(PREFAB_DIR))
            {
                Directory.CreateDirectory(PREFAB_DIR);
                AssetDatabase.Refresh();
            }

            string prefabPath = $"{PREFAB_DIR}/{PREFAB_NAME}.prefab";

            // Check if prefab already exists
            if (File.Exists(prefabPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Prefab Already Exists",
                    $"A prefab named '{PREFAB_NAME}' already exists at:\n{prefabPath}\n\nDo you want to overwrite it?",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
            }

            // Create the NetworkManager GameObject
            GameObject managerObj = new GameObject(PREFAB_NAME);

            // Add MirrorNetworkManager component (which inherits from NetworkManager)
            MirrorNetworkManager networkManager = managerObj.AddComponent<MirrorNetworkManager>();

            // Add FizzySteamworks transport
            FizzySteamworks steamTransport = managerObj.AddComponent<FizzySteamworks>();

            // Configure transport settings
            steamTransport.reliableChannel = 0;
            steamTransport.unreliableChannel = 1;
            steamTransport.connectionTimeout = 25f;
            steamTransport.maxPacketSize = 1200;

            // Set transport reference on NetworkManager
            // (MirrorNetworkManager.EnsureTransport() handles this at runtime,
            //  but we also set it here for the prefab)
            networkManager.transport = steamTransport;
            Transport.active = steamTransport;

            // Configure NetworkManager settings
            networkManager.maxConnections = 4;

            // Create a simple NetworkPlayer prefab placeholder
            string playerPrefabPath = CreatePlayerPrefab();
            if (!string.IsNullOrEmpty(playerPrefabPath))
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
                if (playerPrefab != null)
                {
                    networkManager.playerPrefab = playerPrefab;
                }
            }

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(managerObj, prefabPath);
            Object.DestroyImmediate(managerObj);

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                EditorUtility.DisplayDialog(
                    "MirrorNetworkManager Prefab Created",
                    $"MirrorNetworkManager prefab created at:\n{prefabPath}\n\n" +
                    "Configuration:\n" +
                    "- Transport: FizzySteamworks (Steam P2P)\n" +
                    "- Max Connections: 2\n" +
                    "- Player Prefab: NetworkPlayer (placeholder)\n\n" +
                    "Next steps:\n" +
                    "1. Drag this prefab into your startup scene\n" +
                    "2. Or it will be auto-created by the lobby system when needed",
                    "OK");

                Debug.Log($"[CreateMirrorNetworkManagerPrefab] Prefab created at: {prefabPath}");
            }
            else
            {
                Debug.LogError("[CreateMirrorNetworkManagerPrefab] Failed to create prefab.");
            }
        }

        /// <summary>
        /// Creates a simple NetworkPlayer prefab with NetworkIdentity.
        /// </summary>
        private static string CreatePlayerPrefab()
        {
            string playerPrefabDir = "Assets/Resources/Prefabs/Network";
            string playerPrefabPath = $"{playerPrefabDir}/NetworkPlayer.prefab";

            if (File.Exists(playerPrefabPath))
            {
                return playerPrefabPath; // Already exists
            }

            if (!Directory.Exists(playerPrefabDir))
            {
                Directory.CreateDirectory(playerPrefabDir);
                AssetDatabase.Refresh();
            }

            // Create a simple player placeholder
            GameObject playerObj = new GameObject("NetworkPlayer");
            playerObj.AddComponent<NetworkIdentity>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerObj, playerPrefabPath);
            Object.DestroyImmediate(playerObj);

            if (prefab != null)
            {
                Debug.Log($"[CreateMirrorNetworkManagerPrefab] NetworkPlayer prefab created at: {playerPrefabPath}");
                return playerPrefabPath;
            }

            return null;
        }

        /// <summary>
        /// Menu item to ensure MirrorNetworkManager exists in the current scene.
        /// </summary>
        [MenuItem("Tools/PetGame/Add MirrorNetworkManager to Scene")]
        public static void AddToScene()
        {
            // Check if already exists in scene
            MirrorNetworkManager existing = Object.FindObjectOfType<MirrorNetworkManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Already Exists",
                    "A MirrorNetworkManager already exists in the current scene.",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Try to load from prefab
            string prefabPath = $"{PREFAB_DIR}/{PREFAB_NAME}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add MirrorNetworkManager");
                Selection.activeGameObject = instance;
                Debug.Log("[CreateMirrorNetworkManagerPrefab] Added MirrorNetworkManager to scene from prefab.");
            }
            else
            {
                // Create fresh if no prefab exists
                GameObject managerObj = new GameObject("[MirrorNetworkManager]");
                managerObj.AddComponent<MirrorNetworkManager>();
                managerObj.AddComponent<FizzySteamworks>();
                Undo.RegisterCreatedObjectUndo(managerObj, "Add MirrorNetworkManager");
                Selection.activeGameObject = managerObj;
                Debug.Log("[CreateMirrorNetworkManagerPrefab] Created fresh MirrorNetworkManager in scene.");
            }
        }
    }
}
