using NUnit.Framework;
using UnityEngine;

public sealed class CombatRosaryHealSkillTests
{
    [Test]
    public void RosaryDistantHealSkill_HealsAllyWithinRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(maxHP: 30, currentHP: 10);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 8f;

            var skill = new RosaryDistantHealSkill();
            skill.Execute(owner, ally);

            Assert.That(ally.Health.HP, Is.EqualTo(16));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryDistantHealSkill_DoesNotHealAllyOutOfRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(maxHP: 30, currentHP: 10);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 10f;

            var skill = new RosaryDistantHealSkill();
            skill.Execute(owner, ally);

            Assert.That(ally.Health.HP, Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryCloseHealSkill_HealsAllyWithinRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(maxHP: 30, currentHP: 10);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            var skill = new RosaryCloseHealSkill();
            skill.Execute(owner, ally);

            Assert.That(ally.Health.HP, Is.EqualTo(30));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryCloseHealSkill_DoesNotHealAllyOutOfRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            ally.Health.Initialize(maxHP: 30, currentHP: 10);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 4f;

            var skill = new RosaryCloseHealSkill();
            skill.Execute(owner, ally);

            Assert.That(ally.Health.HP, Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }
}
