using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// Core component of a ring (donut) radial menu.
    /// Distributes its child <see cref="RingRadialMenuButton"/>s evenly along an annulus
    /// defined by <see cref="RingRadialMenuSettings.innerRadius"/> and
    /// <see cref="RingRadialMenuSettings.outerRadius"/>, and (optionally) follows a world-space
    /// <see cref="centerTarget"/> by projecting it to screen space each <see cref="LateUpdate"/>.
    ///
    /// <para>
    /// Hit-testing is delegated to per-button <see cref="RingSectorGraphic"/> components,
    /// so clicks outside the ring (in the inner hole or beyond the outer rim) are not absorbed
    /// by this menu and pass through to whatever lies underneath.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Ring Radial Menu/Ring Radial Menu")]
    public class RingRadialMenu : MonoBehaviour, IScrollHandler
    {
        // -----------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------

        [Header("Geometry")]
        [Tooltip("All geometry parameters: inner/outer radius, button count, start angle, direction.")]
        [SerializeField] private RingRadialMenuSettings settings = new RingRadialMenuSettings();

        [Header("Center Following")]
        [Tooltip("Optional world-space target around which this menu is centered. " +
                 "If null, the menu uses its own anchoredPosition as the center and does not follow.")]
        [SerializeField] private Transform centerTarget;

        [Tooltip("Camera used to project centerTarget to screen space. " +
                 "If null, Camera.main is used.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Optional Canvas reference. If null, the parent Canvas is auto-resolved.")]
        [SerializeField] private Canvas parentCanvas;

        [Tooltip("CanvasGroup used to fade/disable the menu when the target is behind the camera. " +
                 "If null, one will be added automatically when needed.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Buttons")]
        [Tooltip("Buttons to be laid out on the ring. If empty, child RingRadialMenuButton components are auto-collected on Awake/OnValidate.")]
        [SerializeField] private List<RingRadialMenuButton> buttons = new List<RingRadialMenuButton>();

        [Header("Icons")]
        [Tooltip("Distance from the RingRadialMenu center to every child Icon object. -1 uses the button center radius.")]
        [Min(-1f)]
        [SerializeField] private float iconRadius = -1f;

        [Header("Debug")]
        [Tooltip("If true, draws gizmos for the inner/outer ring in the Scene view.")]
        [SerializeField] private bool drawGizmos = true;

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when any ring button is clicked. Argument is the button index.
        /// </summary>
        public event Action<int> OnButtonClicked;

        /// <summary>
        /// Fired when any button's state changes (Idle / Suspended / Selected).
        /// Arguments: (buttonIndex, oldState, newState).
        /// Subscribe to this to drive visual feedback (sprite swaps, color tints, etc.)
        /// from outside the menu component.
        /// </summary>
        public event Action<int, RingRadialMenuButton.ButtonState, RingRadialMenuButton.ButtonState> OnButtonStateChanged;

        /// <summary>
        /// Fired after a scroll-driven rotation is applied. Argument is the
        /// total accumulated rotation (in degrees) since the menu was initialized.
        /// Positive value means the wheel has been rotated counter-clockwise overall
        /// (matching the math convention where increasing angle = CCW in screen space
        /// with Y-up).
        /// </summary>
        public event Action<float> OnRotated;

        // -----------------------------------------------------------------
        // Cached state
        // -----------------------------------------------------------------

        private RectTransform cachedRect;
        /// <summary>RectTransform of this menu (the ring center pivot).</summary>
        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null) cachedRect = (RectTransform)transform;
                return cachedRect;
            }
        }

        private bool wasHiddenByCamera;

        // Accumulated rotation (degrees) applied through scroll-wheel input.
        // This is added on top of settings.startAngleDegrees when laying buttons out.
        private float accumulatedRotation;

        // -----------------------------------------------------------------
        // Public read-only accessors
        // -----------------------------------------------------------------

        public float InnerRadius => settings.innerRadius;
        public float OuterRadius => settings.outerRadius;
        public int ButtonCount => buttons.Count;
        public IReadOnlyList<RingRadialMenuButton> Buttons => buttons;

        /// <summary>
        /// Whether scroll-driven rotation is currently enabled (mirrors the setting).
        /// </summary>
        public bool IsScrollRotateEnabled => settings != null && settings.enableScrollRotate;

        /// <summary>
        /// Total accumulated rotation (degrees) applied via the mouse wheel since
        /// initialization (or the last <see cref="ResetRotation"/>).
        /// </summary>
        public float AccumulatedRotation => accumulatedRotation;

        // -----------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            ResolveReferences();
            CollectButtonsIfEmpty();
            settings.Validate();
            RebuildLayout();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CollectButtonsIfEmpty();
            RebuildLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Defer rebuild slightly to avoid SendMessage warnings inside OnValidate.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ResolveReferences();
                CollectButtonsIfEmpty();
                settings.Validate();
                RebuildLayout();
            };
        }
