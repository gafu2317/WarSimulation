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
            owner.EquipWeapon(new Sword());

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Ally);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            SkillBase skill = new IdentifiedSkill(new AiBrainTestAttackSkill(), SkillId.Sword_Slash);
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

            SkillBase skill = new IdentifiedSkill(new AiBrainTestAttackSkill(), SkillId.Sword_Slash);
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
    public void AiBrain_ExecutePlan_UpdatesLastPlanObjective()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(maxHP: 30);
            owner.EquipWeapon(new Shield());

            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            var plan = new CombatAiPlan(
                CombatObjective.SupportAlly,
                CombatMoveTarget.None,
                null,
                SkillExecutionContext.None);

            brain.ExecutePlan(plan);

            Assert.That(brain.LastPlan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void IdentifiedSkill_Execute_UpdatesExistingWorldLabelSkillText()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);
            owner.EquipWeapon(new Wand());

            Character target = targetGo.AddComponent<Character>();
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(maxHP: 30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward;

            CombatAiWorldLabel label = ownerGo.AddComponent<CombatAiWorldLabel>();
            SkillBase skill = new IdentifiedSkill(new SwordSlashSkill(), SkillId.Sword_Slash);
            CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
                owner,
                skill,
                SkillExecutionContext.ForTarget(target));

            Assert.That(evaluation.CanUse, Is.True);

            skill.Execute(owner, evaluation.Context);

            Assert.That(label.CurrentWeaponText, Is.EqualTo("杖"));
            Assert.That(label.CurrentSkillText, Is.EqualTo("斬撃"));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WorldLabel_ShowSkill_ExpiresAfterDuration()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);

            CombatAiWorldLabel label = ownerGo.AddComponent<CombatAiWorldLabel>();
            label.ShowSkill("斬撃", 0.1f);
            label.RefreshTransientState(Time.unscaledTime + 0.2f);

            Assert.That(label.CurrentSkillText, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void IdentifiedSkill_Execute_WithoutWorldLabel_DoesNotThrow()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);

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
            Assert.DoesNotThrow(() => skill.Execute(owner, evaluation.Context));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void AiBrain_DestroyEnemyStonePlan_UsesSkillTargetingStone()
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
            stoneGo.transform.position = new Vector3(0f, 1.5f, 2f);
            stoneGo.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
            stoneGo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);
            owner.EquipWeapon(new Sword(range: 3f, strBonus: 12));
            ownerGo.AddComponent<CombatVision>();

            SkillBase skill = new IdentifiedSkill(new SwordSlashSkill(), SkillId.Sword_Slash);
            CombatAiBrain brain = ownerGo.AddComponent<CombatAiBrain>();
            var plan = new CombatAiPlan(
                CombatObjective.DestroyEnemyStone,
                CombatMoveTarget.ForPosition(stoneGo.transform.position),
                skill,
                SkillExecutionContext.ForTarget(stone));

            Assert.That(system.TryGetHP(1, out int hpBefore), Is.True);
            bool acted = brain.ExecutePlan(plan);

            Assert.That(acted, Is.True);
            Assert.That(system.TryGetHP(1, out int hpAfter), Is.True);
            Assert.That(hpAfter, Is.LessThan(hpBefore));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(stoneGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WandBolt_StonePositionAlwaysKnown_FacesThenDamages()
    {
        GameObject ownerGo = new GameObject("WandOwner");
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
            stoneGo.transform.position = new Vector3(0f, 1.5f, 8f);
            stoneGo.AddComponent<BoxCollider>();

            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);
            owner.EquipWeapon(new Wand());
            ownerGo.transform.position = Vector3.zero;
            ownerGo.transform.rotation = Quaternion.LookRotation(Vector3.back);
            CombatVision vision = ownerGo.AddComponent<CombatVision>();
            Physics.SyncTransforms();

            // 位置は戦闘開始から既知 → 向きに関係なく候補に載る
            Assert.That(CombatSkillTargeting.IsValidEnemyStone(owner, stone), Is.True);

            SkillBase skill = new IdentifiedSkill(new WandBoltSkill(), SkillId.Wand_Bolt);
            SkillExecutionContext stoneContext = SkillExecutionContext.ForTarget(stone);

            // 計画可否は遮蔽のみ（背を向けていても可）。撃つには向き＋視線が必要。
            CombatSkillEvaluationResult facingAway = CombatSkillEvaluator.Evaluate(owner, skill, stoneContext);
            Assert.That(facingAway.CanUse, Is.True, facingAway.FailureReason);
            vision.UpdateVision();
            Assert.That(vision.HasLineOfSight(stone.transform), Is.False);

            owner.FaceHorizontalToward(stone.transform.position);
            vision.UpdateVision();
            Assert.That(vision.HasLineOfSight(stone.transform), Is.True);

            CombatSkillEvaluationResult facingStone = CombatSkillEvaluator.Evaluate(owner, skill, stoneContext);
            Assert.That(facingStone.CanUse, Is.True, facingStone.FailureReason);

            Assert.That(system.TryGetHP(1, out int hpBefore), Is.True);
            skill.Execute(owner, facingStone.Context);
            Assert.That(system.TryGetHP(1, out int hpAfter), Is.True);
            Assert.That(hpAfter, Is.LessThan(hpBefore));
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
