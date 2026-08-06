using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class CombatAutoBattlePlayer
{
    private const string BuildDirectory = ".unity/CombatAutoBattle";
    private const string ApplicationName = "CombatAutoBattle";

    [MenuItem("Tools/War Simulation/Auto Battle/Build Player")]
    public static void Build()
    {
        BuildPlayer(run: false, configPath: null, sweep: false);
    }

    [MenuItem("Tools/War Simulation/Auto Battle/Build And Run With Config...")]
    public static void BuildAndRunWithConfig()
    {
        string configPath = EditorUtility.OpenFilePanel("自動戦闘設定 JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(configPath)) return;
        BuildPlayer(run: true, configPath: configPath, sweep: false);
    }

    [MenuItem("Tools/War Simulation/Auto Battle/Build And Run Sweep...")]
    public static void BuildAndRunSweep()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string defaultDir = Path.Combine(projectRoot, "Tools", "AutoBattle");
        if (!Directory.Exists(defaultDir))
            defaultDir = Application.dataPath;

        string configPath = EditorUtility.OpenFilePanel("編成探索設定 JSON", defaultDir, "json");
        if (string.IsNullOrEmpty(configPath)) return;
        BuildPlayer(run: true, configPath: configPath, sweep: true);
    }

    private static void BuildPlayer(bool run, string configPath, bool sweep)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
        {
            EditorUtility.DisplayDialog(
                "StandaloneOSXへ切り替えてください",
                "現在の Build Target では macOS 用 Auto Battle Player を作成できません。",
                "閉じる");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("シーンがありません", "ビルドする戦闘シーンを開いてください。", "閉じる");
            return;
        }

        CombatAutoBattleRunner runner = FindInScene<CombatAutoBattleRunner>(scene);
        if (runner == null)
        {
            EditorUtility.DisplayDialog(
                "Runner がありません",
                "開いているシーンに CombatAutoBattleRunner を追加してからビルドしてください。",
                "閉じる");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string buildDirectory = Path.Combine(projectRoot, BuildDirectory);
        string applicationPath = Path.Combine(buildDirectory, ApplicationName + ".app");
        Directory.CreateDirectory(buildDirectory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene.path },
            locationPathName = applicationPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development,
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(
                "ビルドに失敗しました",
                $"Build結果: {report.summary.result}\nConsoleを確認してください。",
                "閉じる");
            return;
        }

        Debug.Log($"[自動戦闘] Playerをビルドしました: {applicationPath}");
        if (!run) return;

        Launch(applicationPath, configPath, projectRoot, sweep);
    }

    private static void Launch(string applicationPath, string configPath, string projectRoot, bool sweep)
    {
        string playerLogDirectory = Path.Combine(projectRoot, "Logs", "AutoBattles");
        Directory.CreateDirectory(playerLogDirectory);
        string logPath = Path.Combine(
            playerLogDirectory,
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_player.log");

        string argumentName = sweep
            ? CombatAutoBattleConfigLoader.SweepCommandLineArgument
            : CombatAutoBattleConfigLoader.CommandLineArgument;

        string executablePath = ResolveExecutablePath(applicationPath);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments =
                $"-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile {Quote(logPath)} " +
                $"{argumentName} {Quote(configPath)}",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = false,
        });
        if (process == null)
        {
            EditorUtility.DisplayDialog("起動に失敗しました", executablePath, "閉じる");
            return;
        }

        Debug.Log($"[自動戦闘] Playerを開始しました。PID={process.Id}, Log={logPath}");
    }

    private static string ResolveExecutablePath(string applicationPath)
    {
        string macOsDirectory = Path.Combine(applicationPath, "Contents", "MacOS");
        string expected = Path.Combine(macOsDirectory, ApplicationName);
        if (File.Exists(expected)) return expected;

        string[] candidates = Directory.GetFiles(macOsDirectory);
        if (candidates.Length == 1) return candidates[0];
        throw new FileNotFoundException("Player の実行ファイルを特定できません。", expected);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null) return component;
        }

        return null;
    }
}
