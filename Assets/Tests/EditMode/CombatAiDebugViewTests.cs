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

        Assert.That(text, Does.Contain("移動: 退却 120"));
        Assert.That(text, Does.Contain("行動: 攻撃 100"));
        Assert.That(text, Does.Contain("対象: Enemy_01"));
        Assert.That(text, Does.Not.Contain("HP:"));
        Assert.That(text, Does.Contain("状態: 戦闘中"));
        Assert.That(text, Does.Contain("視認: 2"));
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

        Assert.That(text, Does.Contain("移動: 待機 0"));
        Assert.That(text, Does.Contain("行動: なし 0"));
        Assert.That(text, Does.Contain("対象: -"));
    }

    [Test]
    public void FormatMoveKind_IncludesNewWeaponMoveKindsInJapanese()
    {
        Assert.That(
            CombatAiDebugView.FormatMoveKind(SimpleCombatBrain.MoveKind.MoveToHighGround),
            Is.EqualTo("高所へ"));
        Assert.That(
            CombatAiDebugView.FormatMoveKind(SimpleCombatBrain.MoveKind.HideInForest),
            Is.EqualTo("森に潜む"));
    }
}
