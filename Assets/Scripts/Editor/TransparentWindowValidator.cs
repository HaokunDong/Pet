using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PetGame.DesktopWindow.Editor
{
    /// <summary>
    /// Pre-build validator that checks Player Settings compatibility
    /// for the transparent window system.
    /// Runs automatically before every build.
    /// </summary>
    public class TransparentWindowValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneWindows)
            {
                return; // Only validate for Windows builds
            }

            bool hasWarnings = false;

            // Check 1: Fullscreen mode should be Windowed
            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            {
                Debug.LogWarning("[TransparentWindowValidator] Player Settings > Resolution > Fullscreen Mode " +
                    "should be set to 'Windowed' for transparent window to work correctly. " +
                    $"Current: {PlayerSettings.fullScreenMode}");
                hasWarnings = true;
            }

            // Check 2: Color Space (Gamma recommended)
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                Debug.LogWarning("[TransparentWindowValidator] Player Settings > Other Settings > Color Space " +
                    "is set to 'Linear'. Transparent window works best with 'Gamma' color space. " +
                    "Linear mode may cause transparency artifacts on some systems.");
                hasWarnings = true;
            }

            // Check 3: Resizable window (informational)
            if (PlayerSettings.resizableWindow)
            {
                Debug.Log("[TransparentWindowValidator] Note: 'Resizable Window' is enabled. " +
                    "The transparent window system will override window size at startup.");
            }

#if UNITY_2021_2_OR_NEWER
            // Check 4: DXGI Flip Model Swapchain (Unity 2021.2+)
            // This setting can interfere with transparent rendering
            if (PlayerSettings.useFlipModelSwapchain)
            {
                Debug.LogWarning("[TransparentWindowValidator] Player Settings > Resolution > " +
                    "'Use DXGI Flip Model Swapchain' is enabled. This MUST be disabled for " +
                    "transparent window to work. Disabling it now...");
                PlayerSettings.useFlipModelSwapchain = false;
                hasWarnings = true;
            }
#endif

            if (!hasWarnings)
            {
                Debug.Log("[TransparentWindowValidator] All Player Settings are correctly configured for transparent window.");
            }
        }

        /// <summary>
        /// Menu item to manually validate settings without building.
        /// </summary>
        [MenuItem("PetGame/Validate Transparent Window Settings")]
        private static void ValidateSettings()
        {
            Debug.Log("[TransparentWindowValidator] Validating Player Settings...");

            bool allGood = true;

            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            {
                Debug.LogWarning($"  ✗ Fullscreen Mode: {PlayerSettings.fullScreenMode} (should be Windowed)");
                allGood = false;
            }
            else
            {
                Debug.Log("  ✓ Fullscreen Mode: Windowed");
            }

            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                Debug.LogWarning("  ✗ Color Space: Linear (Gamma recommended for transparency)");
                allGood = false;
            }
            else
            {
                Debug.Log($"  ✓ Color Space: {PlayerSettings.colorSpace}");
            }

#if UNITY_2021_2_OR_NEWER
            if (PlayerSettings.useFlipModelSwapchain)
            {
                Debug.LogWarning("  ✗ DXGI Flip Model Swapchain: Enabled (must be disabled)");
                allGood = false;
            }
            else
            {
                Debug.Log("  ✓ DXGI Flip Model Swapchain: Disabled");
            }
#endif

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows)
            {
                Debug.LogWarning($"  ✗ Build Target: {EditorUserBuildSettings.activeBuildTarget} " +
                    "(should be StandaloneWindows or StandaloneWindows64)");
                allGood = false;
            }
            else
            {
                Debug.Log($"  ✓ Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            }

            if (allGood)
            {
                Debug.Log("[TransparentWindowValidator] All settings are correct! Ready to build.");
            }
            else
            {
                Debug.LogWarning("[TransparentWindowValidator] Some settings need attention. See warnings above.");
            }
        }

        /// <summary>
        /// Menu item to auto-fix all settings for transparent window.
        /// </summary>
        [MenuItem("PetGame/Fix Transparent Window Settings")]
        private static void FixSettings()
        {
            Debug.Log("[TransparentWindowValidator] Applying recommended settings...");

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            Debug.Log("  → Set Fullscreen Mode to Windowed");

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.useFlipModelSwapchain = false;
            Debug.Log("  → Disabled DXGI Flip Model Swapchain");
#endif

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                Debug.Log("  → Switched build target to StandaloneWindows64");
            }

            Debug.Log("[TransparentWindowValidator] Settings applied successfully!");
        }
    }
}
