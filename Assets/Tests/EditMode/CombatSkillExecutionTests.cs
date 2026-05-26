using System.Reflection;
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
            typeof(Character).GetProperty("STR").SetValue(owner, 10);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            SkillBase skill = new IdentifiedSkill(new SwordSlashSkill(), SkillId.Sword_Slash);
            var personality = ownerGo.AddComponent<ExecutionTestPersonality>();
            personality.Configure(skill, target);

            personality.Tick();

            Assert.That(target.Health.HP, Is.LessThan(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    private sealed class ExecutionTestPersonality : PersonalityBase
    {
        private SkillBase _skill;
        private Character _target;

        public void Configure(SkillBase skill, Character target)
        {
            _skill = skill;
            _target = target;
        }

        public override CombatAiPlan DecidePlan()
        {
            return new CombatAiPlan(CombatObjective.Search, CombatMoveTarget.None, _skill, _target);
        }
    }
}
