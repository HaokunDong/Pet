using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Sprite frame animation component for UI buttons.
/// Supports Idle (loop) and OnClick (once, interruptible) animation states.
/// Attach to a GameObject with an Image component.
/// </summary>
public class ButtonSpriteAnimation : MonoBehaviour
{
    // Animation state enum for tracking current playback state
    public enum AnimState
    {
        Idle,
        OnClick
    }

    [Header("Idle Animation")]
    [Tooltip("Sprite frames for idle loop animation")]
    public Sprite[] idleSprites;

    [Tooltip("Idle animation frame rate (frames per second)")]
    public float idleFPS = 24f;

    [Header("OnClick Animation")]
    [Tooltip("Sprite frames for click animation (played once)")]
    public Sprite[] onClickSprites;

    [Tooltip("OnClick animation frame rate (frames per second)")]
    public float onClickFPS = 24f;

    [Header("Settings")]
    [Tooltip("Automatically start playing Idle animation on enable")]
    public bool autoPlayIdle = true;

    // Current animation state
    private AnimState _currentState = AnimState.Idle;

    // Reference to the Image component on this GameObject
    private Image _image;

    // Single coroutine reference to prevent coroutine accumulation
    private Coroutine _currentAnim;

    // Cached reference to AlphaHitTestImage for refreshing threshold after sprite swaps
    private AlphaHitTestImage _alphaHitTest;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (_image == null)
        {
            Debug.LogWarning($"[ButtonSpriteAnimation] No Image component found on {gameObject.name}. Animation will not play.");
        }
        _alphaHitTest = GetComponent<AlphaHitTestImage>();
    }

    void OnEnable()
    {
        if (autoPlayIdle)
        {
            PlayIdle();
        }
    }

    void OnDisable()
    {
        StopCurrentAnimation();
        _currentState = AnimState.Idle;
    }

    void OnDestroy()
    {
        _currentAnim = null;
    }

    /// <summary>
    /// Stop the currently running animation coroutine (if any).
    /// </summary>
    private void StopCurrentAnimation()
    {
        if (_currentAnim != null)
        {
            StopCoroutine(_currentAnim);
            _currentAnim = null;
        }
    }

    /// <summary>
    /// Start playing the Idle animation in a loop.
    /// Stops any currently running animation first.
    /// </summary>
    public void PlayIdle()
    {
        if (_image == null) return;
        if (idleSprites == null || idleSprites.Length == 0) return;

        StopCurrentAnimation();
        _currentState = AnimState.Idle;
        _currentAnim = StartCoroutine(PlayAnimation(idleSprites, idleFPS, true));
    }

    /// <summary>
    /// Start playing the OnClick animation once.
    /// Interrupts any currently running animation (including a previous OnClick).
    /// After playback completes, automatically returns to Idle animation.
    /// </summary>
    public void PlayOnClick()
    {
        if (_image == null) return;
        if (onClickSprites == null || onClickSprites.Length == 0) return;

        StopCurrentAnimation();
        _currentState = AnimState.OnClick;
        _currentAnim = StartCoroutine(PlayAnimation(onClickSprites, onClickFPS, false));
    }

    /// <summary>
    /// Core coroutine that plays sprite frame animation.
    /// </summary>
    /// <param name="frames">Array of sprites to play</param>
    /// <param name="fps">Playback frame rate</param>
    /// <param name="loop">If true, loops forever; if false, plays once then triggers auto-revert to Idle</param>
    private IEnumerator PlayAnimation(Sprite[] frames, float fps, bool loop)
    {
        if (frames == null || frames.Length == 0)
            yield break;

        float interval = 1f / Mathf.Max(fps, 0.001f);

        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (_image != null)
                {
                    _image.sprite = frames[i];
                    // Refresh alpha hit test threshold after sprite swap
                    // Lazy-cache: AlphaHitTestImage may be added after Awake by MainView
                    if (_alphaHitTest == null)
                    {
                        _alphaHitTest = GetComponent<AlphaHitTestImage>();
                    }
                    if (_alphaHitTest != null)
                    {
                        _alphaHitTest.RefreshThreshold();
                    }
                }
                yield return new WaitForSeconds(interval);
            }
        }
        while (loop);

        // Single-play finished (OnClick), auto-revert to Idle
        _currentAnim = null;
        PlayIdle();
    }

    /// <summary>
    /// Get the current animation state.
    /// </summary>
    public AnimState CurrentState
    {
        get { return _currentState; }
    }
}
