using UnityEngine;
using UnityEditor;
using System.IO;

namespace PetGame.DesktopWindow.Editor
{
    /// <summary>
    /// Editor utility to create the ClickThroughManager prefab.
    /// Menu: Tools > PetGame > Create ClickThroughManager Prefab
    /// </summary>
    public static class ClickThroughManagerPrefabCreator
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/System/ClickThroughManager.prefab";

        [MenuItem("Tools/PetGame/Create ClickThroughManager Prefab")]
        public static void CreatePrefab()
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Check if prefab already exists
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab != null)
            {
                if (!EditorUtility.DisplayDialog(
                    "Prefab Already Exists",
                    "ClickThroughManager prefab already exists. Do you want to overwrite it?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // Create a temporary GameObject with ClickThroughManager component
            GameObject tempGO = new GameObject("ClickThroughManager");
            tempGO.AddComponent<ClickThroughManager>();

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, PrefabPath);

            // Clean up temporary object
            Object.DestroyImmediate(tempGO);

            if (prefab != null)
            {
                Debug.Log($"[ClickThroughManagerPrefabCreator] Prefab created at: {PrefabPath}");
                Debug.Log("[ClickThroughManagerPrefabCreator] You can now select the prefab and configure parameters in the Inspector.");
                
                // Select the created prefab in Project window
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[ClickThroughManagerPrefabCreator] Failed to create prefab!");
            }
        }

        [MenuItem("Tools/PetGame/Select ClickThroughManager Prefab")]
        public static void SelectPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Prefab Not Found",
                    "ClickThroughManager prefab not found. Use 'Tools > PetGame > Create ClickThroughManager Prefab' to create it first.",
                    "OK");
            }
        }
    }
}
