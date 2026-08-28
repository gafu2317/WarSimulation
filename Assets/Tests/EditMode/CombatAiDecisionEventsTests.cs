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

    [Test]
    public void PlanExecutionPublishesStartedActionsAndFailureReason()
    {
        GameObject ownerObject = new GameObject("Owner");
        try
        {
            Character owner = ownerObject.AddComponent<Character>();
            var plan = new CombatAiPlan(
                CombatObjective.Search,
                CombatMoveTarget.ForPosition(new Vector3(4f, 0f, 7f)),
                null,
                SkillExecutionContext.None,
                CombatAiMoveCode.SearchLastKnown,
                CombatAiReasonCode.EnemyLocationUncertain);
            Character publishedOwner = null;
            CombatAiPlan publishedPlan = default;
            bool publishedMovementStarted = false;
            bool publishedSkillStarted = true;
            string publishedFailureReason = null;

            void Capture(
                Character character,
                CombatAiPlan executedPlan,
                bool movementStarted,
                bool skillStarted,
                string failureReason)
            {
                publishedOwner = character;
                publishedPlan = executedPlan;
                publishedMovementStarted = movementStarted;
                publishedSkillStarted = skillStarted;
                publishedFailureReason = failureReason;
            }

            CombatAiDecisionEvents.PlanExecuted += Capture;
            try
            {
                CombatAiDecisionEvents.RaisePlanExecuted(owner, plan, true, false, "skill-not-ready");
            }
            finally
            {
                CombatAiDecisionEvents.PlanExecuted -= Capture;
            }

            Assert.That(publishedOwner, Is.SameAs(owner));
            Assert.That(publishedPlan.ActionCode, Is.EqualTo(CombatAiMoveCode.SearchLastKnown));
            Assert.That(publishedMovementStarted, Is.True);
            Assert.That(publishedSkillStarted, Is.False);
            Assert.That(publishedFailureReason, Is.EqualTo("skill-not-ready"));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
        }
    }
}
