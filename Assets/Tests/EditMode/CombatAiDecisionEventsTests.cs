using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiDecisionEventsTests
{
    [Test]
    public void PlanSelectionPublishesOwnerAndBothPlans()
    {
        GameObject ownerObject = new GameObject("Owner");
        try
        {
            Character owner = ownerObject.AddComponent<Character>();
            CombatAiPlan previous = CombatAiPlan.None;
            var next = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.None,
                null,
                SkillExecutionContext.None);
            Character publishedOwner = null;
            CombatAiPlan publishedPrevious = default;
            CombatAiPlan publishedNext = default;

            void Capture(Character character, CombatAiPlan before, CombatAiPlan after)
            {
                publishedOwner = character;
                publishedPrevious = before;
                publishedNext = after;
            }

            CombatAiDecisionEvents.PlanSelected += Capture;
            try
            {
                CombatAiDecisionEvents.RaisePlanSelected(owner, previous, next);
            }
            finally
            {
                CombatAiDecisionEvents.PlanSelected -= Capture;
            }

            Assert.That(publishedOwner, Is.SameAs(owner));
            Assert.That(publishedPrevious.Objective, Is.EqualTo(previous.Objective));
            Assert.That(publishedNext.Objective, Is.EqualTo(next.Objective));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
        }
    }
}
