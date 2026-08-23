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
    public float TimeoutSeconds = 600f;
    public float TimeScale = 6f;
    public bool DisableDiagnostics;
    public bool FixedSeed;
    public bool PreserveFixedDeltaTime;
}

[Serializable]
public sealed class CombatAutoBattleMatchResult
{
    public int Index;
    public int Seed;
    public string MapName;
    public bool StonePositionsReversed;
    public string Outcome;
    public float GameSeconds;
    public float RealSeconds;
    public float TimeScale;
    public float FixedDeltaTime;
    public bool PreserveFixedDeltaTime;
    public bool FixedSeed;
    public int SkippedAiDecisionCount;
    public string PlayerBuildGuid;
    public string UnityVersion;
    public string DiagnosticLogPath;
}

public static class CombatAutoBattleReportSchema
{
    public const int CurrentVersion = 2;
}

public static class CombatAutoBattleOutcomes
{
    public const string Victory = "Victory";
    public const string Defeat = "Defeat";
    public const string Timeout = "Timeout";

    public static string FromBattleState(CombatBattleState state, bool timedOut)
    {
        if (timedOut) return Timeout;
        if (state == CombatBattleState.Victory) return Victory;
        return Defeat;
    }
}

public static class CombatAutoBattleConfigLoader
{
    public const string CommandLineArgument = "-autoBattleConfig";
    public const string SweepCommandLineArgument = "-autoBattleSweepConfig";
    public const string OutputDirectoryCommandLineArgument = "-autoBattleOutputDirectory";

    private static string _lastConfigPath;

    public static bool TryGetLastConfigPath(out string path)
    {
        path = _lastConfigPath;
        return !string.IsNullOrEmpty(path);
    }

    public static bool TryGetOutputDirectory(out string path)
    {
        if (!TryFindArgument(OutputDirectoryCommandLineArgument, out path)) return false;
        path = Path.GetFullPath(path);
        return true;
    }

    public static bool TryLoadFromCommandLine(out CombatAutoBattleConfig config, out string path)
    {
        config = null;
        path = null;
        if (!TryFindArgument(CommandLineArgument, out path)) return false;
        if (!TryLoadFromFile(path, out config)) return false;
        _lastConfigPath = path;
        return true;
    }

    public static bool TryLoadSweepFromCommandLine(out CombatCompositionSweepConfig config, out string path)
    {
        config = null;
        path = null;
        if (!TryFindArgument(SweepCommandLineArgument, out path)) return false;
        if (!CombatCompositionSweepConfigLoader.TryLoadFromFile(path, out config)) return false;
        _lastConfigPath = path;
        return true;
    }

    public static bool TryLoadFromFile(string path, out CombatAutoBattleConfig config)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        string json = File.ReadAllText(path);
        config = JsonUtility.FromJson<CombatAutoBattleConfig>(json);
        if (config == null) return false;
        _lastConfigPath = path;
        return true;
    }

    private static bool TryFindArgument(string name, out string value)
    {
        value = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal)) continue;
            value = args[i + 1];
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}

public static class CombatAutoBattleReportWriter
{
    [Serializable]
    private sealed class Report
    {
        public int SchemaVersion = CombatAutoBattleReportSchema.CurrentVersion;
        public int MatchCount;
        public int Wins;
        public int Losses;
        public int Timeouts;
        public float AverageGameSeconds;
        public float AverageRealSeconds;
        public float MedianGameSeconds;
        public float MinGameSeconds;
        public float MaxGameSeconds;
        public float MedianDecidedGameSeconds;
        public float TimeScale;
        public float FixedDeltaTime;
        public bool PreserveFixedDeltaTime;
        public bool FixedSeed;
        public string PlayerBuildGuid;
        public string UnityVersion;
        public List<CombatAutoBattleMatchResult> Matches = new();
    }

    public static string Write(IReadOnlyList<CombatAutoBattleMatchResult> matches, string path = null)
    {
        string directory = ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        int wins = 0;
        int losses = 0;
        int timeouts = 0;
        float gameSeconds = 0f;
        float realSeconds = 0f;
        var gameSecondsSamples = new List<float>(matches.Count);
        var decidedGameSecondsSamples = new List<float>(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            CombatAutoBattleMatchResult match = matches[i];
            gameSeconds += match.GameSeconds;
            realSeconds += match.RealSeconds;
            gameSecondsSamples.Add(match.GameSeconds);
            if (match.Outcome != CombatAutoBattleOutcomes.Timeout)
                decidedGameSecondsSamples.Add(match.GameSeconds);
            if (match.Outcome == CombatAutoBattleOutcomes.Victory) wins++;
            else if (match.Outcome == CombatAutoBattleOutcomes.Defeat) losses++;
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
            MedianGameSeconds = CombatAutoBattleStatistics.Median(gameSecondsSamples),
            MinGameSeconds = CombatAutoBattleStatistics.Min(gameSecondsSamples),
            MaxGameSeconds = CombatAutoBattleStatistics.Max(gameSecondsSamples),
            MedianDecidedGameSeconds = CombatAutoBattleStatistics.Median(decidedGameSecondsSamples),
            Matches = new List<CombatAutoBattleMatchResult>(matches),
        };
        if (matches.Count > 0)
        {
            CombatAutoBattleMatchResult first = matches[0];
            report.TimeScale = first.TimeScale;
            report.FixedDeltaTime = first.FixedDeltaTime;
            report.PreserveFixedDeltaTime = first.PreserveFixedDeltaTime;
            report.FixedSeed = first.FixedSeed;
            report.PlayerBuildGuid = first.PlayerBuildGuid;
            report.UnityVersion = first.UnityVersion;
        }

        if (string.IsNullOrEmpty(path))
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            path = Path.Combine(directory, timestamp + ".json");
        }

        File.WriteAllText(path, JsonUtility.ToJson(report, prettyPrint: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string CreateReportPath(string prefix = null)
    {
        string directory = ResolveOutputDirectory();
        Directory.CreateDirectory(directory);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string name = string.IsNullOrEmpty(prefix) ? timestamp + ".json" : prefix + "_" + timestamp + ".json";
        return Path.Combine(directory, name);
    }

    private static string ResolveOutputDirectory()
    {
        return CombatDebugPaths.GetLogsDirectory("AutoBattles");
    }
}

public static class CombatAutoBattleStatistics
{
    public static float Median(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0) return 0f;
        var sorted = new List<float>(values);
        sorted.Sort();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) * 0.5f
            : sorted[middle];
    }

    public static float Min(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0) return 0f;
        float minimum = values[0];
        for (int i = 1; i < values.Count; i++)
            minimum = Mathf.Min(minimum, values[i]);
        return minimum;
    }

    public static float Max(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0) return 0f;
        float maximum = values[0];
        for (int i = 1; i < values.Count; i++)
            maximum = Mathf.Max(maximum, values[i]);
        return maximum;
    }
}
