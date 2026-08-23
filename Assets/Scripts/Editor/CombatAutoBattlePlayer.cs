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
    private const string LightweightBuildDirectory = ".unity/CombatAutoBattleLight";
    private const string LightweightApplicationName = "CombatAutoBattleLight";

    [MenuItem("Tools/War Simulation/Auto Battle/Build Player")]
    public static void Build()
    {
        BuildPlayer(run: false, configPath: null, sweep: false, lightweight: false);
    }

    [MenuItem("Tools/War Simulation/Auto Battle/Build Lightweight Player")]
    public static void BuildLightweight()
    {
        BuildPlayer(run: false, configPath: null, sweep: false, lightweight: true);
    }

    [MenuItem("Tools/War Simulation/Auto Battle/Setup And Build Lightweight Player")]
    public static void SetupAndBuildLightweight()
    {
        if (!CombatAutoBattleSceneSetup.TrySetupCurrentScene()) return;
        BuildLightweight();
    }

    [MenuItem("Tools/War Simulation/Auto Battle/Build And Run With Config...")]
    public static void BuildAndRunWithConfig()
    {
        string configPath = EditorUtility.OpenFilePanel("自動戦闘設定 JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(configPath)) return;
        BuildPlayer(run: true, configPath: configPath, sweep: false, lightweight: false);
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
        BuildPlayer(run: true, configPath: configPath, sweep: true, lightweight: false);
    }

    private static void BuildPlayer(bool run, string configPath, bool sweep, bool lightweight)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
        {
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone,
                BuildTarget.StandaloneOSX);
            if (!switched)
            {
                EditorUtility.DisplayDialog(
                    "StandaloneOSXへの切替に失敗しました",
                    "macOS Build Support がインストールされているか確認してください。",
                    "閉じる");
                return;
            }
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
        string buildDirectory = Path.Combine(
            projectRoot,
            lightweight ? LightweightBuildDirectory : BuildDirectory);
        string applicationName = lightweight ? LightweightApplicationName : ApplicationName;
        string applicationPath = Path.Combine(buildDirectory, applicationName + ".app");
        Directory.CreateDirectory(buildDirectory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene.path },
            locationPathName = applicationPath,
            target = BuildTarget.StandaloneOSX,
            options = lightweight ? BuildOptions.None : BuildOptions.Development,
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

        Launch(applicationPath, applicationName, configPath, projectRoot, sweep);
    }

    private static void Launch(
        string applicationPath,
        string applicationName,
        string configPath,
        string projectRoot,
        bool sweep)
    {
        string playerLogDirectory = Path.Combine(projectRoot, "Logs", "AutoBattles");
        Directory.CreateDirectory(playerLogDirectory);
        string logPath = Path.Combine(
            playerLogDirectory,
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_player.log");

        string argumentName = sweep
            ? CombatAutoBattleConfigLoader.SweepCommandLineArgument
            : CombatAutoBattleConfigLoader.CommandLineArgument;

        string executablePath = ResolveExecutablePath(applicationPath, applicationName);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments =
                $"-batchmode -nographics -logFile {Quote(logPath)} " +
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

    private static string ResolveExecutablePath(string applicationPath, string applicationName)
    {
        string macOsDirectory = Path.Combine(applicationPath, "Contents", "MacOS");
        string expected = Path.Combine(macOsDirectory, applicationName);
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
