using UnityEngine;
using UnityEditor;
using Mirror;

namespace PetGame.Network.Editor
{
    /// <summary>
    /// Editor utility to create the NetworkPlayer prefab required by MirrorNetworkManager.
    /// </summary>
    public static class CreateNetworkPlayerPrefab
    {
        [MenuItem("Tools/PetGame/Create NetworkPlayer Prefab")]
        public static void Create()
        {
            // Ensure directory exists
            string directory = "Assets/Resources/Prefabs/Network";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Network");

            string prefabPath = $"{directory}/NetworkPlayer.prefab";

            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                    "NetworkPlayer prefab already exists. Overwrite?", "Yes", "No"))
                {
                    return;
                }
            }

            // Create the GameObject
            GameObject playerObj = new GameObject("NetworkPlayer");

            // Add NetworkIdentity (required for all networked objects)
            NetworkIdentity netId = playerObj.AddComponent<NetworkIdentity>();

            // Add NetworkPlayer component
            playerObj.AddComponent<NetworkPlayer>();

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerObj, prefabPath);
            Object.DestroyImmediate(playerObj);

            if (prefab != null)
            {
                // Auto-assign to MirrorNetworkManager if it exists in the scene
                MirrorNetworkManager manager = Object.FindObjectOfType<MirrorNetworkManager>();
                if (manager != null)
                {
                    SerializedObject so = new SerializedObject(manager);
                    SerializedProperty playerPrefabProp = so.FindProperty("playerPrefab");
                    if (playerPrefabProp != null)
                    {
                        playerPrefabProp.objectReferenceValue = prefab;
                        so.ApplyModifiedProperties();
                        Debug.Log("[CreateNetworkPlayerPrefab] Auto-assigned to MirrorNetworkManager in scene.");
                    }
                }

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = prefab;
                Debug.Log($"[CreateNetworkPlayerPrefab] Created prefab at: {prefabPath}");
                EditorUtility.DisplayDialog("Success",
                    $"NetworkPlayer prefab created at:\n{prefabPath}\n\n" +
                    "Assign it to MirrorNetworkManager's Player Prefab field.",
                    "OK");
            }
        }
    }
}
