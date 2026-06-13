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
            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(owner, target));
            Assert.That(result.CanUse, Is.True);
            skill.Execute(owner, result.Context);

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

            CombatSkillEvaluationResult boltResult = CombatSkillEvaluator.Evaluate(
                bolt,
                CombatSkillEvaluationRequest.ForTarget(owner, target));
            Assert.That(boltResult.CanUse, Is.True);
            bolt.Execute(owner, boltResult.Context);
            int hpAfterBolt = target.Health.HP;

            target.Health.Initialize(maxHP: 200);
            CombatSkillEvaluationResult blastResult = CombatSkillEvaluator.Evaluate(
                blast,
                CombatSkillEvaluationRequest.ForTarget(owner, target));
            Assert.That(blastResult.CanUse, Is.True);
            blast.Execute(owner, blastResult.Context);
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
            CombatSkillEvaluationResult nearResult = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(owner, nearTarget));
            Assert.That(nearResult.CanUse, Is.True);
            skill.Execute(owner, nearResult.Context);
            int nearHpAfterHit = nearTarget.Health.HP;

            CombatSkillEvaluationResult farResult = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(owner, farTarget));
            Assert.That(farResult.CanUse, Is.True);
            skill.Execute(owner, farResult.Context);
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
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 13f;

            var skill = new WandArcaneBlastSkill();
            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(owner, target));

            Assert.That(result.CanUse, Is.False);

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
