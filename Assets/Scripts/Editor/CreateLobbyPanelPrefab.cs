using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;
using System.Reflection;
using PetGame.UI;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create the LobbyPanel UI prefab with all required elements.
    /// Accessible via: Tools > PetGame > Create LobbyPanel Prefab
    /// </summary>
    public static class CreateLobbyPanelPrefab
    {
        private const string PREFAB_DIR = "Assets/Resources/Prefabs/UI";
        private const string PREFAB_NAME = "LobbyPanel";

        [MenuItem("Tools/PetGame/Create LobbyPanel Prefab")]
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

            // Create root LobbyPanel GameObject
            GameObject root = new GameObject(PREFAB_NAME, typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetStretch(rootRect);

            // Add CanvasRenderer for background
            // Add semi-transparent background overlay
            GameObject bgObj = CreateUIElement("Background", root.transform);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.7f);
            bgImage.raycastTarget = true;
            SetStretch(bgObj.GetComponent<RectTransform>());

            // ===== Initial Panel =====
            GameObject initialPanel = CreateUIElement("InitialPanel", root.transform);
            RectTransform initialRect = initialPanel.GetComponent<RectTransform>();
            SetCentered(initialRect, 420f, 380f);
            Image initialBg = initialPanel.AddComponent<Image>();
            initialBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // Add Vertical Layout Group
            VerticalLayoutGroup initialLayout = initialPanel.AddComponent<VerticalLayoutGroup>();
            initialLayout.padding = new RectOffset(25, 25, 25, 25);
            initialLayout.spacing = 15f;
            initialLayout.childAlignment = TextAnchor.UpperCenter;
            initialLayout.childControlWidth = true;
            initialLayout.childControlHeight = false;
            initialLayout.childForceExpandWidth = true;
            initialLayout.childForceExpandHeight = false;

            // Title
            GameObject titleObj = CreateTMPText("Title", initialPanel.transform, "Multiplayer", 28, TextAlignmentOptions.Center, Color.white);
            SetLayoutHeight(titleObj, 40f);

            // Create Lobby Button
            GameObject createLobbyBtnObj = CreateTMPButton("CreateLobbyButton", initialPanel.transform, "Create Lobby");
            SetLayoutHeight(createLobbyBtnObj, 50f);
            Button createLobbyButton = createLobbyBtnObj.GetComponent<Button>();

            // Separator
            GameObject separator = CreateUIElement("Separator", initialPanel.transform);
            Image sepImage = separator.AddComponent<Image>();
            sepImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            SetLayoutHeight(separator, 2f);

            // "Or join a lobby" label
            GameObject orLabel = CreateTMPText("OrLabel", initialPanel.transform, "— or join a lobby —", 14, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.6f));
            SetLayoutHeight(orLabel, 25f);

            // Join Code Input Field
            GameObject joinCodeInputObj = CreateTMPInputField("JoinCodeInput", initialPanel.transform, "Enter Lobby Code...");
            SetLayoutHeight(joinCodeInputObj, 50f);
            TMP_InputField joinCodeInput = joinCodeInputObj.GetComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 6;

            // Join Button
            GameObject joinBtnObj = CreateTMPButton("JoinButton", initialPanel.transform, "Join");
            SetLayoutHeight(joinBtnObj, 50f);
            Button joinButton = joinBtnObj.GetComponent<Button>();
            // Set join button color slightly different
            Image joinBtnImage = joinBtnObj.GetComponent<Image>();
            joinBtnImage.color = new Color(0.2f, 0.5f, 0.8f, 1f);

            // Close Button (positioned at top-right, outside layout)
            GameObject closeBtnObj = CreateTMPButton("CloseButton", initialPanel.transform, "✕");
            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            // Remove layout element so it doesn't participate in vertical layout
            LayoutElement closeLE = closeBtnObj.GetComponent<LayoutElement>();
            if (closeLE != null) Object.DestroyImmediate(closeLE);
            closeLE = closeBtnObj.AddComponent<LayoutElement>();
            closeLE.ignoreLayout = true;
            // Position at top-right corner
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-5f, -5f);
            closeRect.sizeDelta = new Vector2(35f, 35f);
            Button closeButton = closeBtnObj.GetComponent<Button>();
            Image closeBtnImage = closeBtnObj.GetComponent<Image>();
            closeBtnImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            // ===== In-Lobby Panel =====
            GameObject lobbyPanelObj = CreateUIElement("LobbyPanel_InLobby", root.transform);
            RectTransform lobbyRect = lobbyPanelObj.GetComponent<RectTransform>();
            SetCentered(lobbyRect, 420f, 320f);
            Image lobbyBg = lobbyPanelObj.AddComponent<Image>();
            lobbyBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            VerticalLayoutGroup lobbyLayout = lobbyPanelObj.AddComponent<VerticalLayoutGroup>();
            lobbyLayout.padding = new RectOffset(25, 25, 25, 25);
            lobbyLayout.spacing = 15f;
            lobbyLayout.childAlignment = TextAnchor.UpperCenter;
            lobbyLayout.childControlWidth = true;
            lobbyLayout.childControlHeight = false;
            lobbyLayout.childForceExpandWidth = true;
            lobbyLayout.childForceExpandHeight = false;

            // Player Count Text
            GameObject playerCountObj = CreateTMPText("PlayerCountText", lobbyPanelObj.transform, "Lobby (1/2)", 18, TextAlignmentOptions.Center, Color.white);
            SetLayoutHeight(playerCountObj, 30f);
            TextMeshProUGUI playerCountText = playerCountObj.GetComponent<TextMeshProUGUI>();

            // Code Row (Horizontal: LobbyCodeText + CopyButton)
            GameObject codeRow = CreateUIElement("CodeRow", lobbyPanelObj.transform);
            HorizontalLayoutGroup codeRowLayout = codeRow.AddComponent<HorizontalLayoutGroup>();
            codeRowLayout.spacing = 10f;
            codeRowLayout.childAlignment = TextAnchor.MiddleCenter;
            codeRowLayout.childControlWidth = false;
            codeRowLayout.childControlHeight = true;
            codeRowLayout.childForceExpandWidth = false;
            codeRowLayout.childForceExpandHeight = true;
            SetLayoutHeight(codeRow, 50f);

            // Lobby Code Text
            GameObject lobbyCodeObj = CreateTMPText("LobbyCodeText", codeRow.transform, "ABCDEF", 36, TextAlignmentOptions.Center, new Color(0.3f, 0.9f, 0.4f));
            RectTransform lobbyCodeRect = lobbyCodeObj.GetComponent<RectTransform>();
            lobbyCodeRect.sizeDelta = new Vector2(250f, 50f);
            LayoutElement lobbyCodeLE = lobbyCodeObj.AddComponent<LayoutElement>();
            lobbyCodeLE.preferredWidth = 250f;
            TextMeshProUGUI lobbyCodeText = lobbyCodeObj.GetComponent<TextMeshProUGUI>();
            lobbyCodeText.fontStyle = FontStyles.Bold;

            // Copy Code Button
            GameObject copyBtnObj = CreateTMPButton("CopyCodeButton", codeRow.transform, "📋 Copy");
            RectTransform copyBtnRect = copyBtnObj.GetComponent<RectTransform>();
            copyBtnRect.sizeDelta = new Vector2(100f, 40f);
            LayoutElement copyBtnLE = copyBtnObj.GetComponent<LayoutElement>();
            if (copyBtnLE == null) copyBtnLE = copyBtnObj.AddComponent<LayoutElement>();
            copyBtnLE.preferredWidth = 100f;
            Button copyCodeButton = copyBtnObj.GetComponent<Button>();

            // Status Text
            GameObject statusObj = CreateTMPText("StatusText", lobbyPanelObj.transform, "Waiting for player...", 16, TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));
            SetLayoutHeight(statusObj, 25f);
            TextMeshProUGUI statusText = statusObj.GetComponent<TextMeshProUGUI>();

            // Leave Lobby Button
            GameObject leaveBtnObj = CreateTMPButton("LeaveLobbyButton", lobbyPanelObj.transform, "Leave Lobby");
            SetLayoutHeight(leaveBtnObj, 50f);
            Image leaveBtnImage = leaveBtnObj.GetComponent<Image>();
            leaveBtnImage.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            Button leaveLobbyButton = leaveBtnObj.GetComponent<Button>();

            // Set In-Lobby panel inactive by default
            lobbyPanelObj.SetActive(false);

            // ===== Error Text =====
            GameObject errorObj = CreateTMPText("ErrorText", root.transform, "", 14, TextAlignmentOptions.Center, new Color(1f, 0.3f, 0.3f));
            RectTransform errorRect = errorObj.GetComponent<RectTransform>();
            // Position at bottom center
            errorRect.anchorMin = new Vector2(0.5f, 0f);
            errorRect.anchorMax = new Vector2(0.5f, 0f);
            errorRect.pivot = new Vector2(0.5f, 0f);
            errorRect.anchoredPosition = new Vector2(0f, 50f);
            errorRect.sizeDelta = new Vector2(400f, 40f);
            TextMeshProUGUI errorText = errorObj.GetComponent<TextMeshProUGUI>();
            errorObj.SetActive(false);

            // ===== Add LobbyPanel script and wire references =====
            LobbyPanel lobbyPanelScript = root.AddComponent<LobbyPanel>();

            // Use reflection to set private serialized fields
            System.Type type = typeof(LobbyPanel);
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            SetField(type, lobbyPanelScript, "initialPanel", initialPanel, flags);
            SetField(type, lobbyPanelScript, "createLobbyButton", createLobbyButton, flags);
            SetField(type, lobbyPanelScript, "joinCodeInput", joinCodeInput, flags);
            SetField(type, lobbyPanelScript, "joinButton", joinButton, flags);
            SetField(type, lobbyPanelScript, "closeButton", closeButton, flags);
            SetField(type, lobbyPanelScript, "lobbyPanel", lobbyPanelObj, flags);
            SetField(type, lobbyPanelScript, "lobbyCodeText", lobbyCodeText, flags);
            SetField(type, lobbyPanelScript, "copyCodeButton", copyCodeButton, flags);
            SetField(type, lobbyPanelScript, "playerCountText", playerCountText, flags);
            SetField(type, lobbyPanelScript, "leaveLobbyButton", leaveLobbyButton, flags);
            SetField(type, lobbyPanelScript, "statusText", statusText, flags);
            SetField(type, lobbyPanelScript, "errorText", errorText, flags);
            SetField(type, lobbyPanelScript, "errorDisplayDuration", 3f, flags);

            // ===== Save as Prefab =====
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                EditorUtility.DisplayDialog(
                    "LobbyPanel Prefab Created",
                    $"LobbyPanel prefab created at:\n{prefabPath}\n\n" +
                    "Next steps:\n" +
                    "1. Drag this prefab into your MainView Canvas as a child\n" +
                    "2. In MainView Inspector, assign the LobbyPanel reference\n" +
                    "3. All internal SerializeField references are already configured!",
                    "OK");

                Debug.Log($"[CreateLobbyPanelPrefab] LobbyPanel prefab created at: {prefabPath}");
            }
            else
            {
                Debug.LogError("[CreateLobbyPanelPrefab] Failed to create LobbyPanel prefab.");
            }
        }

        // --- Helper Methods ---

        private static void SetField(System.Type type, object target, string fieldName, object value, BindingFlags flags)
        {
            FieldInfo field = type.GetField(fieldName, flags);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[CreateLobbyPanelPrefab] Could not find field: {fieldName}");
            }
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static GameObject CreateTMPText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject obj = CreateUIElement(name, parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return obj;
        }

        private static GameObject CreateTMPButton(string name, Transform parent, string label)
        {
            GameObject btnObj = CreateUIElement(name, parent);
            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            // Button navigation none
            Navigation nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            // Button label
            GameObject labelObj = CreateUIElement("Text", btnObj.transform);
            TextMeshProUGUI labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 18;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.color = Color.white;
            labelTMP.raycastTarget = false;
            SetStretch(labelObj.GetComponent<RectTransform>());

            return btnObj;
        }

        private static GameObject CreateTMPInputField(string name, Transform parent, string placeholder)
        {
            GameObject inputObj = CreateUIElement(name, parent);
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Text Area
            GameObject textArea = CreateUIElement("Text Area", inputObj.transform);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            SetStretch(textAreaRect);
            textAreaRect.offsetMin = new Vector2(10f, 5f);
            textAreaRect.offsetMax = new Vector2(-10f, -5f);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            GameObject placeholderObj = CreateUIElement("Placeholder", textArea.transform);
            TextMeshProUGUI placeholderTMP = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = placeholder;
            placeholderTMP.fontSize = 16;
            placeholderTMP.fontStyle = FontStyles.Italic;
            placeholderTMP.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            placeholderTMP.alignment = TextAlignmentOptions.MidlineLeft;
            SetStretch(placeholderObj.GetComponent<RectTransform>());

            // Text
            GameObject textObj = CreateUIElement("Text", textArea.transform);
            TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
            textTMP.text = "";
            textTMP.fontSize = 16;
            textTMP.color = Color.white;
            textTMP.alignment = TextAlignmentOptions.MidlineLeft;
            SetStretch(textObj.GetComponent<RectTransform>());

            // TMP_InputField component
            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textTMP;
            inputField.placeholder = placeholderTMP;
            inputField.fontAsset = textTMP.font;

            return inputObj;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetCentered(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetLayoutHeight(GameObject obj, float height)
        {
            LayoutElement le = obj.GetComponent<LayoutElement>();
            if (le == null) le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
