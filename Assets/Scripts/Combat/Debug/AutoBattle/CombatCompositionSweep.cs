using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CombatCompositionCandidate
{
    public CombatAutoBattleRole[] Roles = Array.Empty<CombatAutoBattleRole>();
}

[Serializable]
public sealed class CombatCompositionSweepConfig
{
    public string[] MapNames;
    public CombatAutoBattleRole[] Enemy;
    public CombatCompositionCandidate[] Candidates;
    public int CandidateCount = 40;
    public int MatchesPerCandidate = 8;
    public int BaseSeed = 12000;
    public int MinPartySize = 4;
    public int MaxPartySize = 6;
    public float TimeoutSeconds = 480f;
    public float TimeScale = 16f;
}

[Serializable]
public sealed class CombatCompositionCandidateResult
{
    public int Index;
    public CombatAutoBattleRole[] Roles = Array.Empty<CombatAutoBattleRole>();
    public int MatchCount;
    public int Wins;
    public int Losses;
    public int Timeouts;
    public float WinRate;
}

[Serializable]
public sealed class CombatCompositionSweepReport
{
    public int CandidateCount;
    public int MatchesPerCandidate;
    public int CompletedCandidates;
    public List<CombatCompositionCandidateResult> Ranking = new();
}

public static class CombatCompositionSweepConfigLoader
{
    public static bool TryLoadFromFile(string path, out CombatCompositionSweepConfig config)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        config = JsonUtility.FromJson<CombatCompositionSweepConfig>(File.ReadAllText(path));
        return config != null;
    }
}

