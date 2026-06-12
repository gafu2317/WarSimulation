using System.Reflection;
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
            CombatAiWorldLabel label = ownerGo.GetComponent<CombatAiWorldLabel>();

            Assert.That(acted, Is.True);
            Assert.That(target.Health.HP, Is.EqualTo(23));
            Assert.That(owner.SkillCooldowns.IsReady(skill), Is.False);
            Assert.That(brain.HasLastSkillEvaluation, Is.True);
            Assert.That(brain.LastSkillEvaluation.CanUse, Is.True);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.CurrentWeaponText, Is.EqualTo("剣"));
            Assert.That(label.CurrentSkillText, Is.EqualTo("AiBrainTestSlash"));
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
    public void AiBrain_ExecutePlan_UpdatesWorldObjectiveLabel()
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

            CombatAiWorldLabel label = ownerGo.GetComponent<CombatAiWorldLabel>();
            Assert.That(label, Is.Not.Null);
            Assert.That(brain.LastPlan.Objective, Is.EqualTo(CombatObjective.SupportAlly));
            Assert.That(label.CurrentText, Is.EqualTo("味方を援護"));
            Assert.That(label.CurrentWeaponText, Is.EqualTo("盾"));
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
            label.RefreshTransientState(Time.time + 0.2f);

            Assert.That(label.CurrentSkillText, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void WorldLabel_HidesWhenBehindMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera");
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraGo.transform.position = Vector3.zero;
            cameraGo.transform.rotation = Quaternion.identity;

            Character owner = ownerGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(maxHP: 30);
            ownerGo.transform.position = new Vector3(0f, 0f, -5f);

            CombatAiWorldLabel label = ownerGo.AddComponent<CombatAiWorldLabel>();
            label.SetObjective(CombatObjective.Search);

            InvokePrivateLateUpdate(label);

            var labelRootField = typeof(CombatAiWorldLabel).GetField("_labelRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Transform labelRoot = (Transform)labelRootField.GetValue(label);

            Assert.That(label.CurrentText, Is.EqualTo("索敵"));
            Assert.That(labelRoot, Is.Not.Null);
            Assert.That(labelRoot.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(cameraGo);
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

    private static void InvokePrivateLateUpdate(CombatAiWorldLabel label)
    {
        MethodInfo lateUpdate = typeof(CombatAiWorldLabel).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(lateUpdate, Is.Not.Null);
        lateUpdate.Invoke(label, null);
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
