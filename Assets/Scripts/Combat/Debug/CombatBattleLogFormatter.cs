using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using WarSimulation.Combat.Map;

public readonly struct CombatBattleLogMetadata
{
    public CombatBattleLogMetadata(
        string mapName,
        int seed,
        bool stonePositionsReversed,
        string weatherLabel,
        float timeScale,
        float fixedDeltaTime,
        string preserveFixedDeltaTime,
        string unityVersion,
        string playerBuildGuid,
        string participants)
    {
        MapName = mapName;
        Seed = seed;
        StonePositionsReversed = stonePositionsReversed;
        WeatherLabel = weatherLabel;
        TimeScale = timeScale;
        FixedDeltaTime = fixedDeltaTime;
        PreserveFixedDeltaTime = preserveFixedDeltaTime;
        UnityVersion = unityVersion;
        PlayerBuildGuid = playerBuildGuid;
        Participants = participants;
    }

    public string MapName { get; }
    public int Seed { get; }
    public bool StonePositionsReversed { get; }
    public string WeatherLabel { get; }
    public float TimeScale { get; }
    public float FixedDeltaTime { get; }
    public string PreserveFixedDeltaTime { get; }
    public string UnityVersion { get; }
    public string PlayerBuildGuid { get; }
    public string Participants { get; }
}

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

    public static bool HasMeaningfulPlanChange(CombatAiPlan previous, CombatAiPlan next)
    {
        return BuildPlanKey(previous) != BuildPlanKey(next);
    }

    public static bool ShouldLogAiExecution(bool movementStarted, bool skillStarted, string failureReason)
    {
        return movementStarted || skillStarted || !string.IsNullOrEmpty(failureReason);
    }

    public void Reset()
    {
        _skillTally.Clear();
    }

    public string FormatBattleHeader(string logFilePath, CombatBattleLogMetadata metadata)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("# CombatBattleLog");
        sb.AppendLine("file=" + logFilePath);
        sb.AppendLine("map=" + ValueOrUnknown(metadata.MapName));
        sb.AppendLine("seed=" + metadata.Seed);
        sb.AppendLine("stonePositionsReversed=" + (metadata.StonePositionsReversed ? "true" : "false"));
        sb.AppendLine("weather=" + ValueOrUnknown(metadata.WeatherLabel));
        sb.AppendLine("timeScale=" + FormatFloat(metadata.TimeScale));
        sb.AppendLine("fixedDeltaTime=" + FormatFloat(metadata.FixedDeltaTime));
        sb.AppendLine("preserveFixedDeltaTime=" + ValueOrUnknown(metadata.PreserveFixedDeltaTime));
        sb.AppendLine("unityVersion=" + ValueOrUnknown(metadata.UnityVersion));
        sb.AppendLine("playerBuildGuid=" + ValueOrUnknown(metadata.PlayerBuildGuid));
        sb.AppendLine("participants=" + ValueOrUnknown(metadata.Participants));
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

    public string FormatAiPlan(
        float battleTimeSeconds,
        string characterName,
        CombatObjective previous,
        CombatAiPlan plan,
        int planId,
        int decisionTick)
    {
        var sb = new StringBuilder(320);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" AI_PLAN ");
        sb.Append("actor=");
        sb.Append(characterName);
        sb.Append(" planId=");
        sb.Append(planId);
        sb.Append(" decisionTick=");
        sb.Append(decisionTick);
        sb.Append(" state=");
        sb.Append(CombatAiDebugLabels.ObjectiveShort(previous));
        sb.Append("->");
        sb.Append(CombatAiDebugLabels.ObjectiveShort(plan.Objective));
        sb.Append(" reason=");
        sb.Append(CombatAiDebugLabels.Reason(plan.TransitionReason));
        AppendPlanDetails(sb, plan);
        return sb.ToString();
    }

    public string FormatAiExecution(
        float battleTimeSeconds,
        string characterName,
        CombatAiPlan plan,
        int planId,
        int decisionTick,
        bool movementStarted,
        bool skillStarted,
        string failureReason)
    {
        var sb = new StringBuilder(280);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" AI_EXECUTE ");
        sb.Append("actor=");
        sb.Append(characterName);
        sb.Append(" planId=");
        sb.Append(planId);
        sb.Append(" decisionTick=");
        sb.Append(decisionTick);
        if (!string.IsNullOrEmpty(plan.ActionCode))
        {
            AppendPlanDetails(sb, plan);
        }

        sb.Append(" movementStarted=");
        sb.Append(movementStarted ? "true" : "false");
        sb.Append(" skillStarted=");
        sb.Append(skillStarted ? "true" : "false");
        if (!string.IsNullOrEmpty(failureReason))
        {
            sb.Append(" failure=");
            sb.Append(failureReason);
        }

        return sb.ToString();
    }

    public string FormatAiPlanRepeat(
        float battleTimeSeconds,
        string characterName,
        int planId,
        int count,
        float durationSeconds,
        int destinationUpdates,
        string lastDestination)
    {
        var sb = new StringBuilder(192);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" AI_PLAN_REPEAT actor=");
        sb.Append(characterName);
        sb.Append(" planId=");
        sb.Append(planId);
        sb.Append(" count=");
        sb.Append(count);
        sb.Append(" duration=");
        sb.Append(FormatFloat(durationSeconds));
        sb.Append(" destinationUpdates=");
        sb.Append(destinationUpdates);
        sb.Append(" lastDestination=");
        sb.Append(string.IsNullOrEmpty(lastDestination) ? "none" : lastDestination);
        return sb.ToString();
    }

    public string FormatAiCancelled(
        float battleTimeSeconds,
        string characterName,
        CombatAiPlan plan,
        int planId,
        int decisionTick,
        string reason)
    {
        var sb = new StringBuilder(220);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" AI_CANCELLED actor=");
        sb.Append(characterName);
        sb.Append(" planId=");
        sb.Append(planId);
        sb.Append(" decisionTick=");
        sb.Append(decisionTick);
        sb.Append(" reason=");
        sb.Append(reason);
        if (!string.IsNullOrEmpty(plan.ActionCode))
        {
            AppendPlanDetails(sb, plan);
        }

        return sb.ToString();
    }

    public string FormatSkillUsed(
        float battleTimeSeconds,
        string characterName,
        string skillName,
        string targetName,
        long actionId,
        int decisionTick,
        SkillId skillId)
    {
        if (IsBasicAttackSkillName(skillName))
        {
            IncrementSkillTally(skillName);
            return null;
        }

        var sb = new StringBuilder(224);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" SKILL ");
        sb.Append(characterName);
        sb.Append(" used ");
        sb.Append(skillName);
        AppendSkillCorrelation(sb, skillId, actionId, decisionTick);
        if (!string.IsNullOrEmpty(targetName))
        {
            sb.Append(" target=");
            sb.Append(targetName);
        }

        return sb.ToString();
    }

    public string FormatSkillResult(
        float battleTimeSeconds,
        string characterName,
        string skillName,
        string targetName,
        CombatSkillActionOutcome outcome,
        long actionId,
        int decisionTick,
        SkillId skillId)
    {
        var sb = new StringBuilder(224);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" SKILL_RESULT actor=");
        sb.Append(characterName);
        sb.Append(" skill=");
        sb.Append(skillName);
        sb.Append(" outcome=");
        sb.Append(outcome);
        AppendSkillCorrelation(sb, skillId, actionId, decisionTick);
        if (!string.IsNullOrEmpty(targetName))
        {
            sb.Append(" target=");
            sb.Append(targetName);
        }

        return sb.ToString();
    }

    public string FormatStoneTarget(
        float battleTimeSeconds,
        string actorName,
        int featureIndex,
        int amount,
        long actionId,
        int decisionTick)
    {
        var sb = new StringBuilder(160);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" STONE_TARGET actor=");
        sb.Append(actorName);
        sb.Append(" featureIndex=");
        sb.Append(featureIndex);
        sb.Append(" amount=");
        sb.Append(amount);
        sb.Append(" actionId=");
        sb.Append(actionId);
        sb.Append(" decisionTick=");
        sb.Append(decisionTick);
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
        string outcome,
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
        sb.Append(FormatFloat(battleTimeSeconds));
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

    public string FormatBattleAborted(float battleTimeSeconds, string reason)
    {
        var sb = new StringBuilder(128);
        sb.Append(FormatTimePrefix(battleTimeSeconds));
        sb.Append(" BATTLE_ABORTED reason=");
        sb.Append(reason);
        sb.Append(" duration=");
        sb.Append(FormatFloat(battleTimeSeconds));
        sb.Append('s');
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

    public static string FormatPosition(Vector3 position)
    {
        return "(" + position.x.ToString("0.0", CultureInfo.InvariantCulture) + "," +
            position.z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
    }

    private static void AppendPlanDetails(StringBuilder sb, CombatAiPlan plan)
    {
        sb.Append(" action=");
        sb.Append(plan.ActionCode);
        if (plan.MoveTarget.Kind == CombatMoveTargetKind.Character && plan.MoveTarget.TargetCharacter != null)
        {
            sb.Append(" target=");
            sb.Append(plan.MoveTarget.TargetCharacter.name);
        }
        else if (plan.MoveTarget.HasDestination)
        {
            sb.Append(" destination=");
            sb.Append(FormatPosition(plan.MoveTarget.Destination));
        }

        if (plan.MoveTarget.HasAssaultRouteKey)
        {
            sb.Append(" route=");
            sb.Append(plan.MoveTarget.AssaultRouteKey);
        }

        if (plan.Skill != null)
        {
            sb.Append(" skill=");
            sb.Append(plan.Skill.Name);
            sb.Append(" skillId=");
            sb.Append(plan.Skill.Id);
        }

        if (plan.SkillContext.PrimaryTarget != null)
        {
            sb.Append(" skillTarget=");
            sb.Append(plan.SkillContext.PrimaryTarget.name);
        }

        MagicStone skillStone = ResolveSkillStone(plan.SkillContext);
        if (skillStone != null)
        {
            sb.Append(" skillStone=");
            sb.Append(skillStone.FeatureIndex);
        }

        if (plan.SkillContext.HasTargetPoint)
        {
            sb.Append(" skillPoint=");
            sb.Append(FormatPosition(plan.SkillContext.TargetPoint));
        }
    }

    private static void AppendSkillCorrelation(StringBuilder sb, SkillId skillId, long actionId, int decisionTick)
    {
        sb.Append(" skillId=");
        sb.Append(skillId);
        sb.Append(" actionId=");
        sb.Append(actionId);
        sb.Append(" decisionTick=");
        sb.Append(decisionTick);
    }

    private static string BuildPlanKey(CombatAiPlan plan)
    {
        var sb = new StringBuilder(256);
        sb.Append(plan.Objective);
        sb.Append('|');
        sb.Append(plan.ActionCode);
        sb.Append('|');
        sb.Append(plan.MoveTarget.Kind);
        sb.Append('|');
        if (plan.MoveTarget.Kind == CombatMoveTargetKind.Character)
        {
            sb.Append(GetCharacterKey(plan.MoveTarget.TargetCharacter));
        }
        else if (plan.MoveTarget.HasAssaultRouteKey)
        {
            sb.Append(plan.MoveTarget.AssaultRouteKey);
        }

        sb.Append('|');
        sb.Append(plan.TransitionReason);
        sb.Append('|');
        sb.Append(plan.Skill != null ? plan.Skill.Id.ToString() : "none");
        sb.Append('|');
        sb.Append(plan.Skill != null ? plan.Skill.Name : "none");
        sb.Append('|');
        sb.Append(GetCharacterKey(plan.SkillContext.PrimaryTarget));
        sb.Append('|');
        MagicStone skillStone = ResolveSkillStone(plan.SkillContext);
        sb.Append(skillStone != null ? skillStone.FeatureIndex.ToString(CultureInfo.InvariantCulture) : "none");
        sb.Append('|');
        sb.Append(plan.SkillContext.HasTargetPoint
            ? FormatPosition(plan.SkillContext.TargetPoint)
            : "none");
        return sb.ToString();
    }

    private static MagicStone ResolveSkillStone(SkillExecutionContext context)
    {
        if (context.PrimaryStone != null) return context.PrimaryStone;
        if (context.ResolvedStones == null || context.ResolvedStones.Count == 0) return null;
        return context.ResolvedStones[0];
    }

    private static string GetCharacterKey(Character character)
    {
        if (character == null) return "none";
        if (character.BattleParticipantId != 0)
        {
            return "id:" + character.BattleParticipantId.ToString(CultureInfo.InvariantCulture);
        }

        return "name:" + character.name;
    }

    private void IncrementSkillTally(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        _skillTally.TryGetValue(skillName, out int count);
        _skillTally[skillName] = count + 1;
    }

    private static string FormatTimePrefix(float battleTimeSeconds)
    {
        return "[t=" + FormatFloat(battleTimeSeconds) + "s]";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}
