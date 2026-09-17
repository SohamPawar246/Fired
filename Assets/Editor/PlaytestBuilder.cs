using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click builds for playtesting and for the itch.io upload.
///
///   Tools > FIRED > Build Windows (playtest zip)
///   Tools > FIRED > Build WebGL (itch.io)
///
/// Output goes to a "FIRED_Builds" folder NEXT TO the project, never inside it —
/// the repo has no ignore rule for build output, so building into the project
/// would dump a few hundred MB into the next commit.
///
/// The executable is named FIRED.exe here rather than by changing
/// PlayerSettings.productName: product name is part of the PlayerPrefs path, so
/// renaming it would orphan every saved best-score on players' machines.
/// </summary>
public static class PlaytestBuilder
{
    private static string OutRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "FIRED_Builds"));

    [UnityEditor.MenuItem("Tools/FIRED/Build Windows (playtest zip)")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone,
              Path.Combine(OutRoot, "Windows"), "FIRED.exe");
    }

    [UnityEditor.MenuItem("Tools/FIRED/Build WebGL (itch.io)")]
    public static void BuildWebGL()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUtility.DisplayDialog("Switch to WebGL?",
                "The active platform is " + EditorUserBuildSettings.activeBuildTarget +
                ". Switching to WebGL re-imports every asset in the project, which takes a "
                + "long time on a project this size.\n\nContinue?", "Switch and build", "Cancel"))
            return;

        BuildWebGLNow();
    }

    /// <summary>
    /// Same build with no confirmation dialog, so it can be driven by automation.
    /// Switching the active platform re-imports every asset, so this is slow the
    /// first time and fast on repeat runs.
    /// </summary>
    [UnityEditor.MenuItem("Tools/FIRED/Build WebGL (no prompt)")]
    public static void BuildWebGLNow()
    {
        // itch.io serves the build as static files. Brotli WITHOUT a decompression
        // fallback relies on the host sending the right Content-Encoding header, and
        // when it does not the player just sees a stuck loading bar. The fallback
        // ships a JS decompressor so the build loads regardless of host behaviour.
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.dataCaching = true;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            Debug.Log("[Build] switching active platform to WebGL — this re-imports all assets and takes a while...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, Path.Combine(OutRoot, "WebGL"), "");
    }

    private static void Build(BuildTarget target, BuildTargetGroup group, string dir, string exeName)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[Build] No enabled scenes in Build Settings — aborting.");
            return;
        }

        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = string.IsNullOrEmpty(exeName) ? dir : Path.Combine(dir, exeName),
            target = target,
            targetGroup = group,
            options = BuildOptions.None,
        });

        var s = report.summary;
        if (s.result == BuildResult.Succeeded)
            Debug.Log($"[Build] {target} OK — {s.totalSize / 1048576f:F1} MB in {s.totalTime}\n{dir}");
        else
            Debug.LogError($"[Build] {target} {s.result} — {s.totalErrors} errors. See the log above.");
    }
}
