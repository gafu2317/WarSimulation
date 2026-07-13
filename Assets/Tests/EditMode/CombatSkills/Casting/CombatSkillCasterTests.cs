using NUnit.Framework;
using UnityEngine;

public sealed class CombatSkillCasterTests
{
    private GameObject _battleFlowGo;

    [SetUp]
    public void SetUp()
    {
        _battleFlowGo = new GameObject("BattleFlow");
        CombatBattleFlow flow = _battleFlowGo.AddComponent<CombatBattleFlow>();
        CombatEditModeTestUtil.SetPrivateField(flow, "_state", CombatBattleState.Running);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_battleFlowGo);
    }

    [Test]
    public void Cast_DelaysEffectAndCooldownUntilCompletion()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            SkillBase skill = new IdentifiedSkill(new WandBoltSkill(), SkillId.Wand_Bolt);

            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);
            Assert.That(owner.SkillCaster.IsCasting, Is.True);
            Assert.That(target.Health.HP, Is.EqualTo(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.True);

            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(owner.SkillCaster.IsCasting, Is.False);
            Assert.That(target.Health.HP, Is.LessThan(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [Test]
    public void Cast_UsesStatsDistanceAndRecognitionCapturedAtStart()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            CombatEditModeTestUtil.SetPrivateField(owner, "<INT>k__BackingField", 10);
            target.transform.position = Vector3.zero;
            SkillBase skill = new WandBoltSkill();
            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);

            owner.StatusEffects.Apply(CombatStatusEffects.StatKind.INT, 2f, 10f);
            target.transform.position = Vector3.forward * 100f;
            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(target.Health.HP, Is.EqualTo(26));
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [Test]
    public void Cast_DamageAndBindDoNotInterrupt()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            SkillBase skill = new WandBoltSkill();
            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);

            owner.Health.TakeDamage(1, target);
            owner.StatusEffects.ApplyBind(10f);
            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(owner.SkillCaster.IsCasting, Is.False);
            Assert.That(target.Health.HP, Is.LessThan(30));
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [Test]
    public void Cast_DeathImmediatelyCancelsTheActiveCast()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            SkillBase skill = new WandBoltSkill();
            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);

            owner.Health.TakeDamage(owner.Health.MaxHP, target);

            Assert.That(owner.Health.LifeState, Is.EqualTo(LifeState.Retreating));
            Assert.That(owner.SkillCaster.IsCasting, Is.False);
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [Test]
    public void Cast_DefeatedTargetIsNotReplacedAndStillStartsCooldown()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            SkillBase skill = new WandBoltSkill();
            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);

            target.Health.TakeDamage(100);
            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(target.Health.HP, Is.Zero);
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [Test]
    public void Cast_AreaTargetsAreFixedAtStart()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character first = CreateCharacter("First", CombatTeam.Enemy, Vector3.forward);
        Character later = CreateCharacter("Later", CombatTeam.Enemy, Vector3.forward * 20f);
        try
        {
            CombatEditModeTestUtil.SetPrivateField(owner, "<INT>k__BackingField", 10);
            SkillBase skill = new WandAreaBlastSkill();
            SkillExecutionContext context = SkillExecutionContext.ForPoint(Vector3.forward, new[] { first });
            Assert.That(owner.SkillCaster.TryStartCast(skill, context), Is.True);

            first.transform.position = Vector3.forward * 20f;
            later.transform.position = Vector3.forward;
            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(first.Health.HP, Is.LessThan(30));
            Assert.That(later.Health.HP, Is.EqualTo(30));
        }
        finally
        {
            DestroyCharacters(owner, first, later);
        }
    }

    [Test]
    public void Cast_OwnerRetreatClearsWithoutCooldown()
    {
        Character owner = CreateCharacter("Owner", CombatTeam.Ally, Vector3.zero);
        Character target = CreateCharacter("Target", CombatTeam.Enemy, Vector3.forward);
        try
        {
            SkillBase skill = new WandBoltSkill();
            Assert.That(owner.SkillCaster.TryStartCast(skill, SkillExecutionContext.ForTarget(target)), Is.True);

            owner.Health.TakeDamage(100);
            owner.SkillCaster.Tick(float.PositiveInfinity);

            Assert.That(owner.SkillCaster.IsCasting, Is.False);
            Assert.That(target.Health.HP, Is.EqualTo(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.True);
        }
        finally
        {
            DestroyCharacters(owner, target);
        }
    }

    [TestCase(SkillId.Wand_Bolt, 0.6f)]
    [TestCase(SkillId.Wand_ArcaneBlast, 1.5f)]
    [TestCase(SkillId.Wand_AreaBlast, 1.5f)]
    [TestCase(SkillId.Wand_GodsHand, 2.5f)]
    [TestCase(SkillId.Grimoire_Bolt, 0.7f)]
    [TestCase(SkillId.Grimoire_StrDebuff, 1f)]
    [TestCase(SkillId.Grimoire_Bind, 1.4f)]
    [TestCase(SkillId.Grimoire_Poison, 1.1f)]
    [TestCase(SkillId.Grimoire_Stealth, 0.8f)]
    [TestCase(SkillId.Bible_Smite, 0.7f)]
    [TestCase(SkillId.Bible_StrBuff, 0.9f)]
    [TestCase(SkillId.Bible_Invulnerable, 1.2f)]
    [TestCase(SkillId.Bible_Gotsume, 1f)]
    [TestCase(SkillId.Bible_CarryRush, 1.2f)]
    [TestCase(SkillId.Rosary_Strike, 0.6f)]
    [TestCase(SkillId.Rosary_DistantHeal, 0.9f)]
    [TestCase(SkillId.Rosary_CloseHeal, 1.3f)]
    [TestCase(SkillId.Rosary_Regeneration, 1f)]
    [TestCase(SkillId.Rosary_HealingArea, 1.5f)]
    [TestCase(SkillId.Rosary_SacrificeThunder, 2.5f)]
    public void FactorySkill_HasConfiguredCastTime(SkillId skillId, float expected)
    {
        Assert.That(CombatSkillFactory.Create(skillId).CastTimeSeconds, Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(SkillId.Sword_Slash)]
    [TestCase(SkillId.Shield_Slash)]
    [TestCase(SkillId.Shield_ShoulderGuard)]
    public void PhysicalSkill_HasNoCastTime(SkillId skillId)
    {
        Assert.That(CombatSkillFactory.Create(skillId).CastTimeSeconds, Is.Zero);
    }

    private static Character CreateCharacter(string name, CombatTeam team, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        Character character = go.AddComponent<Character>();
        character.SetTeam(team);
        character.Health.Initialize(30);
        return character;
    }

    private static void DestroyCharacters(params Character[] characters)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                Object.DestroyImmediate(characters[i].gameObject);
            }
        }
    }
}
