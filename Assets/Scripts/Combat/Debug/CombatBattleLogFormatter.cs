using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WarSimulation.Combat.Map;

public sealed class CombatBattleLogFormatter
{
    private static readonly HashSet<string> BasicAttackSkillNames = new HashSet<string>
    {
        "斬撃",
        "盾撃",
        "魔弾",
        "通常攻撃",
    };

    private readonly Dictionary<string, int> _skillTally = new Dictionary<string, int>();

    public static bool IsBasicAttackSkillName(string skillName)
    {
        return !string.IsNullOrEmpty(skillName) && BasicAttackSkillNames.Contains(skillName);
    }

    public void Reset()
    {
        _skillTally.Clear();
    }

    public string FormatBattleHeader(string logFilePath, string weatherLabel)
    {
        var sb = new StringBuilder(256);
        sb.AppendLine("# CombatBattleLog");
        sb.AppendLine("file=" + logFilePath);
        if (!string.IsNullOrEmpty(weatherLabel))
        {
            sb.AppendLine("weather=" + weatherLabel);
        }

        return sb.ToString().TrimEnd();
    }

    public string FormatObjectiveChange(
        float battleTimeSeconds,
        string characterName,
        string weaponLabel,
        CombatObjective previous,
        CombatObjective next,
        IReadOnlyList<string> reasonLabels)
    {
        var sb = new StringBuilder(256);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" OBJECTIVE ");
        sb.Append(characterName);
        if (!string.IsNullOrEmpty(weaponLabel))
        {
            sb.Append('(');
            sb.Append(weaponLabel);
            sb.Append(')');
        }

        sb.Append(' ');
        sb.Append(CombatAiDebugLabels.ObjectiveShort(previous));
        sb.Append(" -> ");
        sb.Append(CombatAiDebugLabels.ObjectiveShort(next));

        if (reasonLabels != null && reasonLabels.Count > 0)
        {
            sb.Append(" reason=");
            for (int i = 0; i < reasonLabels.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(reasonLabels[i]);
            }
        }

        return sb.ToString();
    }

    public string FormatSkillUsed(float battleTimeSeconds, string characterName, string skillName, string targetName)
    {
        if (IsBasicAttackSkillName(skillName))
        {
            IncrementSkillTally(skillName);
            return null;
        }

        var sb = new StringBuilder(192);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" SKILL ");
        sb.Append(characterName);
        sb.Append(" used ");
        sb.Append(skillName);
        if (!string.IsNullOrEmpty(targetName))
        {
            sb.Append(" target=");
            sb.Append(targetName);
        }

        return sb.ToString();
    }

    public string FormatSnapshot(
        float battleTimeSeconds,
        int ownStoneHp,
        int ownStoneMaxHp,
        int enemyStoneHp,
        int enemyStoneMaxHp,
        int allyAliveCount,
        int enemyAliveCount)
    {
        var sb = new StringBuilder(192);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" SNAPSHOT ownStoneHP=");
        sb.Append(ownStoneHp);
        sb.Append('/');
        sb.Append(ownStoneMaxHp);
        sb.Append(" enemyStoneHP=");
        sb.Append(enemyStoneHp);
        sb.Append('/');
        sb.Append(enemyStoneMaxHp);
        sb.Append(" alive=");
        sb.Append(allyAliveCount);
        sb.Append('v');
        sb.Append(enemyAliveCount);
        return sb.ToString();
    }

    public string FormatDefeated(float battleTimeSeconds, string victimName, string killerName)
    {
        var sb = new StringBuilder(128);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" DEFEATED ");
        sb.Append(victimName);
        if (!string.IsNullOrEmpty(killerName))
        {
            sb.Append(" killer=");
            sb.Append(killerName);
        }

        return sb.ToString();
    }

    public string FormatStoneDestroyed(float battleTimeSeconds, FeatureType stoneType)
    {
        var sb = new StringBuilder(96);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" STONE_DESTROYED type=");
        sb.Append(stoneType);
        return sb.ToString();
    }

    public string FormatBattleEnd(
        float battleTimeSeconds,
        CombatBattleState outcome,
        int ownStoneHp,
        int enemyStoneHp,
        int allyAliveCount,
        int enemyAliveCount)
    {
        var sb = new StringBuilder(256);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" BATTLE_END outcome=");
        sb.Append(outcome);
        sb.Append(" duration=");
        sb.Append(battleTimeSeconds.ToString("0.0", CultureInfo.InvariantCulture));
        sb.Append('s');
        sb.Append(" ownStoneHP=");
        sb.Append(ownStoneHp);
        sb.Append(" enemyStoneHP=");
        sb.Append(enemyStoneHp);
        sb.Append(" alive=");
        sb.Append(allyAliveCount);
        sb.Append('v');
        sb.Append(enemyAliveCount);

        string tallyLine = BuildSkillTallyLine();
        if (!string.IsNullOrEmpty(tallyLine))
        {
            sb.AppendLine();
            sb.Append("  skillTally: ");
            sb.Append(tallyLine);
        }

        return sb.ToString();
    }

    public string BuildSkillTallyLine()
    {
        if (_skillTally.Count == 0) return string.Empty;

        var sb = new StringBuilder(128);
        bool first = true;
        foreach (KeyValuePair<string, int> pair in _skillTally)
        {
            if (!first) sb.Append(", ");
            sb.Append(pair.Key);
            sb.Append(" x");
            sb.Append(pair.Value);
            first = false;
        }

        return sb.ToString();
    }

    private void IncrementSkillTally(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        _skillTally.TryGetValue(skillName, out int count);
        _skillTally[skillName] = count + 1;
    }

    private static string FormatTimePrefix(float battleTimeSeconds)
    {
        return "[t=" + battleTimeSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s]";
    }
}
