using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Сборка первой alpha-версии из командной строки:
// Unity.exe -batchmode -quit -projectPath ... -executeMethod AlphaBuild.BuildWindows64
public static class AlphaBuild
{
    private const string OutputDirectory = "F:/3d game/Builds/Alpha0.1";

    [MenuItem("Slutsk1941/Собрать Alpha 0.1")]
    public static void BuildWindows64()
    {
        PlayerSettings.bundleVersion = "0.1-alpha";
        PlayerSettings.productName = "Slutsk1941";

        string[] scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        Directory.CreateDirectory(OutputDirectory);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDirectory, "Slutsk1941.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("[AlphaBuild] result=" + summary.result +
                  " size=" + summary.totalSize +
                  " errors=" + summary.totalErrors +
                  " output=" + summary.outputPath);
        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
