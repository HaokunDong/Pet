using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PetGame.DesktopWindow
{
    /// <summary>
    /// Bootstrapper that initializes the entire Desktop Window system.
    /// Attach this to a GameObject in your main scene alongside TransparentWindowManager.
    /// Handles DPI awareness, component setup, and integration with existing GamePlay.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Execute before other scripts
    public class DesktopWindowBootstrap : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);

#endif

        [Header("Component References")]
        [Tooltip("Reference to the WindowConfig asset.")]
        [SerializeField] private WindowConfig windowConfig;

        [Header("Prefab References")]
        [Tooltip("Prefab for ClickThroughManager. If assigned, instantiates from prefab instead of AddComponent. Load path: Resources/Prefabs/System/ClickThroughManager")]
        [SerializeField] private GameObject clickThroughPrefab;

        [Header("Auto-Setup")]
        [Tooltip("If true, automatically adds required components to this GameObject if missing.")]
        [SerializeField] private bool autoSetupComponents = true;

        private void Awake()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Set DPI awareness before any window operations
            // Try Per-Monitor DPI awareness first (Windows 8.1+), fall back to system DPI aware
            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE = 2
                SetProcessDpiAwareness(2);
            }
            catch
            {
                // Fallback for older Windows versions
                try
                {
                    SetProcessDPIAware();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DesktopWindowBootstrap] Failed to set DPI awareness: {e.Message}");
                }
            }
#endif

            if (autoSetupComponents)
            {
                EnsureComponent<TransparentWindowManager>();
                
                if (windowConfig == null || windowConfig.enableClickThrough)
                    SetupClickThroughManager();

                if (windowConfig == null || windowConfig.enableDrag)
                    EnsureComponent<WindowDragHandler>();
            }
        }

        /// <summary>
        /// Sets up ClickThroughManager: instantiates from prefab if available,
        /// otherwise falls back to loading from Resources or AddComponent.
        /// </summary>
        private void SetupClickThroughManager()
        {
            // If already exists on this object, skip
            if (GetComponent<ClickThroughManager>() != null) return;

            // Try prefab reference first
            if (clickThroughPrefab != null)
            {
                GameObject instance = Instantiate(clickThroughPrefab);
                instance.name = "ClickThroughManager";
                DontDestroyOnLoad(instance);
                Debug.Log("[DesktopWindowBootstrap] ClickThroughManager instantiated from assigned prefab.");
                return;
            }

            // Try loading from Resources
            GameObject prefab = Resources.Load<GameObject>("Prefabs/System/ClickThroughManager");
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = "ClickThroughManager";
                DontDestroyOnLoad(instance);
                Debug.Log("[DesktopWindowBootstrap] ClickThroughManager instantiated from Resources prefab.");
                return;
            }

            // Fallback: AddComponent on this GameObject
            gameObject.AddComponent<ClickThroughManager>();
            Debug.Log("[DesktopWindowBootstrap] ClickThroughManager added via AddComponent (no prefab found).");
        }

        private T EnsureComponent<T>() where T : MonoBehaviour
        {
            T component = GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Validates that all required components are present.
        /// </summary>
        private void OnValidate()
        {
            if (windowConfig == null)
            {
                Debug.Log("[DesktopWindowBootstrap] No WindowConfig assigned. " +
                    "Create one via Assets > Create > PetGame > Window Config");
            }
        }
#endif
    }
}
