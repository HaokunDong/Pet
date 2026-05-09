#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Editor utility to create the CultivationPanel prefab with proper UI hierarchy.
    /// Menu: Game/Create Cultivation Panel Prefab
    /// </summary>
    public static class CultivationPanelBuilder
    {
        [MenuItem("Game/Create Cultivation Panel Prefab")]
        public static void CreateCultivationPanelPrefab()
        {
            // Create root panel
            GameObject root = new GameObject("CultivationPanel");
            RectTransform rootRect = root.AddComponent<RectTransform>();
            root.AddComponent<CanvasRenderer>();
            Image rootBg = root.AddComponent<Image>();
            rootBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Set root size (centered panel)
            rootRect.sizeDelta = new Vector2(320f, 480f);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;

            // Add VerticalLayoutGroup for auto-layout
            VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(15, 15, 15, 15);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Add ContentSizeFitter
            ContentSizeFitter csf = root.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // === Close Button (top-right corner) ===
            GameObject closeBtn = CreateButton(root.transform, "CloseBtn", "X", 30f, 30f);
            RectTransform closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1f, 1f);
            closeBtnRect.anchorMax = new Vector2(1f, 1f);
            closeBtnRect.pivot = new Vector2(1f, 1f);
            closeBtnRect.anchoredPosition = new Vector2(-5f, -5f);
            // Remove from layout
            LayoutElement closeLE = closeBtn.AddComponent<LayoutElement>();
            closeLE.ignoreLayout = true;

            // === Level Text ===
            GameObject levelObj = CreateText(root.transform, "LevelText", "Lv. 1", 28, TextAnchor.MiddleCenter, 35f);

            // === Talent Points Text ===
            GameObject talentObj = CreateText(root.transform, "TalentPointsText", "\u5929\u8d4b\u70b9: 0", 20, TextAnchor.MiddleCenter, 28f);

            // === Experience Bar ===
            GameObject expBarRoot = CreateExpBar(root.transform);

            // === Attribute Rows ===
            GameObject attackRow = CreateAttributeRow(root.transform, "AttackRow", "\u653b\u51fb");
            GameObject defenseRow = CreateAttributeRow(root.transform, "DefenseRow", "\u9632\u5fa1");
            GameObject healthRow = CreateAttributeRow(root.transform, "HealthRow", "\u751f\u547d");
            GameObject attackSpeedRow = CreateAttributeRow(root.transform, "AttackSpeedRow", "\u653b\u901f");
            GameObject moveSpeedRow = CreateAttributeRow(root.transform, "MoveSpeedRow", "\u79fb\u901f");
            GameObject skillCDRow = CreateAttributeRow(root.transform, "SkillCDRow", "\u6280\u80fdCD");

            // === Add CultivationPanelUI component and bind references ===
            CultivationPanelUI panelUI = root.AddComponent<CultivationPanelUI>();

            // Use SerializedObject to set private serialized fields
            SerializedObject so = new SerializedObject(panelUI);
            so.FindProperty("levelText").objectReferenceValue = levelObj.GetComponent<Text>();
            so.FindProperty("talentPointsText").objectReferenceValue = talentObj.GetComponent<Text>();
            so.FindProperty("expFillImage").objectReferenceValue = expBarRoot.transform.Find("Fill").GetComponent<Image>();
            so.FindProperty("expText").objectReferenceValue = expBarRoot.transform.Find("ExpText").GetComponent<Text>();

            // Bind attribute rows
            BindAttributeRow(so, "attack", attackRow);
            BindAttributeRow(so, "defense", defenseRow);
            BindAttributeRow(so, "health", healthRow);
            BindAttributeRow(so, "attackSpeed", attackSpeedRow);
            BindAttributeRow(so, "moveSpeed", moveSpeedRow);
            BindAttributeRow(so, "skillCD", skillCDRow);

            so.FindProperty("closeBtn").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            // Save as prefab
            string prefabPath = "Assets/Resources/Prefabs/UI/CultivationPanel.prefab";
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[CultivationPanelBuilder] Prefab created at: {prefabPath}");
            AssetDatabase.Refresh();
        }

        private static void BindAttributeRow(SerializedObject so, string prefix, GameObject row)
        {
            so.FindProperty($"{prefix}LevelText").objectReferenceValue = row.transform.Find("LevelText").GetComponent<Text>();
            so.FindProperty($"{prefix}BonusText").objectReferenceValue = row.transform.Find("BonusText").GetComponent<Text>();
            so.FindProperty($"{prefix}UpgradeBtn").objectReferenceValue = row.transform.Find("UpgradeBtn").GetComponent<Button>();
        }

        private static GameObject CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, float height)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, height);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            obj.AddComponent<CanvasRenderer>();
            Text txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            return obj;
        }

        private static GameObject CreateExpBar(Transform parent)
        {
            GameObject barRoot = new GameObject("ExpBar");
            barRoot.transform.SetParent(parent, false);

            RectTransform barRect = barRoot.AddComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(0f, 25f);

            LayoutElement barLE = barRoot.AddComponent<LayoutElement>();
            barLE.preferredHeight = 25f;

            // Background
            barRoot.AddComponent<CanvasRenderer>();
            Image barBg = barRoot.AddComponent<Image>();
            barBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(barRoot.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fill.AddComponent<CanvasRenderer>();
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0.5f;

            // Exp text overlay
            GameObject expText = new GameObject("ExpText");
            expText.transform.SetParent(barRoot.transform, false);
            RectTransform expTextRect = expText.AddComponent<RectTransform>();
            expTextRect.anchorMin = Vector2.zero;
            expTextRect.anchorMax = Vector2.one;
            expTextRect.sizeDelta = Vector2.zero;
            expTextRect.offsetMin = Vector2.zero;
            expTextRect.offsetMax = Vector2.zero;

            expText.AddComponent<CanvasRenderer>();
            Text txt = expText.AddComponent<Text>();
            txt.text = "0/100";
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            return barRoot;
        }

        private static GameObject CreateAttributeRow(Transform parent, string name, string attrName)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 35f);

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 35f;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Level text (e.g. "攻击 Lv.0")
            GameObject levelText = new GameObject("LevelText");
            levelText.transform.SetParent(row.transform, false);
            levelText.AddComponent<RectTransform>();
            LayoutElement ltLE = levelText.AddComponent<LayoutElement>();
            ltLE.flexibleWidth = 1f;
            ltLE.preferredWidth = 100f;
            levelText.AddComponent<CanvasRenderer>();
            Text lt = levelText.AddComponent<Text>();
            lt.text = $"{attrName} Lv.0";
            lt.fontSize = 16;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = Color.white;
            lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (lt.font == null) lt.font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // Bonus text (e.g. "+0.0%")
            GameObject bonusText = new GameObject("BonusText");
            bonusText.transform.SetParent(row.transform, false);
            bonusText.AddComponent<RectTransform>();
            LayoutElement btLE = bonusText.AddComponent<LayoutElement>();
            btLE.preferredWidth = 70f;
            bonusText.AddComponent<CanvasRenderer>();
            Text bt = bonusText.AddComponent<Text>();
            bt.text = "+0.0%";
            bt.fontSize = 16;
            bt.alignment = TextAnchor.MiddleCenter;
            bt.color = new Color(0.5f, 1f, 0.5f, 1f);
            bt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bt.font == null) bt.font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // Upgrade button
            GameObject upgradeBtn = CreateButton(row.transform, "UpgradeBtn", "+1", 50f, 30f);
            LayoutElement ubLE = upgradeBtn.GetComponent<LayoutElement>();
            if (ubLE == null) ubLE = upgradeBtn.AddComponent<LayoutElement>();
            ubLE.preferredWidth = 50f;

            return row;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, float width, float height)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(width, height);

            btnObj.AddComponent<CanvasRenderer>();
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.6f, 0.9f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            // Button label
            GameObject labelObj = new GameObject("Text");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            labelObj.AddComponent<CanvasRenderer>();
            Text labelTxt = labelObj.AddComponent<Text>();
            labelTxt.text = label;
            labelTxt.fontSize = 14;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.color = Color.white;
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelTxt.font == null) labelTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            return btnObj;
        }
    }
}
#endif
