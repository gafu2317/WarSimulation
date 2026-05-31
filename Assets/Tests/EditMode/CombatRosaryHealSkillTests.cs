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
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));

            Assert.That(ally.Health.HP, Is.EqualTo(15));
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
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));

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
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));

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
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));

            Assert.That(ally.Health.HP, Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryDistantHealSkill_HealsBoundAlly()
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
            ally.StatusEffects.ApplyBind(3f);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 3f;

            var skill = new RosaryDistantHealSkill();
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));

            Assert.That(ally.Health.CanAct, Is.False);
            Assert.That(ally.Health.HP, Is.EqualTo(18));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryDistantHealSkill_HealsMoreAtCloserRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject nearAllyGo = new GameObject("NearAlly");
        GameObject farAllyGo = new GameObject("FarAlly");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character nearAlly = nearAllyGo.AddComponent<Character>();
            Character farAlly = farAllyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            nearAlly.SetTeam(CombatTeam.Ally);
            farAlly.SetTeam(CombatTeam.Ally);
            nearAlly.Health.Initialize(maxHP: 30, currentHP: 10);
            farAlly.Health.Initialize(maxHP: 30, currentHP: 10);
            typeof(Character).GetProperty("FAI").SetValue(owner, 10);

            nearAllyGo.transform.position = ownerGo.transform.position + Vector3.forward * 1f;
            farAllyGo.transform.position = ownerGo.transform.position + Vector3.forward * 8f;

            var skill = new RosaryDistantHealSkill();
            skill.Execute(owner, SkillExecutionContext.ForTarget(nearAlly));
            skill.Execute(owner, SkillExecutionContext.ForTarget(farAlly));

            Assert.That(nearAlly.Health.HP, Is.GreaterThan(farAlly.Health.HP));
        }
        finally
        {
            Object.DestroyImmediate(farAllyGo);
            Object.DestroyImmediate(nearAllyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }
}
