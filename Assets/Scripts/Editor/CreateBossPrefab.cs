using UnityEngine;
using UnityEditor;
using System.IO;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create a Boss prefab template with all required components.
    /// Accessible via the Unity menu: Tools > PetGame > Create Boss Prefab.
    ///
    /// The created prefab mirrors the PlayerPrefab component structure:
    ///   - SpriteRenderer, Animator, BoxCollider2D, Rigidbody2D
    ///   - CharacterEntity, CharacterAnimator, CombatSystem
    ///   - AIController, FlashEffect, AnimEventReceiver
    /// </summary>
    public static class CreateBossPrefab
    {
        private const string PREFAB_DIR = "Assets/Resources/Prefabs/Characters";
        private const string PREFAB_NAME = "BossPrefab";
        private const string CHARACTER_LAYER_NAME = "Character";

        [MenuItem("Tools/PetGame/Create Boss Prefab")]
        public static void CreatePrefab()
        {
            // Ensure the directory exists
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

            // Create the GameObject template
            GameObject template = new GameObject(PREFAB_NAME);

            // Set Tag and Layer
            template.tag = "Enemy";
            int layerIndex = LayerMask.NameToLayer(CHARACTER_LAYER_NAME);
            if (layerIndex >= 0)
            {
                template.layer = layerIndex;
            }
            else
            {
                Debug.LogWarning($"[CreateBossPrefab] Layer '{CHARACTER_LAYER_NAME}' not found. Please create it in Edit > Project Settings > Tags and Layers.");
            }

            // Add SpriteRenderer
            SpriteRenderer sr = template.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            // Add Animator
            template.AddComponent<Animator>();

            // Add BoxCollider2D
            BoxCollider2D col = template.AddComponent<BoxCollider2D>();
            col.isTrigger = false;

            // Add Rigidbody2D
            Rigidbody2D rb = template.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Add CharacterEntity
            template.AddComponent<CharacterEntity>();

            // Add CharacterAnimator
            template.AddComponent<CharacterAnimator>();

            // Add CombatSystem
            template.AddComponent<CombatSystem>();

            // Add AIController
            template.AddComponent<PetGame.AI.AIController>();

            // Add FlashEffect
            template.AddComponent<FlashEffect>();

            // Add AnimEventReceiver
            template.AddComponent<AnimEventReceiver>();

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);

            // Clean up the temporary scene object
            Object.DestroyImmediate(template);

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                EditorUtility.DisplayDialog(
                    "Boss Prefab Created",
                    $"Boss prefab created at:\n{prefabPath}\n\n" +
                    "Next steps:\n" +
                    "1. Create a Boss CharacterData asset (type = Boss)\n" +
                    "2. Assign a sprite and AnimatorController\n" +
                    "3. Register the prefab name 'BossPrefab' in PoolMgr if needed",
                    "OK");

                Debug.Log($"[CreateBossPrefab] Boss prefab created at: {prefabPath}");
            }
            else
            {
                Debug.LogError("[CreateBossPrefab] Failed to create boss prefab.");
            }
        }
    }
}
