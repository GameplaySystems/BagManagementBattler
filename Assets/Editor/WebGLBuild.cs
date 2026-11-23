using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

internal static class WebGLBuild
{
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"WebGL build failed: {report.summary.totalErrors} error(s).");
        }
    }
}
