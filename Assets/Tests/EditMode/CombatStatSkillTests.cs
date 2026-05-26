using NUnit.Framework;
using UnityEngine;

public sealed class CombatStatSkillTests
{
    [Test]
    public void StatBuffSkill_AppliesBuffToAlly()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            target.Health.Initialize(30);

            var skill = new StatBuffSkill(CombatStatusEffects.StatKind.INT, 1.25f, 5f, 5f);
            skill.Execute(owner, target);

            Assert.That(target.StatusEffects.GetMultiplier(CombatStatusEffects.StatKind.INT), Is.EqualTo(1.25f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void StatDebuffSkill_AppliesDebuffToEnemyInRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            var skill = new StatDebuffSkill(CombatStatusEffects.StatKind.AGI, 0.7f, 5f, maxRange: 7f, cooldownSeconds: 5f);
            skill.Execute(owner, target);

            Assert.That(target.StatusEffects.GetMultiplier(CombatStatusEffects.StatKind.AGI), Is.EqualTo(0.7f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void CombatSkillFactory_CreatesAllStatBuffAndDebuffSkills()
    {
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_StrBuff), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_FaiBuff), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_StrDebuff), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_IntBuff), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_AgiBuff), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.StatDebuff_INT), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.StatDebuff_FAI), Is.TypeOf<IdentifiedSkill>());
        Assert.That(CombatSkillFactory.Create(SkillId.StatDebuff_AGI), Is.TypeOf<IdentifiedSkill>());
    }
}
