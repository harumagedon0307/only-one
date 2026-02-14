using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBatchBuild
{
    private const string OutputDir = "Builds/Android";

    public static void BuildApk()
    {
        Build(BuildTarget.Android, false);
    }

    public static void BuildAab()
    {
        Build(BuildTarget.Android, true);
    }

    public static void ApplyAndroidSettings()
    {
        ConfigureAndroidForCI();
        AssetDatabase.SaveAssets();
        Console.WriteLine("Android settings saved.");
    }

    private static void Build(BuildTarget target, bool buildAppBundle)
    {
        ConfigureAndroidForCI();

        var enabledScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in Build Settings.");
        }

        Directory.CreateDirectory(OutputDir);
        var fileName = buildAppBundle ? "HappyNewWear_FrontNew.aab" : "HappyNewWear_FrontNew.apk";
        var outputPath = Path.Combine(OutputDir, fileName);

        EditorUserBuildSettings.buildAppBundle = buildAppBundle;

        var options = new BuildPlayerOptions
        {
            scenes = enabledScenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception(
                $"Android build failed: {report.summary.result}, " +
                $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
        }

        Console.WriteLine($"Android build succeeded: {outputPath}");
    }

    private static void ConfigureAndroidForCI()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.useCustomKeystore = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        Debug.Log("Android build config applied: IL2CPP + ARM64 + Gradle + default debug keystore");
    }
}
