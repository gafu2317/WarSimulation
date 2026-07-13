using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatBattleFlowTests
{
    [Test]
    public void EndBattle_NotifiesTheFirstValidOutcomeOnly()
    {
        GameObject flowObject = new GameObject("BattleFlow");

        try
        {
            CombatBattleFlow flow = flowObject.AddComponent<CombatBattleFlow>();
            SetState(flow, CombatBattleState.Running);
            int notificationCount = 0;
            CombatBattleState notifiedOutcome = CombatBattleState.WaitingToStart;
            flow.BattleEnded += outcome =>
            {
                notificationCount++;
                notifiedOutcome = outcome;
            };

            flow.EndBattle(CombatBattleState.WaitingToStart);
            flow.EndBattle(CombatBattleState.Victory);
            flow.EndBattle(CombatBattleState.Defeat);

            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(notifiedOutcome, Is.EqualTo(CombatBattleState.Victory));
            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Victory));
        }
        finally
        {
            Object.DestroyImmediate(flowObject);
        }
    }

    private static void SetState(CombatBattleFlow flow, CombatBattleState state)
    {
        FieldInfo field = typeof(CombatBattleFlow).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(flow, state);
    }
}
