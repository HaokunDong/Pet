using UnityEngine;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Custom Inspector for MeleeSkillEffectData ScriptableObject.
    /// Draws the default inspector fields plus a visual preview of the character sprite
    /// overlaid with all skill range shapes, similar to CharacterDataEditor.
    /// </summary>
    [CustomEditor(typeof(MeleeSkillEffectData))]
    public class MeleeSkillEffectDataEditor : Editor
    {
        // Preview area settings
        private const float PREVIEW_SIZE = 400f;
        private const float BASE_PIXELS_PER_UNIT = 80f;

        // Cached textures for shape rendering
        private Texture2D circleTexture;

        // Effective pixels-per-unit after auto-fit calculation
        private float effectivePixelsPerUnit = BASE_PIXELS_PER_UNIT;

        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields
            DrawDefaultInspector();

            MeleeSkillEffectData data = (MeleeSkillEffectData)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Skill Range Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview shows the character sprite with skill range shapes overlaid.\n" +
                "Red = Circle shapes, Blue = Box shapes.\n" +
                "Shapes are shown matching the sprite's native facing direction.",
                MessageType.Info);

            // Draw the preview area
            DrawSkillRangePreview(data);

            // Force repaint when values change
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
                Repaint();
            }
        }

        /// <summary>
        /// Draw the visual preview area with sprite and skill range shapes.
        /// </summary>
        private void DrawSkillRangePreview(MeleeSkillEffectData data)
        {
            // Reserve a square area for the preview
            Rect previewRect = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE, GUILayout.ExpandWidth(true));

            // Draw background
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Center of the preview area (character position)
            Vector2 center = previewRect.center;

            // Auto-fit: calculate the best scale so content fills the preview area
            CalculateAutoFitScale(data, previewRect);

            // Draw crosshair at center
            DrawCrosshair(center);

            // Draw the character sprite
            DrawPreviewSprite(data, center);

            // Draw skill range shapes
            DrawSkillRangeShapes(data, center);
        }

        /// <summary>
        /// Calculate the best pixels-per-unit so that the largest element
        /// (sprite or skill range) fills the preview area nicely.
        /// </summary>
        private void CalculateAutoFitScale(MeleeSkillEffectData data, Rect previewRect)
        {
            float maxWorldExtent = 0f;

            // Consider sprite size
            if (data.previewSprite != null)
            {
                float ppu = data.previewSprite.pixelsPerUnit;
                Rect spriteRect = data.previewSprite.textureRect;
                float worldWidth = spriteRect.width / ppu;
                float worldHeight = spriteRect.height / ppu;
                maxWorldExtent = Mathf.Max(maxWorldExtent, worldWidth * 0.5f, worldHeight * 0.5f);
            }

            // Consider skill range shapes
            if (data.skillRangeShapes != null)
            {
                for (int i = 0; i < data.skillRangeShapes.Length; i++)
                {
                    AttackRangeShape shape = data.skillRangeShapes[i];
                    if (shape == null) continue;

                    float shapeExtent = 0f;
                    switch (shape.shapeType)
                    {
                        case AttackShapeType.Circle:
                            shapeExtent = new Vector2(shape.offset.x, shape.offset.y).magnitude + shape.radius;
                            break;
                        case AttackShapeType.Box:
                            shapeExtent = new Vector2(shape.offset.x, shape.offset.y).magnitude
                                          + Mathf.Max(shape.size.x, shape.size.y) * 0.5f;
                            break;
                    }
                    maxWorldExtent = Mathf.Max(maxWorldExtent, shapeExtent);
                }
            }

            // Calculate effective scale: fill 80% of the half-preview size
            if (maxWorldExtent > 0.01f)
            {
                float availablePixels = Mathf.Min(previewRect.width, previewRect.height) * 0.4f;
                effectivePixelsPerUnit = availablePixels / maxWorldExtent;
            }
            else
            {
                effectivePixelsPerUnit = BASE_PIXELS_PER_UNIT;
            }
        }

        /// <summary>
        /// Draw a crosshair at the center to indicate character position.
        /// </summary>
        private void DrawCrosshair(Vector2 center)
        {
            Color crossColor = new Color(1f, 1f, 1f, 0.2f);
            float halfLen = 20f;

            // Horizontal line
            EditorGUI.DrawRect(new Rect(center.x - halfLen, center.y - 0.5f, halfLen * 2, 1f), crossColor);
            // Vertical line
            EditorGUI.DrawRect(new Rect(center.x - 0.5f, center.y - halfLen, 1f, halfLen * 2), crossColor);
        }

        /// <summary>
        /// Draw the preview sprite in the preview area.
        /// </summary>
        private void DrawPreviewSprite(MeleeSkillEffectData data, Vector2 center)
        {
            Sprite sprite = data.previewSprite;

            if (sprite != null)
            {
                Texture2D tex = sprite.texture;
                Rect spriteRect = sprite.textureRect;

                // Calculate UV coordinates for the sprite within its atlas
                Rect uvRect = new Rect(
                    spriteRect.x / tex.width,
                    spriteRect.y / tex.height,
                    spriteRect.width / tex.width,
                    spriteRect.height / tex.height
                );

                // Scale sprite to preview using the auto-fit effectivePixelsPerUnit
                float ppu = sprite.pixelsPerUnit;
                float worldWidth = spriteRect.width / ppu;
                float worldHeight = spriteRect.height / ppu;
                float drawWidth = worldWidth * effectivePixelsPerUnit;
                float drawHeight = worldHeight * effectivePixelsPerUnit;

                Rect drawRect = new Rect(
                    center.x - drawWidth * 0.5f,
                    center.y - drawHeight * 0.5f,
                    drawWidth,
                    drawHeight
                );

                GUI.DrawTextureWithTexCoords(drawRect, tex, uvRect);
            }
            else
            {
                // Draw placeholder icon
                Texture2D icon = EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
                if (icon == null)
                    icon = EditorGUIUtility.FindTexture("DefaultAsset Icon");

                if (icon != null)
                {
                    float iconSize = 64f;
                    Rect iconRect = new Rect(center.x - iconSize * 0.5f, center.y - iconSize * 0.5f, iconSize, iconSize);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }

                Rect labelRect = new Rect(center.x - 50, center.y + 36, 100, 16);
                GUI.Label(labelRect, "No Sprite", EditorStyles.centeredGreyMiniLabel);
            }
        }

        /// <summary>
        /// Draw all skill range shapes in the preview area.
        /// </summary>
        private void DrawSkillRangeShapes(MeleeSkillEffectData data, Vector2 center)
        {
            if (data.skillRangeShapes == null) return;

            for (int i = 0; i < data.skillRangeShapes.Length; i++)
            {
                AttackRangeShape shape = data.skillRangeShapes[i];
                if (shape == null) continue;

                // Convert world offset to preview pixels
                // Mirror offset.x based on defaultFacesRight
                float facingSign = data.defaultFacesRight ? 1f : -1f;
                Vector2 shapeCenter = center + new Vector2(
                    shape.offset.x * facingSign * effectivePixelsPerUnit,
                    -shape.offset.y * effectivePixelsPerUnit
                );

                switch (shape.shapeType)
                {
                    case AttackShapeType.Circle:
                        DrawCircleFilled(shapeCenter, shape.radius * effectivePixelsPerUnit, new Color(1f, 0.2f, 0.2f, 0.2f));
                        DrawCircleOutline(shapeCenter, shape.radius * effectivePixelsPerUnit, new Color(1f, 0.2f, 0.2f, 0.6f));
                        break;

                    case AttackShapeType.Box:
                        float w = shape.size.x * effectivePixelsPerUnit;
                        float h = shape.size.y * effectivePixelsPerUnit;
                        Rect boxRect = new Rect(shapeCenter.x - w * 0.5f, shapeCenter.y - h * 0.5f, w, h);

                        // Filled
                        EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.4f, 1f, 0.2f));

                        // Outline
                        Color outlineColor = new Color(0.2f, 0.4f, 1f, 0.6f);
                        float t = 1f; // thickness
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, boxRect.width, t), outlineColor);             // top
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - t, boxRect.width, t), outlineColor);       // bottom
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, t, boxRect.height), outlineColor);             // left
                        EditorGUI.DrawRect(new Rect(boxRect.xMax - t, boxRect.y, t, boxRect.height), outlineColor);      // right
                        break;
                }
            }
        }

        /// <summary>
        /// Draw a filled circle in the GUI using a generated texture.
        /// </summary>
        private void DrawCircleFilled(Vector2 center, float radius, Color color)
        {
            if (radius < 1f) return;

            int size = Mathf.CeilToInt(radius * 2f);
            size = Mathf.Clamp(size, 4, 512);

            if (circleTexture == null || circleTexture.width != size)
            {
                if (circleTexture != null)
                    DestroyImmediate(circleTexture);

                circleTexture = CreateCircleTexture(size);
            }

            Rect drawRect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(drawRect, circleTexture, ScaleMode.StretchToFill);
            GUI.color = prevColor;
        }

        /// <summary>
        /// Draw a circle outline in the GUI using Handles.
        /// </summary>
        private void DrawCircleOutline(Vector2 center, float radius, Color color)
        {
            if (radius < 1f) return;

            Handles.BeginGUI();
            Handles.color = color;

            int segments = 64;
            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    0f
                );

                if (i > 0)
                {
                    Handles.DrawLine(prevPoint, point);
                }
                prevPoint = point;
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// Create a white circle texture for filled circle rendering.
        /// </summary>
        private Texture2D CreateCircleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            float centerF = size * 0.5f;
            float radiusSqr = centerF * centerF;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - centerF + 0.5f;
                    float dy = y - centerF + 0.5f;
                    float distSqr = dx * dx + dy * dy;

                    pixels[y * size + x] = distSqr <= radiusSqr ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void OnDisable()
        {
            // Clean up cached textures
            if (circleTexture != null)
            {
                DestroyImmediate(circleTexture);
                circleTexture = null;
            }
        }
    }
}
