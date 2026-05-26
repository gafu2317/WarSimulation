using NUnit.Framework;
using UnityEngine;

public sealed class CombatNormalAttackSkillTests
{
    [TestCase(typeof(ShieldSlashSkill), 10, 0)]
    [TestCase(typeof(GrimoireBoltSkill), 0, 10)]
    [TestCase(typeof(BibleSmiteSkill), 0, 0)]
    [TestCase(typeof(RosaryStrikeSkill), 0, 0)]
    public void Execute_DealsDamageWhenTargetIsInRange(System.Type skillType, int str, int statForIntOrFai)
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
            if (skillType == typeof(GrimoireBoltSkill))
            {
                typeof(Character).GetProperty("INT").SetValue(owner, statForIntOrFai);
            }
            else if (skillType == typeof(BibleSmiteSkill) || skillType == typeof(RosaryStrikeSkill))
            {
                typeof(Character).GetProperty("FAI").SetValue(owner, 10);
            }

            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            var skill = (SkillBase)System.Activator.CreateInstance(skillType);
            skill.Execute(owner, target);

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

            bolt.Execute(owner, target);
            int hpAfterBolt = target.Health.HP;

            target.Health.Initialize(maxHP: 200);
            blast.Execute(owner, target);
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

            new WandArcaneBlastSkill().Execute(owner, target);

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
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_Bolt), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_ArcaneBlast), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_Bolt), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_Smite), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Rosary_Strike), Is.Not.Null);
    }
}
