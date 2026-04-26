using UnityEngine;
using UnityEngine.UI;

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

            // Instantiate portal as child of the Canvas
            GameObject portal = Instantiate(settings.portalPrefab, portalCanvas.transform);
            portal.name = "Portal";

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
