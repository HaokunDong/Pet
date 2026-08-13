using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// Automatically copies steam_appid.txt to the build output directory after each build.
/// Steam requires this file to be next to the executable during development.
/// </summary>
public class CopySteamAppIdPostBuild : IPostprocessBuildWithReport
{
    // Execute order (lower = earlier)
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Source: project root directory
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string sourceFile = Path.Combine(projectRoot, "steam_appid.txt");

        // Destination: same directory as the built executable
        string buildDir = Path.GetDirectoryName(report.summary.outputPath);
        string destFile = Path.Combine(buildDir, "steam_appid.txt");

        if (File.Exists(sourceFile))
        {
            File.Copy(sourceFile, destFile, overwrite: true);
            Debug.Log($"[CopySteamAppIdPostBuild] Copied steam_appid.txt to: {destFile}");
        }
        else
        {
            Debug.LogWarning($"[CopySteamAppIdPostBuild] steam_appid.txt not found at: {sourceFile}. " +
                             "Please create it in the project root with your Steam App ID (use 480 for testing).");
        }
    }
}
