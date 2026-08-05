using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiPersonalityBehaviorTests
{
    [TestCase(CombatAiPersonalityKind.AttentionSeeker, 60f)]
    [TestCase(CombatAiPersonalityKind.BattleJunkie, 92f)]
    [TestCase(CombatAiPersonalityKind.Calm, -4f)]
    [TestCase(CombatAiPersonalityKind.Cautious, -8f)]
    [TestCase(CombatAiPersonalityKind.Clumsy, 0f)]
    [TestCase(CombatAiPersonalityKind.Coward, -8f)]
    [TestCase(CombatAiPersonalityKind.Cunning, 14f)]
    [TestCase(CombatAiPersonalityKind.Despicable, -6.4f)]
    [TestCase(CombatAiPersonalityKind.Devoted, -12f)]
    [TestCase(CombatAiPersonalityKind.Gossiper, 0f)]
    [TestCase(CombatAiPersonalityKind.HotBlooded, 54f)]
    [TestCase(CombatAiPersonalityKind.Innocent, -120f)]
    [TestCase(CombatAiPersonalityKind.Lazy, -1.6f)]
    [TestCase(CombatAiPersonalityKind.Lecherous, 6f)]
    [TestCase(CombatAiPersonalityKind.Lonely, -40f)]
    [TestCase(CombatAiPersonalityKind.LoneWolf, 46f)]
    [TestCase(CombatAiPersonalityKind.OverlySerious, 28f)]
    [TestCase(CombatAiPersonalityKind.Reckless, -200f)]
    [TestCase(CombatAiPersonalityKind.Unstable, 26f)]
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

            Assert.That(score == -40f || score == 220f, Is.True);
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
            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(
                Vector3.Distance(plan.MoveTarget.Destination, new Vector3(20f, 0f, 0f)),
                Is.EqualTo(1.7f).Within(0.01f));
            Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(20f));
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
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new DamageBlastSkill());
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

    [Test]
    public void 卑怯者は味方の影へ移動する()
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
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Despicable);
            CombatAiContext context = CreateContext(owner, enemy, ally);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(ally.transform.position.x));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(allyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 臆病者は近くの敵がいると味方の後ろへ下がる()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, new Vector3(6f, 0f, 0f));
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, new Vector3(4f, 0f, 0f));
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(10f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Coward);
            CombatAiContext context = CreateContext(owner, enemy, ally);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(ally.transform.position.x));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(allyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 寂しがりは離れた味方へ合流する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, new Vector3(8f, 0f, 0f));
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(20f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Lonely);
            CombatAiContext context = CreateContext(owner, enemy, ally);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.Kind, Is.EqualTo(CombatMoveTargetKind.Character));
            Assert.That(plan.MoveTarget.TargetCharacter, Is.EqualTo(ally));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(allyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 冷静は近接脅威があると後退する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(3f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Calm);
            CombatAiContext context = CreateContext(owner, enemy);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(plan.MoveTarget.Destination.x, Is.LessThan(owner.transform.position.x));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 一匹狼は味方がいない敵を選ぶ()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject allyObject = new GameObject("Ally");
        GameObject crowdedEnemyObject = new GameObject("CrowdedEnemy");
        GameObject loneEnemyObject = new GameObject("LoneEnemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, new Vector3(5f, 0f, 0f));
            Character crowdedEnemy = CreateCharacter(crowdedEnemyObject, CombatTeam.Enemy, new Vector3(6f, 0f, 0f));
            Character loneEnemy = CreateCharacter(loneEnemyObject, CombatTeam.Enemy, new Vector3(0f, 0f, 12f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.LoneWolf);
            CombatCharacterIntel crowdedIntel = CreateIntel(crowdedEnemy);
            CombatCharacterIntel loneIntel = CreateIntel(loneEnemy);
            CombatAiContext context = new CombatAiContext(
                owner,
                new[] { crowdedIntel, loneIntel },
                new[] { CreateIntel(ally) },
                CombatMapSystem.Weather.Sunny,
                false,
                default,
                false,
                default,
                Array.Empty<Vector3>(),
                Array.Empty<Vector3>(),
                Array.Empty<Vector3>(),
                false,
                0,
                0);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.MoveTarget.HasDestination, Is.True);
            Assert.That(
                Vector3.Distance(plan.MoveTarget.Destination, loneEnemy.transform.position),
                Is.LessThan(Vector3.Distance(plan.MoveTarget.Destination, crowdedEnemy.transform.position)));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(loneEnemyObject);
            UnityEngine.Object.DestroyImmediate(crowdedEnemyObject);
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

    private sealed class DamageBlastSkill : SkillBase
    {
        public override string Name => "攻撃";
        public override float MaxRange => 6f;
        public override int EstimateDamage(Character self, SkillExecutionContext context, Character target) => 10;
        public override void Execute(Character self, SkillExecutionContext context) { }
    }
}
