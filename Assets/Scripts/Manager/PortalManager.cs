using UnityEngine;
using UnityEngine.UI;
using Mirror;
using PetGame.Network;


namespace PetGame
{
    /// <summary>
    /// Manages portal spawning and the "Spawn Portal" UI button.
    /// Attach this MonoBehaviour to a GameObject in the scene.
    ///
    /// Setup:
    ///   1. Create a PortalSettings asset via Assets > Create > Game > PortalSettings
    ///      and place it in a Resources folder.
    ///   2. Assign the portal Canvas reference in the Inspector.
    ///   3. The spawn button is created dynamically at runtime.
    /// </summary>
    public class PortalManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Canvas where portals and the spawn button will be placed")]
        [SerializeField] private Canvas portalCanvas;

        private Button _spawnButton;
        private RectTransform _canvasRect;

        void Start()
        {
            if (portalCanvas == null)
            {
                Debug.LogError("[PortalManager] Portal Canvas is not assigned! Please assign a Canvas in the Inspector.");
                return;
            }

            _canvasRect = portalCanvas.GetComponent<RectTransform>();

            CreateSpawnButton();
        }

        // =====================================================================
        // Spawn Button Creation
        // =====================================================================

        /// <summary>
        /// Dynamically creates the "Spawn Portal" button on the Canvas.
        /// </summary>
        private void CreateSpawnButton()
        {
            PortalSettings settings = PortalSettings.Instance;

            // Create button GameObject
            GameObject buttonObj = new GameObject("SpawnPortalButton");
            buttonObj.transform.SetParent(portalCanvas.transform, false);

            // RectTransform setup
            RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
            btnRect.anchorMin = settings.spawnButtonPosition;
            btnRect.anchorMax = settings.spawnButtonPosition;
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(200f, 50f);

            // Background Image
            Image btnImage = buttonObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Button component
            _spawnButton = buttonObj.AddComponent<Button>();
            _spawnButton.targetGraphic = btnImage;

            // Set button colors
            ColorBlock colors = _spawnButton.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            _spawnButton.colors = colors;

            // Add click listener
            _spawnButton.onClick.AddListener(OnSpawnButtonClicked);

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text btnText = textObj.AddComponent<Text>();
            btnText.text = "点击生成传送门";
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 20;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // =====================================================================
        // Portal Spawning
        // =====================================================================

        /// <summary>
        /// Called when the spawn button is clicked.
        /// Instantiates a portal prefab at a random position within the configured spawn area.
        /// In multiplayer mode, clients send a request to the server to spawn the portal.
        /// </summary>
        private void OnSpawnButtonClicked()
        {
            PortalSettings settings = PortalSettings.Instance;

            if (settings.portalPrefab == null)
            {
                Debug.LogWarning("[PortalManager] Portal prefab is not assigned in PortalSettings!");
                return;
            }

            // Generate random world position within the spawn area
            float randomX = Random.Range(
                settings.spawnAreaCenter.x - settings.spawnAreaSize.x * 0.5f,
                settings.spawnAreaCenter.x + settings.spawnAreaSize.x * 0.5f);
            float randomY = Random.Range(
                settings.spawnAreaCenter.y - settings.spawnAreaSize.y * 0.5f,
                settings.spawnAreaCenter.y + settings.spawnAreaSize.y * 0.5f);

            Vector3 worldPos = new Vector3(randomX, randomY, 0f);

            // Check if we are in multiplayer mode
            bool isMultiplayer = NetworkClient.active;

            if (isMultiplayer)
            {
                // In multiplayer mode, request the server to spawn the portal
                // Both host and client go through the same Command path
                PetGame.Network.NetworkPlayer localPlayer = MirrorNetworkManager.singleton?.LocalPlayer;
                if (localPlayer != null)
                {
                    localPlayer.RequestSpawnPortal(worldPos);
                }
                else
                {
                    Debug.LogWarning("[PortalManager] Local NetworkPlayer not found. Cannot spawn portal in multiplayer.");
                }
            }
            else
            {
                // Single player mode: spawn directly
                SpawnPortalLocally(worldPos);
            }
        }

        /// <summary>
        /// Spawn a portal locally on this client's Canvas.
        /// In multiplayer mode, the portalId is assigned by the server for network identification.
        /// In single player mode, portalId is 0 (not used).
        /// levelDataIndex is the index into SpecialLevelListManager.registeredLevelData.
        /// </summary>
        public void SpawnPortalLocally(Vector3 worldPos, uint portalId = 0, int levelDataIndex = -1)
        {
            if (portalCanvas == null) return;
            if (_canvasRect == null) _canvasRect = portalCanvas.GetComponent<RectTransform>();
            PortalSettings settings = PortalSettings.Instance;
            if (settings.portalPrefab == null) return;

            // Instantiate portal as child of the Canvas
            GameObject portal = Instantiate(settings.portalPrefab, portalCanvas.transform);
            portal.name = portalId > 0 ? $"Portal_{portalId}" : "Portal";

            // Set the portal ID for network identification
            PortalController controller = portal.GetComponent<PortalController>();
            if (controller != null)
            {
                controller.portalId = portalId;

                // Assign SpecialLevelData from the registry if a valid index was provided
                if (levelDataIndex >= 0)
                {
                    var levelListMgr = SpecialLevelListManager.Instance;
                    if (levelListMgr != null)
                    {
                        SpecialLevelData levelData = levelListMgr.GetLevelData(levelDataIndex);
                        if (levelData != null)
                        {
                            controller.specialLevelData = levelData;
                        }
                        else
                        {
                            Debug.LogWarning($"[PortalManager] Invalid levelDataIndex={levelDataIndex}. SpecialLevelData not assigned.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[PortalManager] SpecialLevelListManager not found. SpecialLevelData not assigned.");
                    }
                }
            }

            // Convert world position to Canvas local position
            RectTransform portalRect = portal.GetComponent<RectTransform>();
            if (portalRect != null && _canvasRect != null)
            {
                Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPos);
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    portalCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : portalCanvas.worldCamera,
                    out localPoint))
                {
                    portalRect.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }

            Debug.Log($"[PortalManager] Portal spawned locally at world position {worldPos}, portalId={portalId}.");
        }

        // =====================================================================
        // Editor Gizmos — Spawn Area Visualization
        // =====================================================================

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            PortalSettings settings = PortalSettings.Instance;
            if (settings == null) return;

            // Draw semi-transparent cyan rectangle for the spawn area
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Vector3 center = new Vector3(settings.spawnAreaCenter.x, settings.spawnAreaCenter.y, 0f);
            Vector3 size = new Vector3(settings.spawnAreaSize.x, settings.spawnAreaSize.y, 0f);
            Gizmos.DrawCube(center, size);

            // Draw wireframe border
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
