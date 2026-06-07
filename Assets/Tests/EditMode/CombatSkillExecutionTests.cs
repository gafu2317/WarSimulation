using NUnit.Framework;
using UnityEngine;

public sealed class CombatSkillExecutionTests
{
    [Test]
    public void Tick_ExecutesReadySkillAndStartsCooldown()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30, currentHP: 10);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            SkillBase skill = new IdentifiedSkill(new SwordSlashSkill(), SkillId.Sword_Slash);
            CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
                owner,
                skill,
                SkillExecutionContext.ForTarget(target));
            Assert.That(evaluation.CanUse, Is.True);

            skill.Execute(owner, evaluation.Context);
            owner.SkillCooldowns.StartCooldown(skill);

            Assert.That(target.Health.HP, Is.LessThan(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_ExecutesReadySkillAndStartsCooldown()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Ally);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            var skill = new AiBrainTestAttackSkill();
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.None,
                skill,
                SkillExecutionContext.ForTarget(target));

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.True);
            Assert.That(target.Health.HP, Is.EqualTo(23));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
            Assert.That(brain.HasLastSkillEvaluation, Is.True);
            Assert.That(brain.LastSkillEvaluation.CanUse, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_DoesNotExecuteInvalidSkill()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Ally);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 10f;

            var skill = new AiBrainTestAttackSkill();
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.None,
                skill,
                SkillExecutionContext.ForTarget(target));

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.False);
            Assert.That(target.Health.HP, Is.EqualTo(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.True);
            Assert.That(brain.HasLastSkillEvaluation, Is.True);
            Assert.That(brain.LastSkillEvaluation.CanUse, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_UpdatesWorldObjectiveLabel()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            var plan = new CombatAiPlan(
                CombatObjective.SupportAlly,
                CombatMoveTarget.None,
                null,
                SkillExecutionContext.None);

            brain.ExecutePlan(plan);

            CombatAiWorldLabel label = ownerGo.GetComponent<CombatAiWorldLabel>();
            Assert.That(label, Is.Not.Null);
            Assert.That(brain.LastPlan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(label.CurrentText, Is.EqualTo("味方を援護"));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    private sealed class AiBrainTestAttackSkill : SkillBase
    {
        public override string Name => "AiBrainTestSlash";
        public override float CooldownSeconds => 10f;
        public override float MaxRange => 2f;

        public override void Execute(Character self, SkillExecutionContext context)
        {
            context.PrimaryTarget?.Health?.TakeDamage(7, self);
        }
    }
}
