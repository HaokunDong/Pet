using UnityEngine;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Custom Inspector for CharacterData ScriptableObject.
    /// Draws the default inspector plus a visual preview of the character sprite
    /// overlaid with all attack range shapes.
    /// </summary>
    [CustomEditor(typeof(CharacterData))]
    public class CharacterDataEditor : Editor
    {
        // Preview area settings
        private const float PREVIEW_SIZE = 256f;
        private const float PIXELS_PER_UNIT = 80f; // Scale factor: world units to preview pixels

        // Cached textures for shape rendering
        private Texture2D circleTexture;
        private Texture2D boxTexture;

        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields
            DrawDefaultInspector();

            CharacterData data = (CharacterData)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Attack Range Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview shows the character sprite with attack range shapes overlaid.\n" +
                "Red = Circle shapes, Blue = Box shapes.\n" +
                "Shapes are shown matching the sprite's native facing direction.",
                MessageType.Info);

            // Draw the preview area
            DrawAttackRangePreview(data);

            // Force repaint when values change
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
                Repaint();
            }
        }

        /// <summary>
        /// Draw the visual preview area with sprite and attack range shapes.
        /// </summary>
        private void DrawAttackRangePreview(CharacterData data)
        {
            // Reserve a square area for the preview
            Rect previewRect = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE, GUILayout.ExpandWidth(true));

            // Draw background
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Center of the preview area (character position)
            Vector2 center = previewRect.center;

            // Draw crosshair at center
            DrawCrosshair(center, previewRect);

            // Draw the character sprite
            DrawCharacterSprite(data, center);

            // Draw attack range shapes
            DrawAttackRangeShapes(data, center);

            // Draw fallback range circle if no shapes defined
            if (data.attackRangeShapes == null || data.attackRangeShapes.Length == 0)
            {
                DrawCircleOutline(center, data.attackRange * PIXELS_PER_UNIT, new Color(1f, 1f, 0f, 0.5f));
                // Label
                Vector2 labelPos = new Vector2(center.x, center.y - data.attackRange * PIXELS_PER_UNIT - 12);
                Rect labelRect = new Rect(labelPos.x - 60, labelPos.y, 120, 16);
                GUI.Label(labelRect, $"Fallback: {data.attackRange:F1}", EditorStyles.centeredGreyMiniLabel);
            }
        }

        /// <summary>
        /// Draw a crosshair at the center to indicate character position.
        /// </summary>
        private void DrawCrosshair(Vector2 center, Rect bounds)
        {
            Color crossColor = new Color(1f, 1f, 1f, 0.2f);
            float halfLen = 20f;

            // Horizontal line
            EditorGUI.DrawRect(new Rect(center.x - halfLen, center.y - 0.5f, halfLen * 2, 1f), crossColor);
            // Vertical line
            EditorGUI.DrawRect(new Rect(center.x - 0.5f, center.y - halfLen, 1f, halfLen * 2), crossColor);
        }

        /// <summary>
        /// Draw the character sprite in the preview area.
        /// </summary>
        private void DrawCharacterSprite(CharacterData data, Vector2 center)
        {
            Sprite sprite = data.sprite;

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

                // Scale sprite to preview: use sprite's pixels-per-unit for accurate sizing
                float ppu = sprite.pixelsPerUnit;
                float worldWidth = spriteRect.width / ppu;
                float worldHeight = spriteRect.height / ppu;
                float drawWidth = worldWidth * PIXELS_PER_UNIT;
                float drawHeight = worldHeight * PIXELS_PER_UNIT;

                // Clamp to reasonable size
                float maxDim = PREVIEW_SIZE * 0.6f;
                if (drawWidth > maxDim || drawHeight > maxDim)
                {
                    float scale = maxDim / Mathf.Max(drawWidth, drawHeight);
                    drawWidth *= scale;
                    drawHeight *= scale;
                }

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
        /// Draw all attack range shapes in the preview area.
        /// </summary>
        private void DrawAttackRangeShapes(CharacterData data, Vector2 center)
        {
            if (data.attackRangeShapes == null) return;

            for (int i = 0; i < data.attackRangeShapes.Length; i++)
            {
                AttackRangeShape shape = data.attackRangeShapes[i];
                if (shape == null) continue;

                // Convert world offset to preview pixels
                // Use the sprite's native facing direction: if defaultFacesRight, offset.x is positive to the right;
                // if defaultFacesLeft, offset.x is positive to the left (which is screen-left in preview).
                float facingSign = data.defaultFacesRight ? 1f : -1f;
                Vector2 shapeCenter = center + new Vector2(shape.offset.x * facingSign * PIXELS_PER_UNIT, -shape.offset.y * PIXELS_PER_UNIT);

                switch (shape.shapeType)
                {
                    case AttackShapeType.Circle:
                        DrawCircleFilled(shapeCenter, shape.radius * PIXELS_PER_UNIT, new Color(1f, 0.2f, 0.2f, 0.2f));
                        DrawCircleOutline(shapeCenter, shape.radius * PIXELS_PER_UNIT, new Color(1f, 0.2f, 0.2f, 0.6f));
                        break;

                    case AttackShapeType.Box:
                        float w = shape.size.x * PIXELS_PER_UNIT;
                        float h = shape.size.y * PIXELS_PER_UNIT;
                        Rect boxRect = new Rect(shapeCenter.x - w * 0.5f, shapeCenter.y - h * 0.5f, w, h);

                        // Filled
                        EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.4f, 1f, 0.2f));

                        // Outline
                        Color outlineColor = new Color(0.2f, 0.4f, 1f, 0.6f);
                        float t = 1f; // thickness
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, boxRect.width, t), outlineColor); // top
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - t, boxRect.width, t), outlineColor); // bottom
                        EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, t, boxRect.height), outlineColor); // left
                        EditorGUI.DrawRect(new Rect(boxRect.xMax - t, boxRect.y, t, boxRect.height), outlineColor); // right
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

            // Draw circle as line segments
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

            float center = size * 0.5f;
            float radiusSqr = center * center;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distSqr = dx * dx + dy * dy;

                    if (distSqr <= radiusSqr)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
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
            if (boxTexture != null)
            {
                DestroyImmediate(boxTexture);
                boxTexture = null;
            }
        }
    }
}
