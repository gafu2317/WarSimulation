using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatBattleLogFormatterTests
{
    [Test]
    public void FormatSkillUsed_BasicAttack_IsTalliedNotLogged()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatSkillUsed(
            12.4f,
            "Char_Sword01",
            "斬撃",
            "Enemy01",
            actionId: 1,
            decisionTick: 1,
            skillId: SkillId.None);

        Assert.That(line, Is.Null);
        Assert.That(formatter.BuildSkillTallyLine(), Does.Contain("斬撃 x1"));
    }

    [Test]
    public void FormatSkillUsed_NonBasicAttack_ReturnsLine()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatSkillUsed(
            18.6f,
            "Char_Bible01",
            "STRバフ",
            "Char_Sword02",
            actionId: 2,
            decisionTick: 1,
            skillId: SkillId.Bible_StrBuff);

        Assert.That(line, Does.Contain("[t=18.6s]"));
        Assert.That(line, Does.Contain("SKILL Char_Bible01 used STRバフ"));
        Assert.That(line, Does.Contain("target=Char_Sword02"));
        Assert.That(formatter.BuildSkillTallyLine(), Is.Empty);
    }

    [Test]
    public void FormatObjectiveChange_IncludesObjectiveLabelsAndReasons()
    {
        var formatter = new CombatBattleLogFormatter();
        var reasons = new List<string> { "敵が射程内", "敵魔石位置既知" };

        string line = formatter.FormatObjectiveChange(
            12.4f,
            "Char_Wand01",
            "Wand",
            CombatObjective.AttackEnemy,
            CombatObjective.DestroyEnemyStone,
            reasons);

        Assert.That(line, Does.Contain("OBJECTIVE Char_Wand01(Wand)"));
        Assert.That(line, Does.Contain("敵を攻撃 -> 敵魔石を破壊"));
        Assert.That(line, Does.Contain("reason=敵が射程内,敵魔石位置既知"));
    }

    [Test]
    public void FormatBattleEnd_IncludesSkillTally()
    {
        var formatter = new CombatBattleLogFormatter();
        formatter.FormatSkillUsed(1f, "A", "斬撃", null, 1, 1, SkillId.None);
        formatter.FormatSkillUsed(2f, "B", "魔弾", null, 2, 1, SkillId.None);
        formatter.FormatSkillUsed(3f, "A", "斬撃", null, 3, 2, SkillId.None);

        string line = formatter.FormatBattleEnd(
            182.1f,
            "Victory",
            ownStoneHp: 88,
            enemyStoneHp: 0,
            allyAliveCount: 4,
            enemyAliveCount: 0);

        Assert.That(line, Does.Contain("BATTLE_END outcome=Victory"));
        Assert.That(line, Does.Contain("alive=4v0"));
        Assert.That(line, Does.Contain("skillTally:"));
        Assert.That(line, Does.Contain("斬撃 x2"));
        Assert.That(line, Does.Contain("魔弾 x1"));
    }

    [Test]
    public void FormatAiPlan_IncludesStateReasonActionAndDestination()
    {
        var formatter = new CombatBattleLogFormatter();
        var plan = new CombatAiPlan(
            CombatObjective.DestroyEnemyStone,
            CombatMoveTarget.ForPosition(new Vector3(12f, 0f, 7f)),
            null,
            SkillExecutionContext.None,
            CombatAiMoveCode.AdvanceAssaultRoute,
            CombatAiReasonCode.EnemyStoneKnown);

        string line = formatter.FormatAiPlan(3.2f, "Char_Sword01", CombatObjective.Search, plan, 1, 1);

        Assert.That(line, Does.Contain("AI_PLAN actor=Char_Sword01"));
        Assert.That(line, Does.Contain("state=索敵->敵魔石を破壊"));
        Assert.That(line, Does.Contain("reason=EnemyStoneKnown"));
        Assert.That(line, Does.Contain("action=" + CombatAiMoveCode.AdvanceAssaultRoute));
        Assert.That(line, Does.Contain("destination=(12.0,7.0)"));
    }

    [Test]
    public void FormatBattleHeader_IncludesBattleMetadata()
    {
        var formatter = new CombatBattleLogFormatter();
        var metadata = new CombatBattleLogMetadata(
            mapName: "Forest",
            seed: 42,
            stonePositionsReversed: true,
            weatherLabel: "Rain",
            timeScale: 6f,
            fixedDeltaTime: 0.1f,
            preserveFixedDeltaTime: "false",
            unityVersion: "6000.4.3f1",
            playerBuildGuid: "build-guid",
            participants: "A:Ally[剣/慎重]|E:Enemy[杖/攻撃的]");

        string header = formatter.FormatBattleHeader("battle.log", metadata);

        Assert.That(header, Does.Contain("map=Forest"));
        Assert.That(header, Does.Contain("seed=42"));
        Assert.That(header, Does.Contain("stonePositionsReversed=true"));
        Assert.That(header, Does.Contain("weather=Rain"));
        Assert.That(header, Does.Contain("timeScale=6.0"));
        Assert.That(header, Does.Contain("fixedDeltaTime=0.1"));
        Assert.That(header, Does.Contain("preserveFixedDeltaTime=false"));
        Assert.That(header, Does.Contain("unityVersion=6000.4.3f1"));
        Assert.That(header, Does.Contain("playerBuildGuid=build-guid"));
        Assert.That(header, Does.Contain("participants=A:Ally[剣/慎重]|E:Enemy[杖/攻撃的]"));
    }

    [Test]
    public void FormatAiPlan_IncludesCorrelationAndSkillContext()
    {
        var formatter = new CombatBattleLogFormatter();
        GameObject targetObject = new GameObject("SkillTarget");
        Character target = targetObject.AddComponent<Character>();
        try
        {
            var skill = new IdentifiedSkill(new TestSkill("雷撃"), SkillId.Wand_Bolt);
            var context = new SkillExecutionContext(
                target,
                null,
                hasTargetPoint: true,
                new Vector3(2.4f, 0f, 3.6f),
                new[] { target },
                Array.Empty<MagicStone>());
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.ForPosition(new Vector3(12f, 0f, 7f), "assault-route-1"),
                skill,
                context,
                CombatAiMoveCode.AdvanceAssaultRoute,
                CombatAiReasonCode.EnemyInRange);

            string line = formatter.FormatAiPlan(3.2f, "Actor", CombatObjective.Search, plan, 7, 11);

            Assert.That(line, Does.Contain("planId=7"));
            Assert.That(line, Does.Contain("decisionTick=11"));
            Assert.That(line, Does.Contain("route=assault-route-1"));
            Assert.That(line, Does.Contain("skillId=Wand_Bolt"));
            Assert.That(line, Does.Contain("skillTarget=SkillTarget"));
            Assert.That(line, Does.Contain("skillPoint=(2.4,3.6)"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void HasMeaningfulPlanChange_IgnoresDestinationOnlyChange()
    {
        var previous = new CombatAiPlan(
            CombatObjective.DestroyEnemyStone,
            CombatMoveTarget.ForPosition(new Vector3(1f, 0f, 2f), "route-1"),
            null,
            SkillExecutionContext.None,
            CombatAiMoveCode.AdvanceAssaultRoute,
            CombatAiReasonCode.EnemyStoneKnown);
        var destinationOnly = new CombatAiPlan(
            CombatObjective.DestroyEnemyStone,
            CombatMoveTarget.ForPosition(new Vector3(4f, 0f, 5f), "route-1"),
            null,
            SkillExecutionContext.None,
            CombatAiMoveCode.AdvanceAssaultRoute,
            CombatAiReasonCode.EnemyStoneKnown);
        var routeChanged = new CombatAiPlan(
            CombatObjective.DestroyEnemyStone,
            CombatMoveTarget.ForPosition(new Vector3(4f, 0f, 5f), "route-2"),
            null,
            SkillExecutionContext.None,
            CombatAiMoveCode.AdvanceAssaultRoute,
            CombatAiReasonCode.EnemyStoneKnown);

        Assert.That(CombatBattleLogFormatter.HasMeaningfulPlanChange(previous, destinationOnly), Is.False);
        Assert.That(CombatBattleLogFormatter.HasMeaningfulPlanChange(previous, routeChanged), Is.True);
    }

    [Test]
    public void FormatAiPlanRepeat_IncludesSuppressedPlanSummary()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatAiPlanRepeat(8f, "Actor", 3, 5, 2.5f, 4, "(12.0,7.0)");

        Assert.That(line, Does.Contain("AI_PLAN_REPEAT actor=Actor"));
        Assert.That(line, Does.Contain("planId=3"));
        Assert.That(line, Does.Contain("count=5"));
        Assert.That(line, Does.Contain("duration=2.5"));
        Assert.That(line, Does.Contain("destinationUpdates=4"));
        Assert.That(line, Does.Contain("lastDestination=(12.0,7.0)"));
    }

    [Test]
    public void Logger_AggregatesSameSemanticPlan()
    {
        GameObject loggerObject = new GameObject("CombatBattleEventLogger");
        GameObject actorObject = new GameObject("Actor");
        string path = Path.GetTempFileName();
        StreamWriter writer = null;
        try
        {
            CombatBattleEventLogger logger = loggerObject.AddComponent<CombatBattleEventLogger>();
            Character actor = actorObject.AddComponent<Character>();
            SetPrivateField(logger, "_battleFlow", null);
            writer = new StreamWriter(path);
            SetPrivateField(logger, "_writer", writer);

            var firstPlan = new CombatAiPlan(
                CombatObjective.DestroyEnemyStone,
                CombatMoveTarget.ForPosition(new Vector3(1f, 0f, 2f), "route-1"),
                null,
                SkillExecutionContext.None,
                CombatAiMoveCode.AdvanceAssaultRoute,
                CombatAiReasonCode.EnemyStoneKnown);
            var updatedDestination = new CombatAiPlan(
                CombatObjective.DestroyEnemyStone,
                CombatMoveTarget.ForPosition(new Vector3(4f, 0f, 5f), "route-1"),
                null,
                SkillExecutionContext.None,
                CombatAiMoveCode.AdvanceAssaultRoute,
                CombatAiReasonCode.EnemyStoneKnown);
            MethodInfo onPlanSelected = typeof(CombatBattleEventLogger).GetMethod(
                "OnPlanSelected",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onPlanSelected.Invoke(logger, new object[] { actor, CombatAiPlan.None, firstPlan });
            onPlanSelected.Invoke(logger, new object[] { actor, firstPlan, updatedDestination });

            MethodInfo flushPlanRepeats = typeof(CombatBattleEventLogger).GetMethod(
                "FlushPlanRepeats",
                BindingFlags.Instance | BindingFlags.NonPublic);
            flushPlanRepeats.Invoke(logger, new object[] { 2f });
            writer.Flush();
            writer.Dispose();
            SetPrivateField(logger, "_writer", null);
            writer = null;

            string output = File.ReadAllText(path);
            Assert.That(output, Does.Contain("AI_PLAN actor=Actor planId=1"));
            Assert.That(output, Does.Contain("AI_PLAN_REPEAT actor=Actor planId=1 count=1"));
            Assert.That(output, Does.Contain("destinationUpdates=1"));
            Assert.That(output, Does.Not.Contain("AI_PLAN actor=Actor planId=2"));
        }
        finally
        {
            writer?.Dispose();
            if (File.Exists(path)) File.Delete(path);
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(loggerObject);
        }
    }

    [Test]
    public void FormatAiExecution_IncludesStartedActionsAndFailure()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatAiExecution(
            3.7f,
            "Char_Sword01",
            CombatAiPlan.None,
            1,
            1,
            true,
            false,
            "skill-not-ready");

        Assert.That(line, Does.Contain("AI_EXECUTE actor=Char_Sword01"));
        Assert.That(line, Does.Contain("movementStarted=true"));
        Assert.That(line, Does.Contain("skillStarted=false"));
        Assert.That(line, Does.Contain("failure=skill-not-ready"));
    }

    [Test]
    public void ShouldLogAiExecution_NoOpIsSuppressed()
    {
        Assert.That(CombatBattleLogFormatter.ShouldLogAiExecution(false, false, null), Is.False);
        Assert.That(CombatBattleLogFormatter.ShouldLogAiExecution(false, false, "failed"), Is.True);
        Assert.That(CombatBattleLogFormatter.ShouldLogAiExecution(true, false, null), Is.True);
    }

    [TestCase(CombatSkillActionOutcome.NoEffect)]
    [TestCase(CombatSkillActionOutcome.Failed)]
    [TestCase(CombatSkillActionOutcome.Cancelled)]
    public void FormatSkillResult_DistinguishesNonSuccessOutcomes(CombatSkillActionOutcome outcome)
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatSkillResult(
            4f,
            "Actor",
            "STRバフ",
            "Target",
            outcome,
            actionId: 41,
            decisionTick: 3,
            skillId: SkillId.Grimoire_StrDebuff);

        Assert.That(line, Does.Contain("SKILL_RESULT actor=Actor"));
        Assert.That(line, Does.Contain("outcome=" + outcome));
        Assert.That(line, Does.Contain("actionId=41"));
        Assert.That(line, Does.Contain("decisionTick=3"));
        Assert.That(line, Does.Not.Contain("SKILL Actor used"));
    }

    [Test]
    public void FormatStoneTarget_ContainsOnlyCombatCorrelationFields()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatStoneTarget(5f, "Actor", 12, 8, 41, 3);

        Assert.That(line, Does.Contain("STONE_TARGET actor=Actor"));
        Assert.That(line, Does.Contain("featureIndex=12"));
        Assert.That(line, Does.Contain("amount=8"));
        Assert.That(line, Does.Contain("actionId=41"));
        Assert.That(line, Does.Contain("decisionTick=3"));
        Assert.That(line, Does.Not.Contain("camera"));
        Assert.That(line, Does.Not.Contain("orbit"));
        Assert.That(line, Does.Not.Contain("editor"));
    }

    [Test]
    public void IsBasicAttackSkillName_RecognizesKnownNames()
    {
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("斬撃"), Is.True);
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("通常攻撃"), Is.True);
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("STRバフ"), Is.False);
    }

    private sealed class TestSkill : SkillBase
    {
        private readonly string _name;

        public TestSkill(string name)
        {
            _name = name;
        }

        public override string Name => _name;

        public override void Execute(Character self, SkillExecutionContext context)
        {
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        typeof(CombatBattleEventLogger)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
