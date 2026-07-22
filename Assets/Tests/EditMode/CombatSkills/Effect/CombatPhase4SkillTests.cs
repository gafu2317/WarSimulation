using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class CombatPhase4SkillTests
{
    [Test]
    public void BibleInvulnerableSkill_AppliesInvulnerable()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);

            new BibleInvulnerableSkill().Execute(owner, SkillExecutionContext.ForSelf(owner));

            Assert.That(owner.StatusEffects.IsInvulnerable, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void BibleGotsumeSkill_ReflectsDamageFromEnemyAttack()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            ally.SetTeam(CombatTeam.Ally);
            enemy.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(30);
            ally.Health.Initialize(30);
            enemy.Health.Initialize(30);
            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            new BibleGotsumeSkill().Execute(owner, SkillExecutionContext.ForTarget(ally));
            ally.Health.TakeDamage(5, enemy);

            Assert.That(ally.Health.HP, Is.EqualTo(25));
            Assert.That(enemy.Health.HP, Is.EqualTo(22));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void BibleGotsumeSkill_RecastDoesNotDuplicateReflectSubscription()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            ally.SetTeam(CombatTeam.Ally);
            enemy.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(30);
            ally.Health.Initialize(30);
            enemy.Health.Initialize(30);
            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            var skill = new BibleGotsumeSkill();
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));
            skill.Execute(owner, SkillExecutionContext.ForTarget(ally));
            ally.Health.TakeDamage(5, enemy);

            Assert.That(enemy.Health.HP, Is.EqualTo(22));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void BibleCarryRushSkill_BoostsSpeedAndCarriesAlly()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            ally.SetTeam(CombatTeam.Ally);
            owner.Health.Initialize(30);
            ally.Health.Initialize(30);
            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            CombatCharacterBody ownerBody = owner.GetComponent<CombatCharacterBody>();
            CombatCharacterBody allyBody = ally.GetComponent<CombatCharacterBody>();
            NavMeshAgent allyAgent = ally.GetComponent<NavMeshAgent>();
            ownerBody.BaseSpeed = 3f;
            allyBody.BaseSpeed = 2f;
            Vector3 initialAllyPosition = ally.transform.position;

            new BibleCarryRushSkill().Execute(owner, SkillExecutionContext.ForTarget(ally));

            BibleCarryRushEffect effect = owner.GetComponent<BibleCarryRushEffect>();
            Assert.That(effect, Is.Not.Null);
            Assert.That(ownerBody.BaseSpeed, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(allyBody.BaseSpeed, Is.EqualTo(3.6f).Within(0.001f));
            Assert.That(ally.transform.position, Is.EqualTo(initialAllyPosition));
            Assert.That(ally.transform.parent, Is.EqualTo(owner.transform));
            Assert.That(allyAgent.enabled, Is.False);

            owner.transform.position += Vector3.right * 5f;
            Assert.That(ally.transform.position, Is.EqualTo(initialAllyPosition + Vector3.right * 5f));

            ForceCarryRushExpired(effect);
            InvokePrivateUpdate(effect);

            Assert.That(ownerBody.BaseSpeed, Is.EqualTo(3f).Within(0.001f));
            Assert.That(allyBody.BaseSpeed, Is.EqualTo(2f).Within(0.001f));
            Assert.That(ally.transform.parent, Is.Null);
            Assert.That(allyAgent.enabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void ShieldShoulderGuardSkill_RedirectsDamageFromProtectedAlly()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject guardianGo = new GameObject("Guardian");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character guardian = guardianGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            guardian.SetTeam(CombatTeam.Ally);
            ally.SetTeam(CombatTeam.Ally);
            enemy.SetTeam(CombatTeam.Enemy);
            guardian.Health.Initialize(30);
            ally.Health.Initialize(30);
            enemy.Health.Initialize(30);
            allyGo.transform.position = guardianGo.transform.position + Vector3.forward * 2f;
            enemyGo.transform.position = guardianGo.transform.position + Vector3.forward * 4f;

            system.AllyCharacters.Add(guardian);
            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            guardian.Vision.UpdateVision();
            new ShieldShoulderGuardSkill().Execute(guardian, SkillExecutionContext.ForTarget(ally));
            CombatEffectSource redirectedSource = CombatEffectSource.None;
            void CaptureRedirectedDamage(CombatDamageEvent damage)
            {
                if (!damage.WasPrevented && damage.Target == guardian) redirectedSource = damage.AttackSource;
            }

            CombatDamageEvents.Resolved += CaptureRedirectedDamage;
            try
            {
                ally.Health.TakeDamage(
                    10,
                    new CombatEffectSource(enemy, SkillId.Grimoire_Poison, "毒"));
            }
            finally
            {
                CombatDamageEvents.Resolved -= CaptureRedirectedDamage;
            }

            Assert.That(ally.Health.HP, Is.EqualTo(30));
            Assert.That(guardian.Health.HP, Is.EqualTo(24));
            Assert.That(redirectedSource.Character, Is.SameAs(enemy));
            Assert.That(redirectedSource.SkillId, Is.EqualTo(SkillId.Grimoire_Poison));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(guardianGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void GrimoireBindSkill_AppliesBindAndPreventsActing()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            new GrimoireBindSkill().Execute(owner, SkillExecutionContext.ForTarget(target));

            Assert.That(target.StatusEffects.IsBound, Is.True);
            Assert.That(target.Health.CanAct, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void GrimoirePoisonSkill_AppliesPoison()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject targetGo = new GameObject("Target");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            target.SetTeam(CombatTeam.Enemy);
            target.Health.Initialize(30);
            targetGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            new GrimoirePoisonSkill().Execute(owner, SkillExecutionContext.ForTarget(target));
            ForceAllPeriodicEffectsReadyNow(target.StatusEffects);
            target.StatusEffects.GetActiveEffectSnapshots();

            Assert.That(target.Health.HP, Is.EqualTo(26));
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void GrimoireStealthSkill_AppliesStealthToSelf()
    {
        GameObject ownerGo = new GameObject("Owner");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            owner.Health.Initialize(30);

            new GrimoireStealthSkill().Execute(owner, SkillExecutionContext.ForSelf(owner));

            Assert.That(owner.StatusEffects.IsStealthed, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryRegenerationSkill_AppliesHealOverTime()
    {
        GameObject ownerGo = new GameObject("Owner");
        GameObject allyGo = new GameObject("Ally");
        try
        {
            Character owner = ownerGo.AddComponent<Character>();
            Character ally = allyGo.AddComponent<Character>();
            owner.Health.Initialize(30);
            ally.SetTeam(CombatTeam.Ally);
            ally.Health.Initialize(maxHP: 30, currentHP: 10);
            allyGo.transform.position = ownerGo.transform.position + Vector3.forward * 2f;

            new RosaryRegenerationSkill().Execute(owner, SkillExecutionContext.ForTarget(ally));
            ForceAllPeriodicEffectsReadyNow(ally.StatusEffects);
            ally.StatusEffects.GetActiveEffectSnapshots();

            Assert.That(ally.Health.HP, Is.EqualTo(17));
        }
        finally
        {
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    [Test]
    public void RosaryHealingAreaSkill_HealsAlliesAtPlacedPointAfterOwnerMovesAway()
    {
        TargetAreaFixture fixture = TargetAreaFixture.Create();
        try
        {
            fixture.Ally.Health.Initialize(maxHP: 30, currentHP: 10);
            Vector3 placedPoint = fixture.Ally.transform.position;

            new RosaryHealingAreaSkill().Execute(
                fixture.Owner,
                SkillExecutionContext.ForPoint(placedPoint));

            fixture.OwnerGo.transform.position += Vector3.right * 20f;

            RosaryHealingAreaZone zone = Object.FindAnyObjectByType<RosaryHealingAreaZone>();
            Assert.That(zone, Is.Not.Null);

            ForceAreaZoneReadyNow(zone);
            InvokePrivateUpdate(zone);

            Assert.That(fixture.Ally.Health.HP, Is.EqualTo(14));
        }
        finally
        {
            RosaryHealingAreaZone zone = Object.FindAnyObjectByType<RosaryHealingAreaZone>();
            if (zone != null)
            {
                Object.DestroyImmediate(zone.gameObject);
            }

            fixture.Destroy();
        }
    }

    [Test]
    public void WandAreaBlastSkill_DamagesEnemiesInResolvedTargets()
    {
        TargetAreaFixture fixture = TargetAreaFixture.Create();
        try
        {
            typeof(Character).GetProperty("INT").SetValue(fixture.Owner, 10);
            SkillExecutionContext context = CombatSkillTargeting.CreateEnemyAreaContext(
                fixture.Owner,
                fixture.Owner.transform.position,
                radius: 3f);

            new WandAreaBlastSkill().Execute(fixture.Owner, context);

            Assert.That(fixture.NearEnemy.Health.HP, Is.LessThan(30));
            Assert.That(fixture.Ally.Health.HP, Is.EqualTo(30));
            Assert.That(fixture.FarEnemy.Health.HP, Is.EqualTo(30));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void WandAreaBlastSkill_DoesNotBreakStealthWhenResolvedTargetsAreEmpty()
    {
        TargetAreaFixture fixture = TargetAreaFixture.Create();
        try
        {
            fixture.Owner.StatusEffects.ApplyStealth(5f);
            SkillExecutionContext context = SkillExecutionContext.ForPoint(
                fixture.Owner.transform.position,
                new List<Character>());

            new WandAreaBlastSkill().Execute(fixture.Owner, context);

            Assert.That(fixture.Owner.StatusEffects.IsStealthed, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void RosarySacrificeThunderSkill_DamagesRecognizedEnemiesAndCostsSelfHp()
    {
        TargetAreaFixture fixture = TargetAreaFixture.Create();
        try
        {
            typeof(Character).GetProperty("FAI").SetValue(fixture.Owner, 10);
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            fixture.Owner.Vision.UpdateVision();
            fixture.FarEnemy.StatusEffects.ApplyStealth(5f);
            SkillExecutionContext context = CombatSkillTargeting.CreateRecognizedEnemiesContext(fixture.Owner);

            new RosarySacrificeThunderSkill().Execute(fixture.Owner, context);

            Assert.That(fixture.Owner.Health.HP, Is.EqualTo(18));
            Assert.That(fixture.NearEnemy.Health.HP, Is.LessThan(30));
            Assert.That(fixture.FarEnemy.Health.HP, Is.LessThan(30));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void RosarySacrificeThunderSkill_BreaksStealthOnUse()
    {
        TargetAreaFixture fixture = TargetAreaFixture.Create();
        try
        {
            typeof(Character).GetProperty("FAI").SetValue(fixture.Owner, 10);
            fixture.Owner.StatusEffects.ApplyStealth(5f);
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            SkillExecutionContext context = CombatSkillTargeting.CreateRecognizedEnemiesContext(fixture.Owner);

            new RosarySacrificeThunderSkill().Execute(fixture.Owner, context);

            Assert.That(fixture.Owner.StatusEffects.IsStealthed, Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void SwordSlashSkill_RefreshesTargetRecognitionBeforeStealthBonusCheck()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject ownerGo = new GameObject("Owner");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character owner = ownerGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            owner.SetTeam(CombatTeam.Ally);
            enemy.SetTeam(CombatTeam.Enemy);
            owner.Health.Initialize(30);
            enemy.Health.Initialize(30);
            system.AllyCharacters.Add(owner);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();
            CombatEditModeTestUtil.WireVision(owner.Vision, system);
            CombatEditModeTestUtil.WireVision(enemy.Vision, system);

            var ownerCollider = ownerGo.AddComponent<CapsuleCollider>();
            ownerCollider.center = new Vector3(0f, 1f, 0f);
            ownerCollider.height = 2f;
            var enemyCollider = enemyGo.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1f, 0f);
            enemyCollider.height = 2f;

            ownerGo.transform.position = new Vector3(0f, 0f, -5f);
            enemyGo.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            enemy.Vision.Initialize();
            enemy.Vision.UpdateVision();

            ownerGo.transform.position = new Vector3(0f, 0f, 1.5f);
            Physics.SyncTransforms();
            typeof(Character).GetProperty("STR").SetValue(owner, 10);

            var skill = new SwordSlashSkill();
            int expectedDamage = Mathf.Max(1, Mathf.RoundToInt(owner.GetEffectiveStat(CombatStat.STR) * 0.65f));
            skill.Execute(owner, SkillExecutionContext.ForTarget(enemy));

            Assert.That(enemy.Vision.HasRecognitionOf(owner), Is.True);
            Assert.That(enemy.Health.HP, Is.EqualTo(30 - expectedDamage));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatSkillFactory_CreatesNewPhase4Skills()
    {
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_Invulnerable), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_Gotsume), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Bible_CarryRush), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Shield_ShoulderGuard), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_Bind), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_Poison), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Grimoire_Stealth), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Rosary_Regeneration), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Rosary_HealingArea), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Wand_AreaBlast), Is.Not.Null);
        Assert.That(CombatSkillFactory.Create(SkillId.Rosary_SacrificeThunder), Is.Not.Null);
    }

    private static void ForceAllPeriodicEffectsReadyNow(CombatStatusEffects statusEffects)
    {
        FieldInfo effectsField = typeof(CombatStatusEffects).GetField(
            "_effects",
            BindingFlags.NonPublic | BindingFlags.Instance);
        object effectsList = effectsField?.GetValue(statusEffects);
        if (effectsList == null) return;

        System.Collections.IList list = (System.Collections.IList)effectsList;
        for (int i = 0; i < list.Count; i++)
        {
            object effect = list[i];
            FieldInfo nextTickAtField = effect.GetType().GetField(
                "NextTickAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            FieldInfo expiresAtField = effect.GetType().GetField(
                "ExpiresAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            nextTickAtField?.SetValue(effect, Time.time);
            expiresAtField?.SetValue(effect, Time.time + 0.01f);
            list[i] = effect;
        }
    }

    private static void ForceAreaZoneReadyNow(RosaryHealingAreaZone zone)
    {
        FieldInfo nextTickTimeField = typeof(RosaryHealingAreaZone).GetField(
            "_nextTickTime",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo expiresAtField = typeof(RosaryHealingAreaZone).GetField(
            "_expiresAt",
            BindingFlags.NonPublic | BindingFlags.Instance);

        nextTickTimeField?.SetValue(zone, Time.time);
        expiresAtField?.SetValue(zone, Time.time + 0.01f);
    }

    private static void InvokePrivateUpdate(MonoBehaviour behaviour)
    {
        MethodInfo updateMethod = behaviour.GetType().GetMethod(
            "Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        updateMethod?.Invoke(behaviour, null);
    }

    private static void ForceCarryRushExpired(BibleCarryRushEffect effect)
    {
        FieldInfo expiresAtField = typeof(BibleCarryRushEffect).GetField(
            "_expiresAt",
            BindingFlags.NonPublic | BindingFlags.Instance);
        expiresAtField?.SetValue(effect, Time.time - 0.01f);
    }

    private sealed class TargetAreaFixture
    {
        public GameObject SystemGo;
        public GameObject OwnerGo;
        public GameObject AllyGo;
        public GameObject NearEnemyGo;
        public GameObject FarEnemyGo;
        public CombatCharacterSystem System;
        public Character Owner;
        public Character Ally;
        public Character NearEnemy;
        public Character FarEnemy;

        public static TargetAreaFixture Create()
        {
            var fixture = new TargetAreaFixture
            {
                SystemGo = new GameObject("CombatCharacterSystem"),
                OwnerGo = new GameObject("Owner"),
                AllyGo = new GameObject("Ally"),
                NearEnemyGo = new GameObject("NearEnemy"),
                FarEnemyGo = new GameObject("FarEnemy"),
            };

            fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();
            fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
            fixture.Owner.SetTeam(CombatTeam.Ally);
            fixture.Owner.Health.Initialize(30);

            fixture.Ally = fixture.AllyGo.AddComponent<Character>();
            fixture.Ally.SetTeam(CombatTeam.Ally);
            fixture.Ally.Health.Initialize(30);
            fixture.AllyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.right * 2f;

            fixture.NearEnemy = fixture.NearEnemyGo.AddComponent<Character>();
            fixture.NearEnemy.SetTeam(CombatTeam.Enemy);
            fixture.NearEnemy.Health.Initialize(30);
            fixture.NearEnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.forward * 2f;

            fixture.FarEnemy = fixture.FarEnemyGo.AddComponent<Character>();
            fixture.FarEnemy.SetTeam(CombatTeam.Enemy);
            fixture.FarEnemy.Health.Initialize(30);
            fixture.FarEnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.forward * 6f;

            fixture.System.AllyCharacters.Add(fixture.Owner);
            fixture.System.AllyCharacters.Add(fixture.Ally);
            fixture.System.EnemyCharacters.Add(fixture.NearEnemy);
            fixture.System.EnemyCharacters.Add(fixture.FarEnemy);
            fixture.System.AssignTeamsFromLists();

            return fixture;
        }

        public void Destroy()
        {
            if (FarEnemyGo != null) Object.DestroyImmediate(FarEnemyGo);
            if (NearEnemyGo != null) Object.DestroyImmediate(NearEnemyGo);
            if (AllyGo != null) Object.DestroyImmediate(AllyGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
            if (SystemGo != null) Object.DestroyImmediate(SystemGo);
        }
    }
}
