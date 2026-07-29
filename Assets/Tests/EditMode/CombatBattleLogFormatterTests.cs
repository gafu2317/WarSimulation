using System.Collections.Generic;
using NUnit.Framework;

public sealed class CombatBattleLogFormatterTests
{
    [Test]
    public void FormatSkillUsed_BasicAttack_IsTalliedNotLogged()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatSkillUsed(12.4f, "Char_Sword01", "斬撃", "Enemy01");

        Assert.That(line, Is.Null);
        Assert.That(formatter.BuildSkillTallyLine(), Does.Contain("斬撃 x1"));
    }

    [Test]
    public void FormatSkillUsed_NonBasicAttack_ReturnsLine()
    {
        var formatter = new CombatBattleLogFormatter();

        string line = formatter.FormatSkillUsed(18.6f, "Char_Bible01", "STRバフ", "Char_Sword02");

        Assert.That(line, Does.Contain("[t=18.6s]"));
        Assert.That(line, Does.Contain("SKILL Char_Bible01 used STRバフ"));
        Assert.That(line, Does.Contain("target=Char_Sword02"));
        Assert.That(formatter.BuildSkillTallyLine(), Is.Empty);
    }

    [Test]
    public void FormatObjectiveChange_IncludesObjectiveLabelsAndReasons()
    {
        var formatter = new CombatBattleLogFormatter();
        var reasons = new List<string> { "到達可能敵価値が高い", "敵魔石到達" };

        string line = formatter.FormatObjectiveChange(
            12.4f,
            "Char_Wand01",
            "Wand",
            CombatObjective.AttackEnemy,
            CombatObjective.DestroyEnemyStone,
            reasons);

        Assert.That(line, Does.Contain("OBJECTIVE Char_Wand01(Wand)"));
        Assert.That(line, Does.Contain("敵を攻撃 -> 敵魔石を破壊"));
        Assert.That(line, Does.Contain("reason=到達可能敵価値が高い,敵魔石到達"));
    }

    [Test]
    public void FormatBattleEnd_IncludesSkillTally()
    {
        var formatter = new CombatBattleLogFormatter();
        formatter.FormatSkillUsed(1f, "A", "斬撃", null);
        formatter.FormatSkillUsed(2f, "B", "魔弾", null);
        formatter.FormatSkillUsed(3f, "A", "斬撃", null);

        string line = formatter.FormatBattleEnd(
            182.1f,
            "Victory",
            ownStoneHp: 88,
            enemyStoneHp: 0,
            allyAliveCount: 4,
            enemyAliveCount: 0);

        Assert.That(line, Does.Contain("BATTLE_END outcome=Victory"));
        Assert.That(line, Does.Contain("alive=4v0"));
        Assert.That(line, Does.Contain("skillTally:"));
        Assert.That(line, Does.Contain("斬撃 x2"));
        Assert.That(line, Does.Contain("魔弾 x1"));
    }

    [Test]
    public void IsBasicAttackSkillName_RecognizesKnownNames()
    {
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("斬撃"), Is.True);
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("通常攻撃"), Is.True);
        Assert.That(CombatBattleLogFormatter.IsBasicAttackSkillName("STRバフ"), Is.False);
    }
}