#endif

        private void LateUpdate()
        {
            FollowCenterTarget();
        }

        // -----------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------

        private void ResolveReferences()
        {
            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
            if (parentCanvas != null && parentCanvas.rootCanvas != null)
            {
                parentCanvas = parentCanvas.rootCanvas;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            EnsureScrollRotateZone();
        }

        /// <summary>
        /// Make sure a <see cref="RingScrollRotateZone"/> child exists. The zone is an
        /// invisible, full-rect Graphic that catches scroll-wheel events anywhere
        /// inside the outer circle (including the inner hole and the gaps between buttons).
        /// Without it, scrolls landing on the inner hole would have no Graphic to hit and
        /// would be silently dropped by the GraphicRaycaster.
        ///
        /// <para>This auto-creation makes the feature work even on prefabs that were
        /// authored before the scroll-zone existed, so the user does not have to rebuild them.</para>
        /// </summary>
        private void EnsureScrollRotateZone()
        {
            // Never touch prefab assets opened in the project window — Unity forbids
            // hierarchy / serialized data mutations on them and throws
            // "Setting the parent of a transform which resides in a Prefab Asset is disabled".
            if (!IsEditableInstance())
            {
                return;
            }

            // Look for an existing child zone first.
            var existing = GetComponentInChildren<RingScrollRotateZone>(true);
            if (existing != null)
            {
                existing.SetMenu(this);
                FitZoneRect(existing.rectTransform);
                // Place the zone behind the buttons in sibling order so it does not visually
                // hide them; it has no mesh anyway, but ordering also affects raycast priority.
                existing.transform.SetAsFirstSibling();
                return;
            }

            // Avoid creating new objects while the editor is doing asset import / OnValidate
            // serialization, otherwise Unity will warn about "SendMessage cannot be called ...".
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In edit mode we still create it, but defer to the next editor frame so the
                // creation does not happen inside OnValidate. RebuildLayout will be called again.
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    if (!IsEditableInstance()) return;
                    if (GetComponentInChildren<RingScrollRotateZone>(true) != null) return;
                    CreateScrollRotateZoneChild();
                };
                return;
            }
#endif
            CreateScrollRotateZoneChild();
        }

        private void CreateScrollRotateZoneChild()
        {
            if (!IsEditableInstance()) return;

            var go = new GameObject("ScrollRotateZone",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RingScrollRotateZone));
            go.transform.SetParent(this.transform, worldPositionStays: false);
            go.transform.SetAsFirstSibling();

            var rt = (RectTransform)go.transform;
            FitZoneRect(rt);

            var zone = go.GetComponent<RingScrollRotateZone>();
            zone.SetMenu(this);
            zone.raycastTarget = true;
        }

        /// <summary>
        /// Returns true when this component lives on a real scene instance (or a prefab
        /// stage instance) that can safely have its hierarchy and serialized data mutated.
        /// Returns false when it lives on a prefab asset selected in the Project window,
        /// or on objects that are still being deserialized / imported.
        /// </summary>
        private bool IsEditableInstance()
        {
            if (this == null) return false;
            if (gameObject == null) return false;

            // Objects that are not yet attached to any scene (e.g. an asset that the editor
            // is loading) have an invalid scene handle. We must not modify them.
            var scene = gameObject.scene;
            if (!scene.IsValid()) return false;
            if (scene.handle == 0) return false;

#if UNITY_EDITOR
            // Belt-and-suspenders: even if the scene looks valid, Unity treats prefab assets
            // as read-only for hierarchy operations. The PrefabUtility check is authoritative.
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return false;
            }
