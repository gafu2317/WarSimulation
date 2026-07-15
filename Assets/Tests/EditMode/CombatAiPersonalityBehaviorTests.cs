using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiPersonalityBehaviorTests
{
    [TestCase(CombatAiPersonalityKind.AttentionSeeker, 36f)]
    [TestCase(CombatAiPersonalityKind.BattleJunkie, 68f)]
    [TestCase(CombatAiPersonalityKind.Calm, -4f)]
    [TestCase(CombatAiPersonalityKind.Cautious, -8f)]
    [TestCase(CombatAiPersonalityKind.Clumsy, 0f)]
    [TestCase(CombatAiPersonalityKind.Coward, -8f)]
    [TestCase(CombatAiPersonalityKind.Cunning, -4f)]
    [TestCase(CombatAiPersonalityKind.Despicable, -6.4f)]
    [TestCase(CombatAiPersonalityKind.Devoted, 0f)]
    [TestCase(CombatAiPersonalityKind.Gossiper, 0f)]
    [TestCase(CombatAiPersonalityKind.HotBlooded, 42f)]
    [TestCase(CombatAiPersonalityKind.Innocent, -120f)]
    [TestCase(CombatAiPersonalityKind.Lazy, -1.6f)]
    [TestCase(CombatAiPersonalityKind.Lecherous, 0f)]
    [TestCase(CombatAiPersonalityKind.Lonely, 0f)]
    [TestCase(CombatAiPersonalityKind.LoneWolf, 32f)]
    [TestCase(CombatAiPersonalityKind.OverlySerious, 0f)]
    [TestCase(CombatAiPersonalityKind.Reckless, -200f)]
    [TestCase(CombatAiPersonalityKind.Unstable, 0f)]
    public void 性格ごとの攻撃目的補正を維持する(CombatAiPersonalityKind kind, float expected)
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(4f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
            CombatAiContext context = CreateContext(owner, enemy);
            CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);

            float score = CombatAiPersonalityBehavior.GetObjectiveScore(
                context,
                profile,
                assessment,
                CombatObjective.AttackEnemy);

            Assert.That(score, Is.EqualTo(expected).Within(0.001f));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 不思議ちゃんの攻撃目的補正はランダム候補だけを加算する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(4f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Eccentric);
            CombatAiContext context = CreateContext(owner, enemy);

            float score = CombatAiPersonalityBehavior.GetObjectiveScore(
                context,
                profile,
                CombatAiAssessmentBuilder.Build(context),
                CombatObjective.AttackEnemy);

            Assert.That(score == 0f || score == 160f, Is.True);
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 猪突猛進は生存中の敵より敵魔石を選ぶ()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(2f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Reckless);
            CombatAiContext context = CreateContext(owner, enemy, null, new Vector3(20f, 0f, 0f));

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
            Assert.That(plan.MoveTarget.Destination, Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 天真爛漫は攻撃せず敵の周囲へ移動する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(4f, 0f, 0f));
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new DamageSkill());
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Innocent);
            CombatAiContext context = CreateContext(owner, enemy);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.Skill, Is.Null);
            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(Vector3.Distance(plan.MoveTarget.Destination, enemy.transform.position), Is.GreaterThan(4f));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 献身的は味方と敵の間へ移動する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, new Vector3(4f, 0f, 0f));
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(10f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Devoted);
            CombatAiContext context = CreateContext(owner, enemy, ally);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.Destination.x, Is.GreaterThan(ally.transform.position.x));
            Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(enemy.transform.position.x));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(allyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    private static Character CreateCharacter(GameObject gameObject, CombatTeam team, Vector3 position)
    {
        Character character = gameObject.AddComponent<Character>();
        character.SetTeam(team);
        character.Health.Initialize(30);
        character.EquipWeapon(new Sword());
        gameObject.transform.position = position;
        return character;
    }

    private static CombatAiContext CreateContext(
        Character owner,
        Character enemy,
        Character ally = null,
        Vector3? enemyStonePosition = null)
    {
        CombatCharacterIntel enemyIntel = CreateIntel(enemy);
        CombatCharacterIntel[] allyIntel = ally != null ? new[] { CreateIntel(ally) } : Array.Empty<CombatCharacterIntel>();
        return new CombatAiContext(
            owner,
            new[] { enemyIntel },
            allyIntel,
            CombatMapSystem.Weather.Sunny,
            false,
            default,
            enemyStonePosition.HasValue,
            enemyStonePosition.GetValueOrDefault(),
            Array.Empty<Vector3>(),
            Array.Empty<Vector3>(),
            Array.Empty<Vector3>(),
            enemyStonePosition.HasValue,
            enemyStonePosition.HasValue ? 100 : 0,
            enemyStonePosition.HasValue ? 100 : 0);
    }

    private static CombatCharacterIntel CreateIntel(Character character)
    {
        return new CombatCharacterIntel(
            character,
            character.Team,
            character.transform.position,
            true,
            false,
            true,
            character.transform.position,
            float.PositiveInfinity,
            false,
            character.Health.HP,
            character.Health.MaxHP,
            character.Health.CanAct,
            character.EquippedWeapon.Kind,
            character.EquippedWeapon.Range,
            Array.Empty<CombatStatusEffectSnapshot>(),
            false,
            default);
    }

    private sealed class DamageSkill : SkillBase
    {
        public override string Name => "攻撃";
        public override float MaxRange => 6f;
        public override int EstimateDamage(Character self, SkillExecutionContext context, Character target) => 10;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }
}
