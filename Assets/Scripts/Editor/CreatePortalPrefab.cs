using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create a Portal prefab template with all required components.
    /// Accessible via the Unity menu: Tools > PetGame > Create Portal Prefab.
    ///
    /// The created prefab is a UI element (RectTransform) containing:
    ///   - Image: displays the portal sprite
    ///   - Animator: plays portal animations (default state: "Idle")
    ///   - AlphaHitTestImage: shape-based click detection using sprite alpha channel
    ///   - Button: receives click events
    ///   - PortalController: handles click/drag logic and animation control
    /// </summary>
    public static class CreatePortalPrefab
    {
        private const string PREFAB_DIR = "Assets/Resources/Prefabs/UI";
        private const string PREFAB_NAME = "PortalPrefab";

        [MenuItem("Tools/PetGame/Create Portal Prefab")]
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

            // Create the GameObject template with RectTransform (UI element)
            GameObject template = new GameObject(PREFAB_NAME, typeof(RectTransform));

            // Configure RectTransform
            RectTransform rectTransform = template.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 100f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // Add Image — for displaying the portal sprite
            Image image = template.AddComponent<Image>();
            image.raycastTarget = true; // Required for click/drag detection

            // Add Animator — for portal animations
            // User will assign an AnimatorController with an "Idle" default state
            template.AddComponent<Animator>();

            // Add AlphaHitTestImage — shape-based click detection using sprite alpha
            // Requires the sprite texture to have Read/Write Enabled
            AlphaHitTestImage alphaHit = template.AddComponent<AlphaHitTestImage>();
            alphaHit.alphaThreshold = 0.5f;

            // Add Button — receives click events
            Button button = template.AddComponent<Button>();
            button.targetGraphic = image;

            // Set button navigation to None to avoid unexpected keyboard/gamepad navigation
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            // Set button transition to None (portal uses its own Animator)
            button.transition = Selectable.Transition.None;

            // Add PortalController — handles click/drag logic and animation control
            PortalController portalController = template.AddComponent<PortalController>();

            // Wire up Button.OnClick to PortalController.OnPortalClicked
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick,
                portalController.OnPortalClicked);

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);

            // Clean up the temporary scene object
            Object.DestroyImmediate(template);

            if (prefab != null)
            {
                // Select the newly created prefab in the Project window
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                // Try to auto-assign the prefab to PortalSettings if it exists
                TryAssignToPortalSettings(prefab);

                EditorUtility.DisplayDialog(
                    "Portal Prefab Created",
                    $"Portal prefab created at:\n{prefabPath}\n\n" +
                    "Next steps:\n" +
                    "1. Assign a portal sprite to the Image component\n" +
                    "2. Create an AnimatorController with:\n" +
                    "   - An 'Idle' default animation state\n" +
                    "3. Assign the AnimatorController to the Animator component\n" +
                    "4. Ensure the sprite texture has 'Read/Write Enabled' checked\n" +
                    "5. The prefab has been auto-assigned to PortalSettings (if found)\n" +
                    "   Otherwise, drag it into PortalSettings.portalPrefab field",
                    "OK");

                Debug.Log($"[CreatePortalPrefab] Portal prefab created at: {prefabPath}");
            }
            else
            {
                Debug.LogError("[CreatePortalPrefab] Failed to create portal prefab.");
            }
        }

        /// <summary>
        /// Attempts to find the PortalSettings asset in Resources and auto-assign the portal prefab.
        /// </summary>
        private static void TryAssignToPortalSettings(GameObject prefab)
        {
            // Search for PortalSettings asset in the project
            string[] guids = AssetDatabase.FindAssets("t:PortalSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                PortalSettings settings = AssetDatabase.LoadAssetAtPath<PortalSettings>(path);
                if (settings != null)
                {
                    settings.portalPrefab = prefab;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[CreatePortalPrefab] Auto-assigned portal prefab to PortalSettings at: {path}");
                }
            }
        }
    }
}