#endif
            return true;
        }

        private static void FitZoneRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Ensures the root RectTransform's rect is large enough to contain the outer disk.
        /// Only grows; never shrinks beyond the user's explicit value.
        /// </summary>
        private void EnsureRootSizeCoversOuter()
        {
            // Mutating sizeDelta on a prefab asset triggers the same Unity safeguard as
            // SetParent does, so guard with the same check.
            if (!IsEditableInstance()) return;
            if (settings == null) return;
            float diameter = settings.outerRadius * 2f;
            if (diameter <= 0f) return;

            RectTransform rt = RectTransform;
            if (rt == null) return;

            Rect r = rt.rect;
            float w = r.width;
            float h = r.height;

            // If the rect is smaller than the outer disk on either axis, expand sizeDelta
            // to compensate. We compute the additional sizeDelta needed (= deficit), so that
            // anchored layouts keep their original anchor configuration intact.
            float dw = Mathf.Max(0f, diameter - w);
            float dh = Mathf.Max(0f, diameter - h);
            if (dw > 0.01f || dh > 0.01f)
            {
                Vector2 sd = rt.sizeDelta;
                rt.sizeDelta = new Vector2(sd.x + dw, sd.y + dh);
            }
        }

        private void CollectButtonsIfEmpty()
        {
            if (buttons != null && buttons.Count > 0)
            {
                // Drop any null entries that may appear after deletions in the editor.
                buttons.RemoveAll(b => b == null);
                if (buttons.Count > 0) return;
            }

            if (buttons == null) buttons = new List<RingRadialMenuButton>();
            else buttons.Clear();

            // Collect immediate child buttons, in sibling order.
            for (int i = 0; i < transform.childCount; ++i)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var btn = child.GetComponent<RingRadialMenuButton>();
                if (btn != null) buttons.Add(btn);
            }
        }

        // -----------------------------------------------------------------
        // Layout
        // -----------------------------------------------------------------

        /// <summary>
        /// Rebuilds the radial layout: positions every button on the mid-ring radius,
        /// and pushes hit-test parameters into each <see cref="RingSectorGraphic"/>.
        /// </summary>
        public void RebuildLayout()
        {
            if (buttons == null || buttons.Count == 0) return;

            // Make sure the root RectTransform is at least as large as the outer disk.
            // GraphicRaycaster does a RectTransform-bounds pre-check before invoking
            // IsRaycastLocationValid, so if the root is smaller than 2*outerRadius the
            // ScrollRotateZone (which stretches to fill the root) would be cropped and
            // the inner-hole scrolls would never be delivered to us.
            EnsureRootSizeCoversOuter();

            int actual = buttons.Count;
            if (actual != settings.buttonCount)
            {
                Debug.LogWarning(
                    $"[RingRadialMenu] settings.buttonCount ({settings.buttonCount}) differs from actual child count ({actual}). " +
                    "Laying out using the actual count.", this);
            }

            float midRadius = (settings.innerRadius + settings.outerRadius) * 0.5f;
            float effectiveIconRadius = iconRadius >= 0f ? iconRadius : midRadius;
            float sectorSize = 360f / Mathf.Max(1, actual);
            float dir = settings.clockwise ? -1f : 1f;
            bool useAngleCheck = actual > 1; // single button covers the full ring
            float effectiveSectorSize = useAngleCheck ? sectorSize : 360f;

            // Total angular offset = baseline start angle + scroll-driven accumulated rotation.
            // Direction convention (screen-space, Y-up):
            //   accumulatedRotation > 0  -> CCW (angle value grows)
            //   accumulatedRotation < 0  -> CW  (angle value shrinks)
            // The sign is decided in HandleScrollRotate, not here.
            float baseAngle = settings.startAngleDegrees + accumulatedRotation;

            for (int i = 0; i < actual; ++i)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                float angleDeg = baseAngle + dir * sectorSize * i;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Place the button center on the mid-ring circle.
                Vector2 pos = new Vector2(
                    Mathf.Cos(angleRad) * midRadius,
                    Mathf.Sin(angleRad) * midRadius);
                btn.RectTransform.anchoredPosition = pos;
                ApplyIconRadius(btn, pos.normalized, pos, effectiveIconRadius);

                // Wire dispatcher and index, so the button can fan out to OnButtonClicked.
                btn.Initialize(i, DispatchClickFromButton);

                // Subscribe to per-button state changes so we can relay them through
                // the menu-level OnButtonStateChanged event.
                // Unsubscribe first to avoid duplicate registrations on repeated RebuildLayout calls.
                btn.OnStateChanged -= RelayButtonStateChanged;
                btn.OnStateChanged += RelayButtonStateChanged;

                // Configure the sector graphic for hit-testing.
                var sector = btn.SectorGraphic;
                if (sector != null)
                {
                    sector.Configure(
                        center: this.RectTransform,
                        innerR: settings.innerRadius,
                        outerR: settings.outerRadius,
                        sectorCenterDeg: angleDeg,
                        sectorSizeDeg: effectiveSectorSize,
                        useAngle: useAngleCheck);

                    // Apply custom sector size if specified (> 0); otherwise keep
                    // the existing RectTransform layout (e.g. stretch-to-fill).
                    ApplySectorSize(sector.rectTransform);

                    // Rotate the sector so its local orientation always faces the ring center.
                    // angleDeg is the math-convention angle (0 = +X, 90 = +Y).
                    // Subtracting 90 makes the sector's local "up" (+Y) point toward the center.
                    sector.rectTransform.localEulerAngles = new Vector3(0f, 0f, angleDeg - 90f);
                }

                // Ensure the icon never intercepts pointer events (enter/exit/click).
                // This guarantees the sector's hover state is not interrupted when the
                // mouse moves over the icon area.
                EnsureIconNonInteractive(btn);
            }
        }

        /// <summary>
        /// Positions every child object named "Icon" so its distance from the menu center
        /// matches <see cref="iconRadius"/>. The button itself can stay on the sector center.
        /// </summary>
        private static void ApplyIconRadius(RingRadialMenuButton btn, Vector2 direction, Vector2 buttonCenter, float targetRadius)
        {
            if (btn == null) return;

            Vector2 targetMenuSpace = direction * targetRadius;
            Vector2 iconLocalPosition = targetMenuSpace - buttonCenter;

            RectTransform[] rects = btn.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; ++i)
            {
                RectTransform iconRT = rects[i];
                if (iconRT == null || iconRT == btn.RectTransform) continue;

                bool isBoundIcon = btn.IconImage != null && iconRT == btn.IconImage.rectTransform;
                bool isNamedIcon = iconRT.gameObject.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isBoundIcon && !isNamedIcon) continue;

                iconRT.anchorMin = new Vector2(0.5f, 0.5f);
                iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.pivot = new Vector2(0.5f, 0.5f);
                iconRT.anchoredPosition = iconLocalPosition;
            }
        }

        /// <summary>
        /// Applies the custom sector width/height from <see cref="settings"/> to a sector's
        /// RectTransform. When a dimension is 0, the sector keeps its current layout (e.g.
        /// stretch-to-fill the parent button cell). When > 0, the sector is switched to a
        /// center-anchored fixed-size layout on that axis.
        /// </summary>
        private void ApplySectorSize(RectTransform sectorRT)
        {
            if (sectorRT == null || settings == null) return;

            float w = settings.sectorWidth;
            float h = settings.sectorHeight;

            // Nothing to do if both are zero (keep existing layout).
            if (w <= 0f && h <= 0f) return;

            // Switch to center-anchored layout so sizeDelta controls the actual size.
            sectorRT.anchorMin = new Vector2(0.5f, 0.5f);
            sectorRT.anchorMax = new Vector2(0.5f, 0.5f);
            sectorRT.pivot = new Vector2(0.5f, 0.5f);
            sectorRT.anchoredPosition = Vector2.zero;

            // If only one dimension is specified, use the button cell's size for the other.
            // We read the current sizeDelta as a fallback (it was set by the prefab or user).
            Vector2 size = sectorRT.sizeDelta;
            if (w > 0f) size.x = w;
            if (h > 0f) size.y = h;
            sectorRT.sizeDelta = size;
        }

        /// <summary>
        /// Ensures the icon image on a button does not intercept any pointer events.
        /// Sets <c>raycastTarget = false</c> on the icon <see cref="Image"/> and adds a
        /// <see cref="CanvasGroup"/> with <c>blocksRaycasts = false</c> and
        /// <c>interactable = false</c> to the icon GameObject. This guarantees that
        /// pointer-enter / pointer-exit events are never stolen from the underlying
        /// <see cref="RingSectorGraphic"/>, keeping the sector's hover state continuous
        /// even when the mouse passes over the icon area.
        /// </summary>
        private static void EnsureIconNonInteractive(RingRadialMenuButton btn)
        {
            if (btn == null) return;
            Image icon = btn.IconImage;
            if (icon == null) return;

            // Force raycastTarget off so the icon never participates in UGUI raycasts.
            icon.raycastTarget = false;

            // Add a CanvasGroup to block all pointer interactions on the icon subtree.
            // This is a belt-and-suspenders measure: even if someone accidentally sets
            // raycastTarget = true on the icon, the CanvasGroup will still prevent it
            // from receiving any pointer events.
            CanvasGroup cg = icon.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = icon.gameObject.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        // -----------------------------------------------------------------
        // Center target following
        // -----------------------------------------------------------------

        /// <summary>
        /// Projects <see cref="centerTarget"/> from world to screen space and writes the result
        /// into this RectTransform's position so that the ring stays centered on the target.
        /// </summary>
        private void FollowCenterTarget()
        {
            if (centerTarget == null)
            {
                // No follow: keep current anchoredPosition; ensure visible.
                if (wasHiddenByCamera)
                {
                    SetHiddenByCamera(false);
                }
                return;
            }

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(centerTarget.position);
            // z < 0 means the target is behind the camera.
            if (screen.z < 0f)
            {
                SetHiddenByCamera(true);
                return;
            }
            else if (wasHiddenByCamera)
            {
                SetHiddenByCamera(false);
            }

            // Convert screen to local point in the parent rect of this menu.
            RectTransform parentRT = RectTransform.parent as RectTransform;
            if (parentRT == null) return;

            Camera uiCam = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCam = parentCanvas.worldCamera;
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, new Vector2(screen.x, screen.y), uiCam, out localPoint))
            {
                RectTransform.anchoredPosition = localPoint;
            }
        }

        private void SetHiddenByCamera(bool hidden)
        {
            wasHiddenByCamera = hidden;
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = hidden ? 0f : 1f;
            canvasGroup.blocksRaycasts = !hidden;
            canvasGroup.interactable = !hidden;
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Sets the world-space target this menu should orbit. Pass null to disable following.
        /// </summary>
        public void SetCenterTarget(Transform target)
        {
            centerTarget = target;
        }

        /// <summary>
        /// Sets the camera used to project <see cref="centerTarget"/> to screen space.
        /// </summary>
        public void SetWorldCamera(Camera cam)
        {
            worldCamera = cam;
        }

        /// <summary>
        /// Registers (or replaces) the click callback for a single button by index.
        /// Out-of-range indices log a warning and are otherwise ignored.
        /// </summary>
        public void SetButtonCallback(int index, UnityAction callback)
        {
            if (!IsIndexValid(index)) return;
            buttons[index].SetCallback(callback);
        }

        /// <summary>
        /// Sets the icon sprite for a single button. Null hides the icon.
        /// </summary>
        public void SetButtonIcon(int index, Sprite icon)
        {
            if (!IsIndexValid(index)) return;
            buttons[index].SetIcon(icon);
        }

        /// <summary>
        /// Returns the button at the given index, or null if out of range.
        /// </summary>
        public RingRadialMenuButton GetButton(int index)
        {
            if (!IsIndexValid(index)) return null;
            return buttons[index];
        }

        /// <summary>
        /// Re-applies geometry. Call after mutating <see cref="settings"/> at runtime via reflection.
        /// </summary>
        public void ApplySettings(RingRadialMenuSettings newSettings)
        {
            if (newSettings == null) return;
            settings = newSettings;
            settings.Validate();
            RebuildLayout();
        }

        /// <summary>
        /// Enables or disables scroll-driven rotation at runtime.
        /// When disabled, <see cref="RingScrollRotateZone"/> stops absorbing scroll events,
        /// letting them pass through to lower-layer UI / scene listeners.
        /// </summary>
        public void SetScrollRotateEnabled(bool enabled)
        {
            if (settings == null) return;
            settings.enableScrollRotate = enabled;
        }

        /// <summary>
        /// Resets the accumulated scroll rotation to zero and rebuilds the layout.
        /// </summary>
        public void ResetRotation()
        {
            if (Mathf.Approximately(accumulatedRotation, 0f)) return;
            accumulatedRotation = 0f;
            RebuildLayout();
            try { OnRotated?.Invoke(accumulatedRotation); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        /// <summary>
        /// Applies a wheel-driven rotation step.
        ///
        /// <para>Direction policy (screen space, Y-up):</para>
        /// <list type="bullet">
        /// <item><c>scrollDeltaY &gt; 0</c> (wheel scrolled UP) -&gt; ring rotates <b>clockwise</b>,
        /// implemented as a <i>negative</i> angular offset (angle value decreases).</item>
        /// <item><c>scrollDeltaY &lt; 0</c> (wheel scrolled DOWN) -&gt; ring rotates <b>counter-clockwise</b>,
        /// implemented as a <i>positive</i> angular offset (angle value increases).</item>
        /// </list>
        /// This sign convention is intentionally decoupled from <see cref="RingRadialMenuSettings.clockwise"/>,
        /// which only governs how buttons are <i>indexed</i> around the ring at layout time.
        /// </summary>
        /// <param name="scrollDeltaY">The vertical component of <c>PointerEventData.scrollDelta</c>.</param>
        public void HandleScrollRotate(float scrollDeltaY)
        {
            if (settings == null || !settings.enableScrollRotate) return;
            if (Mathf.Approximately(scrollDeltaY, 0f)) return;

            float step = settings.scrollRotateStepDegrees;
            if (step <= 0f) return;

            // sign: deltaY > 0 (wheel up) -> clockwise -> angle decreases.
            float sign = scrollDeltaY > 0f ? -1f : 1f;
            accumulatedRotation += sign * step;

            RebuildLayout();

            try { OnRotated?.Invoke(accumulatedRotation); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        /// <summary>
        /// IScrollHandler implementation on the menu root.
        ///
        /// <para>
        /// Why here, not only on <see cref="RingScrollRotateZone"/>?
        /// The buttons sitting on top of the zone are themselves raycast targets, and
        /// because UGUI dispatches <c>IScrollHandler</c> to the top-most hit object first
        /// (then walks up the GameObject parent chain via <c>ExecuteEvents.ExecuteHierarchy</c>),
        /// the zone underneath would never see scrolls that occur over a button.
        /// Implementing the handler on the root guarantees every scroll happening anywhere
        /// inside this menu's hierarchy bubbles up to us.
        /// </para>
        ///
        /// <para>
        /// Distance filter: the requirement says scrolling rotates the wheel only when the
        /// pointer is inside the outer circle. We re-apply that filter here so scrolls landing
        /// outside the disk (e.g. on a button that overlaps the rim) still pass through.
        /// </para>
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (settings == null || !settings.enableScrollRotate) return;

            float deltaY = eventData.scrollDelta.y;
            if (Mathf.Approximately(deltaY, 0f)) return;

            // Re-check that the pointer is inside the outer disk in our local space.
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    RectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            float outerR = settings.outerRadius;
            float sqrDist = localPoint.x * localPoint.x + localPoint.y * localPoint.y;
            if (sqrDist > outerR * outerR) return;

            HandleScrollRotate(deltaY);
            eventData.Use();
        }

        private bool IsIndexValid(int index)
        {
            if (buttons == null || index < 0 || index >= buttons.Count)
            {
                Debug.LogWarning($"[RingRadialMenu] Button index {index} out of range [0, {(buttons == null ? 0 : buttons.Count)}).", this);
                return false;
            }
            return true;
        }

        private void DispatchClickFromButton(int index)
        {
            try
            {
                OnButtonClicked?.Invoke(index);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>
        /// Relays per-button <see cref="RingRadialMenuButton.OnStateChanged"/> events
        /// through the menu-level <see cref="OnButtonStateChanged"/> event.
        /// </summary>
        private void RelayButtonStateChanged(int index, RingRadialMenuButton.ButtonState oldState, RingRadialMenuButton.ButtonState newState)
        {
            try
            {
                OnButtonStateChanged?.Invoke(index, oldState, newState);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>
        /// Deselects (sets to Idle) the button at the given index.
        /// Call this when the panel/page associated with a button is closed externally,
        /// so the button reverts from Selected back to Idle.
        /// </summary>
        public void DeselectButton(int index)
        {
            if (!IsIndexValid(index)) return;
            buttons[index].Deselect();
        }

        /// <summary>
        /// Deselects all buttons that are currently in the Selected state.
        /// </summary>
        public void DeselectAllButtons()
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Count; ++i)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Deselect();
                }
            }
        }

        /// <summary>
        /// Returns true if the button at the given index is currently Selected.
        /// </summary>
        public bool IsButtonSelected(int index)
        {
            if (!IsIndexValid(index)) return false;
            return buttons[index].IsSelected;
        }

        // -----------------------------------------------------------------
        // Gizmos
        // -----------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            RectTransform rt = (RectTransform)transform;
            Vector3 center = rt.position;
            Vector3 normal = rt.forward; // Use the rect's local Z as the disk normal.

            // Draw inner ring (green) and outer ring (red) using the rect's
            // X/Y axes scaled by lossy scale, so radii stay in rect-units.
            Vector3 xAxis = rt.right * rt.lossyScale.x;
            Vector3 yAxis = rt.up * rt.lossyScale.y;

            Gizmos.color = Color.green;
            DrawCircleGizmo(center, xAxis, yAxis, settings.innerRadius, 64);

            Gizmos.color = Color.red;
            DrawCircleGizmo(center, xAxis, yAxis, settings.outerRadius, 64);
        }

        private static void DrawCircleGizmo(Vector3 center, Vector3 xAxis, Vector3 yAxis, float radius, int segments)
        {
            if (radius <= 0f || segments < 3) return;
            Vector3 prev = center + xAxis * radius;
            float step = Mathf.PI * 2f / segments;
            for (int i = 1; i <= segments; ++i)
            {
                float a = step * i;
                Vector3 next = center + xAxis * (Mathf.Cos(a) * radius) + yAxis * (Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
