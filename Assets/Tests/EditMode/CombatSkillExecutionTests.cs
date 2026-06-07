using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatSkillExecutionTests
{
    [Test]
    public void Tick_ExecutesReadySkillAndStartsCooldown()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30, currentHP: 10);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            SkillBase skill = new IdentifiedSkill(new SwordSlashSkill(), SkillId.Sword_Slash);
            CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
                owner,
                skill,
                SkillExecutionContext.ForTarget(target));
            Assert.That(evaluation.CanUse, Is.True);

            skill.Execute(owner, evaluation.Context);
            owner.SkillCooldowns.StartCooldown(skill);

            Assert.That(target.Health.HP, Is.LessThan(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_ExecutesReadySkillAndStartsCooldown()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Ally);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            var skill = new AiBrainTestAttackSkill();
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.None,
                skill,
                SkillExecutionContext.ForTarget(target));

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.True);
            Assert.That(target.Health.HP, Is.EqualTo(23));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
            Assert.That(brain.HasLastSkillEvaluation, Is.True);
            Assert.That(brain.LastSkillEvaluation.CanUse, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_DoesNotExecuteInvalidSkill()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Ally);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 10f;

            var skill = new AiBrainTestAttackSkill();
            var plan = new CombatAiPlan(
                CombatObjective.AttackEnemy,
                CombatMoveTarget.None,
                skill,
                SkillExecutionContext.ForTarget(target));

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.False);
            Assert.That(target.Health.HP, Is.EqualTo(30));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.True);
            Assert.That(brain.HasLastSkillEvaluation, Is.True);
            Assert.That(brain.LastSkillEvaluation.CanUse, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_ExecutePlan_UpdatesWorldObjectiveLabel()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            var plan = new CombatAiPlan(
                CombatObjective.SupportAlly,
                CombatMoveTarget.None,
                null,
                SkillExecutionContext.None);

            brain.ExecutePlan(plan);

            CombatAiWorldLabel label = ownerGo.GetComponent<CombatAiWorldLabel>();
            Assert.That(label, Is.Not.Null);
            Assert.That(brain.LastPlan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(label.CurrentText, Is.EqualTo("味方を援護"));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_DestroyEnemyStonePlan_DamagesEnemyMainStoneInRange()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject stoneGo = new GameObject("EnemyMainStone");
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            var map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, Vector3.zero));
            map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, Vector3.forward));
            system.Initialize(map);

            MagicStone stone = stoneGo.AddComponent<MagicStone>();
            stone.Setup(featureIndex: 1, FeatureType.EnemyMainStone, isMainStone: true, stoneHeight: 3f);
            stoneGo.transform.position = Vector3.forward;

            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);
            owner.EquipWeapon(new Sword(range: 2f, basePower: 12f));

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            var plan = new CombatAiPlan(
                CombatObjective.DestroyEnemyStone,
                CombatMoveTarget.ForPosition(stoneGo.transform.position),
                null,
                SkillExecutionContext.None);

            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.True);
            Assert.That(brain.LastStoneDamage, Is.GreaterThan(0));
            Assert.That(system.TryGetHP(1, out int hp), Is.True);
            Assert.That(hp, Is.LessThan(500));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(stoneGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    private sealed class AiBrainTestAttackSkill : SkillBase
    {
        public override string Name => "AiBrainTestSlash";
        public override float CooldownSeconds => 10f;
        public override float MaxRange => 2f;

        public override void Execute(Character self, SkillExecutionContext context)
        {
            context.PrimaryTarget?.Health?.TakeDamage(7, self);
        }
    }
}
