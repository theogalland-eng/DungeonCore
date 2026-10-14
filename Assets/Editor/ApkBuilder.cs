using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Construit l'APK Android en un clic (menu DungeonCore → 10).
///   - Règle Android : identifiant, IL2CPP + ARM64, paysage, SDK minimal.
///   - Bascule la plateforme si besoin (la première fois : longue réimportation).
///   - Produit Builds/DungeonCore.apk (clé de débogage : pour tester, pas pour le Play Store).
/// « Construire et installer » lance aussi le jeu sur le téléphone branché en USB (débogage USB activé).
/// </summary>
public static class ApkBuilder
{
    public const string PackageName = "com.gto13.dungeoncore";
    public static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "DungeonCore.apk"));

    [MenuItem("DungeonCore/10. Construire l'APK (Android)")]
    public static void BuildMenu() => Build(false);

    [MenuItem("DungeonCore/11. Construire et installer sur le téléphone (USB)")]
    public static void BuildAndRunMenu() => Build(true);

    /// <summary>Pont Claude (exec sans argument).</summary>
    public static string BuildApk() => Build(false);

    public static string Build(bool run)
    {
        Configure();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) return "Aucune scène dans la liste de build";
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        EditorUserBuildSettings.buildAppBundle = false;   // un .apk, pas un .aab
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        string msg = s.result == BuildResult.Succeeded
            ? $"APK prêt : {OutputPath} ({s.totalSize / (1024f * 1024f):0.0} Mo, {s.totalTime.TotalMinutes:0.0} min)"
            : $"Échec du build ({s.result}) : {s.totalErrors} erreur(s) — voir la Console";
        Debug.Log("[APK] " + msg);
        if (s.result == BuildResult.Succeeded && !run && !Application.isBatchMode) EditorUtility.RevealInFinder(OutputPath);
        return msg;
    }

    /// <summary>Réglages Android du projet.</summary>
    public static void Configure()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.productName = "Dungeon Core";
        PlayerSettings.SetApplicationIdentifier(android, PackageName);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        // jeu en paysage (les deux sens)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        if (string.IsNullOrEmpty(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "0.1";
        AssetDatabase.SaveAssets();
    }
}
