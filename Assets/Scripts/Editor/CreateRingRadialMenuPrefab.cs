using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetGame.EditorTools
{
    /// <summary>
    /// Editor utility that builds a ready-to-use Ring Radial Menu prefab,
    /// containing a <see cref="RingRadialMenu"/> root, a <see cref="CanvasGroup"/>,
    /// and 8 <see cref="RingRadialMenuButton"/> children, each with a
    /// <see cref="RingSectorGraphic"/> background and an icon Image.
    ///
    /// Default geometry:
    ///   innerRadius = 80, outerRadius = 160, startAngle = 90 (top), clockwise = true
    ///
    /// Menu: Tools > PetGame > Create Ring Radial Menu Prefab
    /// </summary>
    public static class CreateRingRadialMenuPrefab
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/UI/RingRadialMenu.prefab";

        private const float DefaultInnerRadius = 80f;
        private const float DefaultOuterRadius = 160f;
        private const int DefaultButtonCount = 8;
        private const float DefaultStartAngle = 90f;
        private const float DefaultButtonSize = 64f;
        private const float DefaultIconSize = 40f;

        [MenuItem("Tools/PetGame/Create Ring Radial Menu Prefab")]
        public static void CreatePrefab()
        {
            string directory = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                    "Prefab Already Exists",
                    "RingRadialMenu prefab already exists. Overwrite?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            GameObject root = BuildHierarchy();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                Debug.Log($"[CreateRingRadialMenuPrefab] Prefab created at: {PrefabPath}");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("[CreateRingRadialMenuPrefab] Failed to create prefab.");
            }
        }

        [MenuItem("Tools/PetGame/Select Ring Radial Menu Prefab")]
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
                    "RingRadialMenu prefab not found. Use 'Tools > PetGame > Create Ring Radial Menu Prefab' first.",
                    "OK");
            }
        }

        // -----------------------------------------------------------------
        // Hierarchy builder
        // -----------------------------------------------------------------

        private static GameObject BuildHierarchy()
        {
            // Root: RectTransform + CanvasGroup + RingRadialMenu
            GameObject root = new GameObject(
                "RingRadialMenu",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(RingRadialMenu));

            RectTransform rootRT = root.GetComponent<RectTransform>();
            // Anchor to center of parent and use a generous size that covers the outer circle.
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = Vector2.zero;
            rootRT.sizeDelta = new Vector2(DefaultOuterRadius * 2f, DefaultOuterRadius * 2f);

            // Scroll-wheel rotation zone: invisible Graphic that catches scroll events whose
            // pointer lies inside the outer circle and forwards them to RingRadialMenu.
            // Placed as the first child so it sits behind the buttons (lower sibling index =
            // drawn first under default UGUI rules; the buttons' RingSectorGraphic still wins
            // raycasts inside the annulus thanks to UGUI's depth/order priority).
            CreateScrollRotateZone(rootRT, root.GetComponent<RingRadialMenu>());

            // Create 8 button children.
            for (int i = 0; i < DefaultButtonCount; ++i)
            {
                CreateButton(rootRT, i);
            }

            // Apply default settings reflectively so we don't need to expose internals.
            // We instead rely on the component's serialized defaults (innerRadius=80, outerRadius=160,
            // buttonCount=8, startAngleDegrees=90, clockwise=true, enableScrollRotate=true,
            // scrollRotateStepDegrees=15) defined in RingRadialMenuSettings.
            return root;
        }

        private static void CreateScrollRotateZone(RectTransform parent, RingRadialMenu menu)
        {
            GameObject zoneGO = new GameObject(
                "ScrollRotateZone",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RingScrollRotateZone));

            zoneGO.transform.SetParent(parent, worldPositionStays: false);
            zoneGO.transform.SetAsFirstSibling();

            RectTransform zoneRT = zoneGO.GetComponent<RectTransform>();
            zoneRT.anchorMin = Vector2.zero;
            zoneRT.anchorMax = Vector2.one;
            zoneRT.offsetMin = Vector2.zero;
            zoneRT.offsetMax = Vector2.zero;

            RingScrollRotateZone zone = zoneGO.GetComponent<RingScrollRotateZone>();
            zone.raycastTarget = true;

            // Persist the menu reference on the prefab so it does not need to be resolved at runtime.
            using (SerializedObject so = new SerializedObject(zone))
            {
                SerializedProperty pMenu = so.FindProperty("menu");
                if (pMenu != null) pMenu.objectReferenceValue = menu;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateButton(RectTransform parent, int index)
        {
            GameObject btnGO = new GameObject(
                $"Button_{index}",
                typeof(RectTransform),
                typeof(RingRadialMenuButton));

            btnGO.transform.SetParent(parent, worldPositionStays: false);

            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(DefaultButtonSize, DefaultButtonSize);
            btnRT.anchoredPosition = Vector2.zero; // RingRadialMenu.RebuildLayout will reposition.

            // Background sector graphic (clickable hit area).
            GameObject bgGO = new GameObject(
                "Sector",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RingSectorGraphic));
            bgGO.transform.SetParent(btnRT, worldPositionStays: false);

            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            RingSectorGraphic sector = bgGO.GetComponent<RingSectorGraphic>();
            // Slight tint so the area is visible by default; user can replace with sprite later.
            sector.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            sector.raycastTarget = true;

            // Icon image at the center of the button cell.
            GameObject iconGO = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconGO.transform.SetParent(btnRT, worldPositionStays: false);

            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(DefaultIconSize, DefaultIconSize);
            iconRT.anchoredPosition = Vector2.zero;

            Image iconImage = iconGO.GetComponent<Image>();
            iconImage.raycastTarget = false; // icon should not absorb clicks; sector handles them
            iconImage.enabled = false;       // hidden until SetIcon is called

            // Wire the button component's serialized references via SerializedObject so the prefab persists them.
            RingRadialMenuButton btn = btnGO.GetComponent<RingRadialMenuButton>();
            using (SerializedObject so = new SerializedObject(btn))
            {
                SerializedProperty pSector = so.FindProperty("sectorGraphic");
                if (pSector != null) pSector.objectReferenceValue = sector;
                SerializedProperty pIcon = so.FindProperty("iconImage");
                if (pIcon != null) pIcon.objectReferenceValue = iconImage;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
