using UnityEngine;
using UnityEditor;
using System.IO;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create a ProjectileBall prefab template with all required components.
    /// Accessible via the Unity menu: Tools > PetGame > Create Projectile Prefab.
    /// </summary>
    public static class CreateProjectilePrefab
    {
        private const string PREFAB_DIR = "Assets/Resources/Prefabs/Entity/Characters";
        private const string PREFAB_NAME = "ProjectileBall";

        [MenuItem("Tools/PetGame/Create Projectile Prefab")]
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

            // Add SpriteRenderer — for displaying the projectile sprite
            SpriteRenderer sr = template.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10; // Render above characters

            // Add Animator — for flight and explosion animations
            // User will assign an AnimatorController with states:
            //   - Default/Idle state (flight appearance)
            //   - "Explode" trigger → Explosion animation state
            template.AddComponent<Animator>();

            // Add ProjectileController — handles flight + explosion logic
            template.AddComponent<ProjectileController>();

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);

            // Clean up the temporary scene object
            Object.DestroyImmediate(template);

            if (prefab != null)
            {
                // Select the newly created prefab in the Project window
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                EditorUtility.DisplayDialog(
                    "Prefab Created",
                    $"Projectile prefab created at:\n{prefabPath}\n\n" +
                    "Next steps:\n" +
                    "1. Assign a sprite to the SpriteRenderer\n" +
                    "2. Create an AnimatorController with:\n" +
                    "   - A default state for the flight appearance\n" +
                    "   - An 'Explode' trigger parameter\n" +
                    "   - A transition to an explosion animation state\n" +
                    "3. Assign the AnimatorController to the Animator component\n" +
                    "4. Drag this prefab into your ProjectileSkillEffectData asset",
                    "OK");

                Debug.Log($"[CreateProjectilePrefab] Prefab created at: {prefabPath}");
            }
            else
            {
                Debug.LogError("[CreateProjectilePrefab] Failed to create prefab.");
            }
        }
    }
}
