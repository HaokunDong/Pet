using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.IO;

namespace PetGame.EditorTools
{
    /// <summary>
    /// Editor utility to create the NetworkPlayerPrefab required for multiplayer.
    /// This prefab must have a NetworkObject component with a valid GlobalObjectIdHash.
    /// </summary>
    public static class CreateNetworkPlayerPrefab
    {
        private const string PREFAB_PATH = "Assets/Resources/Prefabs/Network/NetworkPlayerPrefab.prefab";

        [MenuItem("Tools/PetGame/Create Network Player Prefab")]
        public static void CreatePrefab()
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(PREFAB_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Check if prefab already exists
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existingPrefab != null)
            {
                if (!EditorUtility.DisplayDialog("Prefab Exists",
                    "NetworkPlayerPrefab already exists. Do you want to recreate it?",
                    "Yes", "No"))
                {
                    return;
                }
            }

            // Create a new GameObject
            GameObject playerObj = new GameObject("NetworkPlayerPrefab");

            // Add required components
            // SpriteRenderer for visual representation
            SpriteRenderer sr = playerObj.AddComponent<SpriteRenderer>();

            // Animator for animations
            Animator animator = playerObj.AddComponent<Animator>();

            // BoxCollider2D for physics
            BoxCollider2D collider = playerObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.92f, 0.81f);
            collider.offset = new Vector2(0.036f, 0.019f);

            // Rigidbody2D for physics
            Rigidbody2D rb = playerObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;

            // NetworkObject - REQUIRED for Netcode
            NetworkObject networkObject = playerObj.AddComponent<NetworkObject>();

            // NetworkPlayerSetup - handles initialization after spawn
            playerObj.AddComponent<PetGame.Network.NetworkPlayerSetup>();

            // Set layer to Character (layer 8)
            playerObj.layer = 8;
            playerObj.tag = "Player";

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerObj, PREFAB_PATH);

            // Cleanup the scene object
            Object.DestroyImmediate(playerObj);

            if (prefab != null)
            {
                Debug.Log($"[CreateNetworkPlayerPrefab] Successfully created prefab at: {PREFAB_PATH}");
                Debug.Log("[CreateNetworkPlayerPrefab] The prefab has NetworkObject component with valid GlobalObjectIdHash.");
                Debug.Log("[CreateNetworkPlayerPrefab] Remember to add this prefab to NetworkManager's NetworkPrefab list if not using runtime registration.");

                // Select the created prefab in the Project window
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[CreateNetworkPlayerPrefab] Failed to create prefab!");
            }
        }

        [MenuItem("Tools/PetGame/Validate Network Player Prefab")]
        public static void ValidatePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[Validate] NetworkPlayerPrefab not found at: {PREFAB_PATH}. Run 'Tools/PetGame/Create Network Player Prefab' first.");
                return;
            }

            NetworkObject netObj = prefab.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[Validate] NetworkPlayerPrefab is missing NetworkObject component!");
                return;
            }

            Debug.Log($"[Validate] NetworkPlayerPrefab is valid. NetworkObject found on prefab.");
            Debug.Log($"[Validate] Components: {string.Join(", ", System.Array.ConvertAll(prefab.GetComponents<Component>(), c => c.GetType().Name))}");
        }
    }
}
