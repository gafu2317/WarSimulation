using NUnit.Framework;
using UnityEngine;

public sealed class CombatStealthCombatTests
{
    [Test]
    public void Attack_FromUnrecognizedAttacker_GainsDamageBonus()
    {
        GameObject attackerGo = new GameObject("Attacker");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character attacker = attackerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            attacker.Health.Initialize(30);
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(50);
            typeof(Character).GetProperty("INT").SetValue(attacker, 10);
            targetGo.transform.position = attackerGo.transform.position + Vector3.forward * 5f;

            int baseDamage = new WandBoltSkill().ExecuteAndMeasure(attacker, target);

            target.Health.Initialize(50);
            target.Vision.ReceiveSharedMemory(attacker, new System.Collections.Generic.List<CharacterMemory>
            {
                new CharacterMemory(attacker, attackerGo.transform.position, Time.time),
            });
            int recognizedDamage = new WandBoltSkill().ExecuteAndMeasure(attacker, target);

            Assert.That(baseDamage, Is.GreaterThan(recognizedDamage));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(attackerGo);
        }
    }

    [Test]
    public void Attack_BreaksStealthOnUse()
    {
        GameObject attackerGo = new GameObject("Attacker");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character attacker = attackerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            attacker.Health.Initialize(30);
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(30);
            typeof(Character).GetProperty("INT").SetValue(attacker, 10);
            targetGo.transform.position = attackerGo.transform.position + Vector3.forward * 5f;

            attacker.StatusEffects.ApplyStealth(5f);
            new WandBoltSkill().Execute(attacker, SkillExecutionContext.ForTarget(target));

            Assert.That(attacker.StatusEffects.IsStealthed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(attackerGo);
        }
    }

    [Test]
    public void GrimoireStealthSkill_IsBrokenByAttack()
    {
        GameObject attackerGo = new GameObject("Attacker");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character attacker = attackerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            attacker.Health.Initialize(30);
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(30);
            typeof(Character).GetProperty("INT").SetValue(attacker, 10);
            targetGo.transform.position = attackerGo.transform.position + Vector3.forward * 5f;

            new GrimoireStealthSkill().Execute(attacker, SkillExecutionContext.ForSelf(attacker));
            Assert.That(attacker.StatusEffects.IsStealthed, Is.True);

            new WandBoltSkill().Execute(attacker, SkillExecutionContext.ForTarget(target));

            Assert.That(attacker.StatusEffects.IsStealthed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(attackerGo);
        }
    }
}

internal static class CombatStealthCombatTestExtensions
{
    public static int ExecuteAndMeasure(this WandBoltSkill skill, Character attacker, Character target)
    {
        int previousHp = target.Health.HP;
        skill.Execute(attacker, SkillExecutionContext.ForTarget(target));
        return previousHp - target.Health.HP;
    }
}