public static class CombatCompositionSweepReportWriter
{
    public static string Write(CombatCompositionSweepReport report, string path)
    {
        report.Ranking.Sort((a, b) =>
        {
            int byWinRate = b.WinRate.CompareTo(a.WinRate);
            if (byWinRate != 0) return byWinRate;
            int byWins = b.Wins.CompareTo(a.Wins);
            return byWins != 0 ? byWins : a.Index.CompareTo(b.Index);
        });
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonUtility.ToJson(report, prettyPrint: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}

/// <summary>
/// Builds party compositions under auto-battle sweep constraints (docs/AI/性格.md affinities).
/// </summary>
public static class CombatCompositionSweepGenerator
{
    private static readonly WeaponKind[] AllWeapons =
    {
        WeaponKind.Sword, WeaponKind.Shield, WeaponKind.Wand,
        WeaponKind.Grimoire, WeaponKind.Bible, WeaponKind.Rosary,
    };

    private static readonly CombatAiPersonalityKind[] AffinitySwordWand =
    {
        CombatAiPersonalityKind.BattleJunkie,
        CombatAiPersonalityKind.Cunning,
        CombatAiPersonalityKind.Reckless,
    };

    private static readonly CombatAiPersonalityKind[] AffinityShield =
    {
        CombatAiPersonalityKind.AttentionSeeker,
        CombatAiPersonalityKind.Devoted,
        CombatAiPersonalityKind.Lonely,
    };

    private static readonly CombatAiPersonalityKind[] AffinitySupport =
    {
        CombatAiPersonalityKind.Devoted,
        CombatAiPersonalityKind.Lonely,
    };

    // Affinity-valid combo seeds from docs/AI/コンボ.md
    private static readonly CombatAutoBattleRole[][] ComboSeeds =
    {
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Reckless },
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.Devoted },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Reckless },
            new CombatAutoBattleRole { Weapon = WeaponKind.Rosary, Personality = CombatAiPersonalityKind.Lonely },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.BattleJunkie },
            new CombatAutoBattleRole { Weapon = WeaponKind.Bible, Personality = CombatAiPersonalityKind.Lonely },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.BattleJunkie },
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.Devoted },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.AttentionSeeker },
            new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.BattleJunkie },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.AttentionSeeker },
            new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Reckless },
        },
        new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.Devoted },
            new CombatAutoBattleRole { Weapon = WeaponKind.Rosary, Personality = CombatAiPersonalityKind.Lonely },
        },
    };

    public static bool IsLegalWeaponComposition(IReadOnlyList<WeaponKind> weapons, int minPartySize, int maxPartySize)
    {
        if (weapons == null || weapons.Count < minPartySize || weapons.Count > maxPartySize) return false;

        int swordWand = 0, shield = 0, bible = 0, grimoire = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            switch (weapons[i])
            {
                case WeaponKind.Sword:
                case WeaponKind.Wand:
                    swordWand++;
                    break;
                case WeaponKind.Shield:
                    shield++;
                    break;
                case WeaponKind.Bible:
                    bible++;
                    break;
                case WeaponKind.Grimoire:
                    grimoire++;
                    break;
                case WeaponKind.Rosary:
                    break;
                default:
                    return false;
            }
        }

        return swordWand >= 2 && shield <= 2 && bible <= 2 && grimoire <= 2;
    }

    public static List<CombatCompositionCandidate> Generate(CombatCompositionSweepConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        int minSize = Mathf.Max(1, config.MinPartySize);
        int maxSize = Mathf.Max(minSize, config.MaxPartySize);
        var results = new List<CombatCompositionCandidate>();
        var seen = new HashSet<string>();

        if (config.Candidates != null && config.Candidates.Length > 0)
        {
            for (int i = 0; i < config.Candidates.Length; i++)
            {
                CombatCompositionCandidate candidate = config.Candidates[i];
                if (candidate?.Roles == null || candidate.Roles.Length == 0) continue;
                if (candidate.Roles.Length < minSize || candidate.Roles.Length > maxSize)
                {
                    Debug.LogWarning($"[編成探索] 明示候補{i}の人数 {candidate.Roles.Length} が範囲外 ({minSize}-{maxSize}) のためスキップします。");
                    continue;
                }

                if (!seen.Add(BuildKey(candidate.Roles))) continue;
                results.Add(CloneCandidate(candidate.Roles));
            }

            return results;
        }

        var rng = new System.Random(config.BaseSeed);
        int target = Mathf.Max(1, config.CandidateCount);
        int attempts = 0;
        int maxAttempts = Mathf.Max(target * 40, 200);
        while (results.Count < target && attempts++ < maxAttempts)
        {
            CombatAutoBattleRole[] roles = GenerateOne(rng, minSize, maxSize);
            if (roles == null || !seen.Add(BuildKey(roles))) continue;
            results.Add(CloneCandidate(roles));
        }

        if (results.Count < target)
            Debug.LogWarning($"[編成探索] 候補を {results.Count}/{target} 件しか生成できませんでした。");

        return results;
    }

    private static CombatAutoBattleRole[] GenerateOne(System.Random rng, int minSize, int maxSize)
    {
        int size = rng.Next(minSize, maxSize + 1);
        var roles = new List<CombatAutoBattleRole>(size);
        CombatAutoBattleRole[] seed = ComboSeeds[rng.Next(ComboSeeds.Length)];
        for (int i = 0; i < seed.Length && roles.Count < size; i++)
            roles.Add(CloneRole(seed[i]));

        int guard = 0;
        while (roles.Count < size && guard++ < 64)
        {
            WeaponKind weapon = PickWeapon(rng, roles);
            if (weapon == WeaponKind.Unarmed) return null;
            roles.Add(new CombatAutoBattleRole
            {
                Weapon = weapon,
                Personality = PickPersonality(rng, weapon),
            });
        }

        if (roles.Count != size) return null;

        var weapons = new WeaponKind[roles.Count];
        for (int i = 0; i < roles.Count; i++)
            weapons[i] = roles[i].Weapon;
        return IsLegalWeaponComposition(weapons, minSize, maxSize) ? roles.ToArray() : null;
    }

    private static WeaponKind PickWeapon(System.Random rng, List<CombatAutoBattleRole> current)
    {
        int swordWand = 0, shield = 0, bible = 0, grimoire = 0;
        for (int i = 0; i < current.Count; i++)
        {
            switch (current[i].Weapon)
            {
                case WeaponKind.Sword:
                case WeaponKind.Wand:
                    swordWand++;
                    break;
                case WeaponKind.Shield:
                    shield++;
                    break;
                case WeaponKind.Bible:
                    bible++;
                    break;
                case WeaponKind.Grimoire:
                    grimoire++;
                    break;
            }
        }

        bool needSwordWand = swordWand < 2;
        var buffer = new List<WeaponKind>(6);
        for (int i = 0; i < AllWeapons.Length; i++)
        {
            WeaponKind weapon = AllWeapons[i];
            if (needSwordWand && weapon != WeaponKind.Sword && weapon != WeaponKind.Wand) continue;
            if (weapon == WeaponKind.Shield && shield >= 2) continue;
            if (weapon == WeaponKind.Bible && bible >= 2) continue;
            if (weapon == WeaponKind.Grimoire && grimoire >= 2) continue;
            buffer.Add(weapon);
        }

        return buffer.Count == 0 ? WeaponKind.Unarmed : buffer[rng.Next(buffer.Count)];
    }

    private static CombatAiPersonalityKind PickPersonality(System.Random rng, WeaponKind weapon)
    {
        CombatAiPersonalityKind[] options = AffinitiesFor(weapon);
        return options[rng.Next(options.Length)];
    }

    private static CombatAiPersonalityKind[] AffinitiesFor(WeaponKind weapon)
    {
        switch (weapon)
        {
            case WeaponKind.Sword:
            case WeaponKind.Wand:
                return AffinitySwordWand;
            case WeaponKind.Shield:
                return AffinityShield;
            default:
                return AffinitySupport;
        }
    }

    private static string BuildKey(CombatAutoBattleRole[] roles)
    {
        var sorted = new List<CombatAutoBattleRole>(roles);
        sorted.Sort((a, b) =>
        {
            int byWeapon = ((int)a.Weapon).CompareTo((int)b.Weapon);
            return byWeapon != 0 ? byWeapon : ((int)a.Personality).CompareTo((int)b.Personality);
        });

        var sb = new StringBuilder(sorted.Count * 8);
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append((int)sorted[i].Weapon).Append(':').Append((int)sorted[i].Personality);
        }

        return sb.ToString();
    }

    private static CombatCompositionCandidate CloneCandidate(CombatAutoBattleRole[] roles)
    {
        var clone = new CombatAutoBattleRole[roles.Length];
        for (int i = 0; i < roles.Length; i++)
            clone[i] = CloneRole(roles[i]);
        return new CombatCompositionCandidate { Roles = clone };
    }

    private static CombatAutoBattleRole CloneRole(CombatAutoBattleRole role)
    {
        return new CombatAutoBattleRole { Weapon = role.Weapon, Personality = role.Personality };
    }
}
