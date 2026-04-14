using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enables alpha-based hit testing on a UI Image component.
/// Only pixels with alpha >= alphaThreshold will respond to raycasts,
/// allowing irregular (non-rectangular) click areas that match the sprite shape.
///
/// Usage: Attach this component to any GameObject that has an Image component.
/// Works with any button type (Button, EventTriggerListener, etc.).
/// Requires the sprite texture to have Read/Write Enabled in import settings.
/// </summary>
[RequireComponent(typeof(Image))]
public class AlphaHitTestImage : MonoBehaviour
{
    [Header("Alpha Hit Test Settings")]
    [Tooltip("Minimum alpha value (0-1) for a pixel to be considered clickable. " +
             "0 = entire rect is clickable (default behavior), " +
             "1 = only fully opaque pixels are clickable.")]
    [Range(0f, 1f)]
    public float alphaThreshold = 0.5f;

    private Image _image;

    // Prevent repeated warning logs for the same unreadable texture
    private bool _hasLoggedReadableWarning = false;

    void Awake()
    {
        _image = GetComponent<Image>();
        ApplyAlphaThreshold();
    }

    void OnEnable()
    {
        // Re-apply threshold when the component is re-enabled
        _hasLoggedReadableWarning = false;
        if (_image != null)
        {
            ApplyAlphaThreshold();
        }
    }

    void OnDisable()
    {
        // Reset to default rectangular hit test when disabled
        if (_image != null)
        {
            _image.alphaHitTestMinimumThreshold = 0f;
        }
    }

    /// <summary>
    /// Apply the alpha threshold to the Image component.
    /// Checks texture readability and falls back gracefully if not readable.
    /// </summary>
    public void ApplyAlphaThreshold()
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
            if (_image == null) return;
        }

        // If threshold is 0, no alpha testing needed — use default rect hit test
        if (alphaThreshold <= 0f)
        {
            _image.alphaHitTestMinimumThreshold = 0f;
            return;
        }

        // Check if the current sprite's texture is readable
        if (!IsCurrentTextureReadable())
        {
            if (!_hasLoggedReadableWarning)
            {
                Debug.LogWarning(
                    $"[AlphaHitTestImage] Texture on '{gameObject.name}' is not readable. " +
                    "Please enable 'Read/Write Enabled' in the texture import settings. " +
                    "Falling back to rectangular hit test.");
                _hasLoggedReadableWarning = true;
            }
            _image.alphaHitTestMinimumThreshold = 0f;
            return;
        }

        _image.alphaHitTestMinimumThreshold = alphaThreshold;
    }

    /// <summary>
    /// Refresh the alpha threshold value on the Image.
    /// Called externally (e.g. by ButtonSpriteAnimation) after sprite changes
    /// to ensure the threshold persists across sprite swaps.
    /// Also safe to call from any other animation or sprite-switching system.
    /// </summary>
    public void RefreshThreshold()
    {
        if (_image == null) return;

        if (alphaThreshold <= 0f)
        {
            _image.alphaHitTestMinimumThreshold = 0f;
            return;
        }

        // Re-check texture readability for the new sprite
        if (!IsCurrentTextureReadable())
        {
            _image.alphaHitTestMinimumThreshold = 0f;
            return;
        }

        _image.alphaHitTestMinimumThreshold = alphaThreshold;
    }

    /// <summary>
    /// Check if the current Image sprite's texture is readable.
    /// Returns false if there is no sprite or the texture is not readable.
    /// </summary>
    private bool IsCurrentTextureReadable()
    {
        if (_image == null || _image.sprite == null || _image.sprite.texture == null)
            return false;

        try
        {
            return _image.sprite.texture.isReadable;
        }
        catch (System.Exception)
        {
            // Safety net — should not happen, but ensures no crash
            return false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Called by Unity Editor when a value is changed in the Inspector.
    /// Provides real-time threshold updates during development.
    /// </summary>
    void OnValidate()
    {
        if (_image == null)
            _image = GetComponent<Image>();

        _hasLoggedReadableWarning = false;

        if (_image != null)
        {
            ApplyAlphaThreshold();
        }
    }
#endif
}
