using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Headless entry point: Unity -batchmode -executeMethod BuildAndroidApk.Build -quit
public static class BuildAndroidApk
{
    const string OutputPath = "Builds/Android/CricketVR.apk";

    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
        Debug.Log($"[BuildAndroidApk] {summary.result} -> {summary.outputPath} ({summary.totalSize} bytes, {summary.totalErrors} errors)");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
