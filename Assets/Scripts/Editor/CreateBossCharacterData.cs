using UnityEngine;
using UnityEditor;
using System.IO;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create a Boss CharacterData ScriptableObject asset.
    /// Accessible via the Unity menu: Tools > PetGame > Create Boss CharacterData.
    ///
    /// Creates a pre-configured CharacterData asset with:
    ///   - characterType = CharacterType.Boss
    ///   - Higher base stats (HP, ATK, DEF) suitable for a Boss encounter
    /// </summary>
    public static class CreateBossCharacterData
    {
        private const string ASSET_DIR = "Assets/Resources/Data/Characters";
        private const string ASSET_NAME = "BossCharacterData";

        [MenuItem("Tools/PetGame/Create Boss CharacterData")]
        public static void CreateAsset()
        {
            // Ensure the directory exists
            if (!Directory.Exists(ASSET_DIR))
            {
                Directory.CreateDirectory(ASSET_DIR);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{ASSET_DIR}/{ASSET_NAME}.asset";

            // Check if asset already exists
            if (File.Exists(assetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Asset Already Exists",
                    $"A CharacterData asset named '{ASSET_NAME}' already exists at:\n{assetPath}\n\nDo you want to overwrite it?",
                    "Overwrite",
                    "Cancel");

                if (!overwrite) return;
            }

            // Create the ScriptableObject
            CharacterData bossData = ScriptableObject.CreateInstance<CharacterData>();

            // Configure Boss-specific defaults
            bossData.characterId = "boss_001";
            bossData.characterName = "Boss";
            bossData.characterType = CharacterType.Boss;

            // Boss stats — significantly higher than normal enemies
            bossData.maxHealth = 500f;
            bossData.attackPower = 30f;
            bossData.defense = 15f;
            bossData.moveSpeed = 2f;
            bossData.attackSpeed = 0.8f;
            bossData.minAttackDistance = 0.4f;
            bossData.attackDistance = 1.5f;

            // Knockback settings — Boss is heavier, less knockback
            bossData.knockbackHorizontalSpeed = 1.0f;
            bossData.knockbackVerticalSpeed = 0.8f;
            bossData.knockbackGravity = 10.0f;

            // Quality level — Boss gets more skills
            bossData.qualityLevel = QualityLevel.SS;

            // Save asset
            AssetDatabase.CreateAsset(bossData, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the newly created asset
            Selection.activeObject = bossData;
            EditorGUIUtility.PingObject(bossData);

            EditorUtility.DisplayDialog(
                "Boss CharacterData Created",
                $"Boss CharacterData asset created at:\n{assetPath}\n\n" +
                "Next steps:\n" +
                "1. Assign a sprite and portrait sprite\n" +
                "2. Assign an AnimatorController\n" +
                "3. Configure attack distance\n" +
                "4. Add skills as needed\n" +
                "5. Assign this asset to BossFightManager.bossCharacterData",
                "OK");

            Debug.Log($"[CreateBossCharacterData] Boss CharacterData created at: {assetPath}");
        }
    }
}
