using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiTeamReservationsTests
{
    [Test]
    public void Reserve_ExposesPlanAndPredictedDamageToLaterAlly()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character owner = ownerObject.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character target = targetObject.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(30);
            var skill = new FixedEffectSkill(damage: 12, healing: 0);
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.ForCharacter(target),
                skill,
                SkillExecutionContext.ForTarget(target));
            var reservations = new CombatAiTeamReservations();

            reservations.Reserve(owner, plan);
            var allyDamage = new List<CombatAiPendingDamage>();
            var enemyDamage = new List<CombatAiPendingDamage>();
            reservations.AppendPendingDamage(owner.Team, allyDamage, enemyDamage);

            Assert.That(reservations.TryGetPlan(owner, out CombatAiPlan reservedPlan), Is.True);
            Assert.That(reservedPlan.Skill, Is.SameAs(skill));
            Assert.That(allyDamage.Count, Is.EqualTo(1));
            Assert.That(allyDamage[0].Target, Is.SameAs(target));
            Assert.That(allyDamage[0].Damage, Is.EqualTo(12));
            Assert.That(enemyDamage, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void Reserve_ExposesPredictedHealingToLaterAlly()
    {
        GameObject ownerObject = new GameObject("Owner");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character owner = ownerObject.AddComponent<Character>();
            owner.Health.Initialize(30);
            Character target = targetObject.AddComponent<Character>();
            target.Health.Initialize(30, 10);
            var reservations = new CombatAiTeamReservations();
            reservations.Reserve(
                owner,
                new CombatAiPlan(
                    CombatObjective.SupportAlly,
                    CombatMoveTarget.ForCharacter(target),
                    new FixedEffectSkill(damage: 0, healing: 15),
                    SkillExecutionContext.ForTarget(target)));
            var allyHealing = new List<CombatAiPendingHealing>();
            var enemyHealing = new List<CombatAiPendingHealing>();

            reservations.AppendPendingHealing(owner.Team, allyHealing, enemyHealing);

            Assert.That(allyHealing.Count, Is.EqualTo(1));
            Assert.That(allyHealing[0].Target, Is.SameAs(target));
            Assert.That(allyHealing[0].Healing, Is.EqualTo(15));
            Assert.That(enemyHealing, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ownerObject);
        }
    }

    private sealed class FixedEffectSkill : SkillBase
    {
        private readonly int _damage;
        private readonly int _healing;

        public override string Name => "予約テスト";

        public FixedEffectSkill(int damage, int healing)
        {
            _damage = damage;
            _healing = healing;
        }

        public override int EstimateDamage(
            Character self,
            SkillExecutionContext context,
            Character target)
        {
            return _damage;
        }

        public override int EstimateHealing(
            Character self,
            SkillExecutionContext context,
            Character target)
        {
            return _healing;
        }

        public override void Execute(Character self, SkillExecutionContext context)
        {
        }
    }
}
