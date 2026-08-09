using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiPersonalityBehaviorTests
{
    [Test]
    public void BuiltInProfiles_ExcludeRemovedPersonality()
    {
        var profiles = CombatAiPersonalityProfile.CreateBuiltInProfiles();
        try
        {
            Assert.That(profiles, Has.Count.EqualTo(7));
            for (int i = 0; i < profiles.Count; i++)
            {
                Assert.That((int)profiles[i].Kind, Is.Not.EqualTo(6));
            }
        }
        finally
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(profiles[i]);
            }
        }
    }

    [TestCase(CombatAiPersonalityKind.AttentionSeeker, 0f)]
    [TestCase(CombatAiPersonalityKind.BattleJunkie, 1020f)]
    [TestCase(CombatAiPersonalityKind.Cunning, -42.4f)]
    [TestCase(CombatAiPersonalityKind.Devoted, -20f)]
    [TestCase(CombatAiPersonalityKind.Lonely, -200f)]
    [TestCase(CombatAiPersonalityKind.Reckless, -200f)]
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

            float score = CombatAiPersonalityBehavior.GetObjectiveScore(
                context,
                profile,
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
    public void 戦闘狂は敵攻撃目的を選ぶ()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(3f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BattleJunkie);
            CombatAiContext context = CreateContext(owner, enemy, null, new Vector3(20f, 0f, 0f));

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.AttackEnemy));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 献身的は低HPの味方のもとへ移動する()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject healthyAllyObject = new GameObject("HealthyAlly");
        GameObject hurtAllyObject = new GameObject("HurtAlly");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character healthyAlly = CreateCharacter(healthyAllyObject, CombatTeam.Ally, new Vector3(4f, 0f, 0f));
            Character hurtAlly = CreateCharacter(hurtAllyObject, CombatTeam.Ally, new Vector3(6f, 0f, 0f));
            hurtAlly.Health.Initialize(30, 10);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(20f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Devoted);
            CombatAiContext context = new CombatAiContext(
                owner,
                new[] { CreateIntel(enemy) },
                new[] { CreateIntel(healthyAlly), CreateIntel(hurtAlly) },
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

            Assert.That(plan.MoveTarget.Kind, Is.EqualTo(CombatMoveTargetKind.Character));
            Assert.That(plan.MoveTarget.TargetCharacter, Is.EqualTo(hurtAlly));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(hurtAllyObject);
            UnityEngine.Object.DestroyImmediate(healthyAllyObject);
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
    public void 寂しがりは単独時スキルを選ばない()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(3f, 0f, 0f));
            CombatEditModeTestUtil.SetAvailableCombatSkills(owner, new DamageBlastSkill());
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Lonely);
            CombatAiContext context = CreateContext(owner, enemy);

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.Skill, Is.Null);
            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.Search));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 狡猾は敵魔石目的を選ぶ()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(3f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Cunning);
            CombatAiContext context = CreateContext(owner, enemy, null, new Vector3(20f, 0f, 0f));

            CombatAiPlan plan = CombatAiPlanner.BuildPlan(context, profile);

            Assert.That(plan.Objective, Is.EqualTo(CombatObjective.DestroyEnemyStone));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void 目立ちたがり屋は密集の重心へ寄る()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject allyAObject = new GameObject("AllyA");
        GameObject allyBObject = new GameObject("AllyB");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character allyA = CreateCharacter(allyAObject, CombatTeam.Ally, new Vector3(10f, 0f, 0f));
            Character allyB = CreateCharacter(allyBObject, CombatTeam.Ally, new Vector3(11f, 0f, 0f));
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(12f, 0f, 0f));
            profile = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.AttentionSeeker);
            CombatAiContext context = new CombatAiContext(
                owner,
                new[] { CreateIntel(enemy) },
                new[] { CreateIntel(allyA), CreateIntel(allyB) },
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
            // 密集塊 (10, 11, 12) の重心付近へ向かう。
            Assert.That(plan.MoveTarget.Destination.x, Is.EqualTo(11f).Within(1.5f));
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(allyBObject);
            UnityEngine.Object.DestroyImmediate(allyAObject);
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
