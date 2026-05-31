using NUnit.Framework;
using UnityEngine;

public sealed class CombatNormalAttackSkillTests
{
    [TestCase(SkillId.Shield_Slash, 10, 0)]
    [TestCase(SkillId.Grimoire_Bolt, 0, 10)]
    [TestCase(SkillId.Bible_Smite, 0, 0)]
    [TestCase(SkillId.Rosary_Strike, 0, 0)]
    public void Execute_DealsDamageWhenTargetIsInRange(SkillId skillId, int str, int statForIntOrFai)
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);

            typeof(Character).GetProperty("STR").SetValue(owner, str);
            if (skillId == SkillId.Grimoire_Bolt)
            {
                typeof(Character).GetProperty("INT").SetValue(owner, statForIntOrFai);
            }
            else if (skillId == SkillId.Bible_Smite || skillId == SkillId.Rosary_Strike)
            {
                typeof(Character).GetProperty("FAI").SetValue(owner, 10);
            }

            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            SkillBase skill = CombatSkillFactory.Create(skillId);
            skill.Execute(owner, SkillExecutionContext.ForTarget(target));

            Assert.That(target.Health.HP, Is.LessThan(30));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WandArcaneBlast_DealsMoreDamageThanWandBoltAtSameRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 200);
            typeof(Character).GetProperty("INT").SetValue(owner, 10);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 7f;

            var bolt = new WandBoltSkill();
            var blast = new WandArcaneBlastSkill();

            bolt.Execute(owner, SkillExecutionContext.ForTarget(target));
            int hpAfterBolt = target.Health.HP;

            target.Health.Initialize(maxHP: 200);
            blast.Execute(owner, SkillExecutionContext.ForTarget(target));
            int hpAfterBlast = target.Health.HP;

            Assert.That(hpAfterBlast, Is.LessThan(hpAfterBolt));
            Assert.That(blast.MaxRange, Is.GreaterThan(bolt.MaxRange));
            Assert.That(blast.CooldownSeconds, Is.GreaterThan(bolt.CooldownSeconds));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WandBolt_DealsMoreDamageAtLongerRangeWithinMaxRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject nearTargetGo = new GameObject("NearTarget");
        GameObject farTargetGo = new GameObject("FarTarget");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character nearTarget = nearTargetGo.AddComponent<Character>();
            Character farTarget = farTargetGo.AddComponent<Character>();
            nearTarget.SetTeam(CombatTeam.Enemy);
            farTarget.SetTeam(CombatTeam.Enemy);
            nearTarget.Health.Initialize(maxHP: 200);
            farTarget.Health.Initialize(maxHP: 200);
            typeof(Character).GetProperty("INT").SetValue(owner, 10);

            nearTargetGo.transform.position = ownerGo.transform.position + Vector3.forward * 1f;
            farTargetGo.transform.position = ownerGo.transform.position + Vector3.forward * 7f;

            var skill = new WandBoltSkill();
            skill.Execute(owner, SkillExecutionContext.ForTarget(nearTarget));
            int nearHpAfterHit = nearTarget.Health.HP;

            skill.Execute(owner, SkillExecutionContext.ForTarget(farTarget));
            int farHpAfterHit = farTarget.Health.HP;

            Assert.That(farHpAfterHit, Is.LessThan(nearHpAfterHit));
        }
        finally
        {
            Object.DestroyImmediate(farTargetGo);
            Object.DestroyImmediate(nearTargetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WandArcaneBlast_DoesNotHitBeyondMaxRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);
            typeof(Character).GetProperty("INT").SetValue(owner, 10);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 16f;

            new WandArcaneBlastSkill().Execute(owner, SkillExecutionContext.ForTarget(target));

            Assert.That(target.Health.HP, Is.EqualTo(30));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void CombatSkillFactory_CreatesNormalAttackForEveryWeaponKind()
    {
        Assert.That(CombatSkillFactory.Create(SkillId.Sword_Slash), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Shield_Slash), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Shield_ShoulderGuard), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_Bolt), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_ArcaneBlast), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_GodsHand), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_Bolt), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_Smite), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Rosary_Strike), Is.Not.Null);
    }
}
