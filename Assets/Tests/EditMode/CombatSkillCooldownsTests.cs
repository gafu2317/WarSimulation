using NUnit.Framework;
using UnityEngine;

public sealed class CombatSkillCooldownsTests
{
    [Test]
    public void CombatSkillCooldowns_StartCooldownMarksSkillNotReady()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatSkillCooldowns cooldowns = character.SkillCooldowns;
            var skill = new CooldownTestSkill(cooldownSeconds: 3f);

            Assert.That(cooldowns.IsReady(skill), Is.True);

            cooldowns.StartCooldown(skill);

            Assert.That(cooldowns.IsReady(skill), Is.False);
            Assert.That(cooldowns.GetRemainingSeconds(skill), Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatSkillCooldowns_ZeroCooldownSkillStaysReady()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatSkillCooldowns cooldowns = character.SkillCooldowns;
            var skill = new CooldownTestSkill(cooldownSeconds: 0f);

            cooldowns.StartCooldown(skill);

            Assert.That(cooldowns.IsReady(skill), Is.True);
            Assert.That(cooldowns.GetRemainingSeconds(skill), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatSkillCooldowns_ClearAllMakesSkillReady()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatSkillCooldowns cooldowns = character.SkillCooldowns;
            var skill = new CooldownTestSkill(cooldownSeconds: 3f);

            cooldowns.StartCooldown(skill);
            Assert.That(cooldowns.IsReady(skill), Is.False);

            cooldowns.ClearAll();

            Assert.That(cooldowns.IsReady(skill), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    private sealed class CooldownTestSkill : SkillBase
    {
        private readonly float _cooldownSeconds;

        public CooldownTestSkill(float cooldownSeconds)
        {
            _cooldownSeconds = cooldownSeconds;
        }

        public override string Name => "CooldownTest";

        public override float CooldownSeconds => _cooldownSeconds;

        public override float EvaluateScore(Character self, Character target) => 100f;

        public override void Execute(Character self, Character target)
        {
        }
    }
}
