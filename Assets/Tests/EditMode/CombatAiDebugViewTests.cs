using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiDebugViewTests
{
    [Test]
    public void BuildDebugText_IncludesDecisionTargetStateAndVisibleCount()
    {
        var decision = new SimpleCombatBrain.Decision(
            new SimpleCombatBrain.MoveOption(SimpleCombatBrain.MoveKind.RetreatToHome, 120f),
            new SimpleCombatBrain.ActionOption(SimpleCombatBrain.ActionKind.AttackEnemy, 100f));

        string text = CombatAiDebugView.BuildDebugText(
            decision,
            "Enemy_01",
            lifeState: LifeState.Active,
            visibleEnemyCount: 2);

        Assert.That(text, Does.Contain("Move: RetreatToHome 120"));
        Assert.That(text, Does.Contain("Action: AttackEnemy 100"));
        Assert.That(text, Does.Contain("Target: Enemy_01"));
        Assert.That(text, Does.Not.Contain("HP:"));
        Assert.That(text, Does.Contain("State: Active"));
        Assert.That(text, Does.Contain("Visible: 2"));
    }

    [Test]
    public void BuildDebugText_UsesDashWhenTargetNameIsMissing()
    {
        var decision = new SimpleCombatBrain.Decision(
            new SimpleCombatBrain.MoveOption(SimpleCombatBrain.MoveKind.Idle, 0f),
            new SimpleCombatBrain.ActionOption(SimpleCombatBrain.ActionKind.None, 0f));

        string text = CombatAiDebugView.BuildDebugText(
            decision,
            "",
            lifeState: LifeState.Active,
            visibleEnemyCount: 0);

        Assert.That(text, Does.Contain("Move: Idle 0"));
        Assert.That(text, Does.Contain("Action: None 0"));
        Assert.That(text, Does.Contain("Target: -"));
    }
}
