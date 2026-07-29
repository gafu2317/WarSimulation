using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CombatAutoBattleRole
{
    public WeaponKind Weapon = WeaponKind.Sword;
    public CombatAiPersonalityKind Personality = CombatAiPersonalityKind.Neutral;
}

[Serializable]
public sealed class CombatAutoBattleConfig
{
    public string[] MapNames;
    public CombatAutoBattleRole[] Allies;
    public CombatAutoBattleRole[] Enemies;
    public int MatchCount = 10;
    public int BaseSeed = 1;
    public float TimeoutSeconds = 180f;
    public float TimeScale = 16f;
}

[Serializable]
public sealed class CombatAutoBattleMatchResult
{
    public int Index;
    public int Seed;
    public string MapName;
    public string Outcome;
    public float GameSeconds;
    public float RealSeconds;
}

public static class CombatAutoBattleConfigLoader
{
    public const string CommandLineArgument = "-autoBattleConfig";

    public static bool TryLoadFromCommandLine(out CombatAutoBattleConfig config, out string path)
    {
        config = null;
        path = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], CommandLineArgument, StringComparison.Ordinal)) continue;
            path = args[i + 1];
            return TryLoadFromFile(path, out config);
        }

        return false;
    }

    public static bool TryLoadFromFile(string path, out CombatAutoBattleConfig config)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        string json = File.ReadAllText(path);
        config = JsonUtility.FromJson<CombatAutoBattleConfig>(json);
        return config != null;
    }
}

public static class CombatAutoBattleReportWriter
{
    [Serializable]
    private sealed class Report
    {
        public int MatchCount;
        public int Wins;
        public int Losses;
        public int Timeouts;
        public float AverageGameSeconds;
        public float AverageRealSeconds;
        public List<CombatAutoBattleMatchResult> Matches = new();
    }

    public static string Write(IReadOnlyList<CombatAutoBattleMatchResult> matches)
    {
        string directory = ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        int wins = 0;
        int losses = 0;
        int timeouts = 0;
        float gameSeconds = 0f;
        float realSeconds = 0f;
        for (int i = 0; i < matches.Count; i++)
        {
            CombatAutoBattleMatchResult match = matches[i];
            gameSeconds += match.GameSeconds;
            realSeconds += match.RealSeconds;
            if (match.Outcome == "勝利") wins++;
            else if (match.Outcome == "敗北") losses++;
            else timeouts++;
        }

        var report = new Report
        {
            MatchCount = matches.Count,
            Wins = wins,
            Losses = losses,
            Timeouts = timeouts,
            AverageGameSeconds = matches.Count > 0 ? gameSeconds / matches.Count : 0f,
            AverageRealSeconds = matches.Count > 0 ? realSeconds / matches.Count : 0f,
            Matches = new List<CombatAutoBattleMatchResult>(matches),
        };

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, timestamp + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(report, prettyPrint: true), Encoding.UTF8);
        return path;
    }

    private static string ResolveOutputDirectory()
    {
        string cwd = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cwd, "Assets")))
            return Path.Combine(cwd, "Logs", "AutoBattles");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(projectRoot) && Directory.Exists(Path.Combine(projectRoot, "Assets")))
            return Path.Combine(projectRoot, "Logs", "AutoBattles");

        return Path.Combine(Application.persistentDataPath, "AutoBattles");
    }
}
