using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatHealthAttackTests
{
    [Test]
    public void Health_RaisesAppliedDamageWithVictimAndAttacker()
    {
        GameObject victimGo = new GameObject("Victim");
        GameObject attackerGo = new GameObject("Attacker");
        Character reportedVictim = null;
        Character reportedAttacker = null;
        int reportedDamage = 0;
        void Handle(Character victim, int amount, Character attacker)
        {
            reportedVictim = victim;
            reportedAttacker = attacker;
            reportedDamage = amount;
        }

        CombatDamageEvents.DamageApplied += Handle;
        try
        {
            Character victim = victimGo.AddComponent<Character>();
            Character attacker = attackerGo.AddComponent<Character>();
            victim.Health.Initialize(20);

            victim.Health.TakeDamage(7, attacker);

            Assert.That(reportedVictim, Is.SameAs(victim));
            Assert.That(reportedAttacker, Is.SameAs(attacker));
            Assert.That(reportedDamage, Is.EqualTo(7));
        }
        finally
        {
            CombatDamageEvents.DamageApplied -= Handle;
            Object.DestroyImmediate(victimGo);
            Object.DestroyImmediate(attackerGo);
        }
    }

    [Test]
    public void Health_RaisesPreventedDamageWhenInvulnerable()
    {
        GameObject characterGo = new GameObject("Character");
        int preventedDamage = 0;
        void Handle(Character victim, int amount, Character attacker) => preventedDamage += amount;
        CombatDamageEvents.DamagePrevented += Handle;
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(20);
            character.StatusEffects.ApplyInvulnerable(5f);

            character.Health.TakeDamage(7);

            Assert.That(preventedDamage, Is.EqualTo(7));
        }
        finally
        {
            CombatDamageEvents.DamagePrevented -= Handle;
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_ResolvedPreventionIdentifiesTheDefensiveSkill()
    {
        GameObject targetGo = new GameObject("Target");
        GameObject attackerGo = new GameObject("Attacker");
        CombatDamageEvent reported = default;
        void Handle(CombatDamageEvent damage)
        {
            if (damage.WasPrevented) reported = damage;
        }

        CombatDamageEvents.Resolved += Handle;
        try
        {
            Character target = targetGo.AddComponent<Character>();
            Character attacker = attackerGo.AddComponent<Character>();
            target.Health.Initialize(20);
            SkillBase skill = new IdentifiedSkill(
                new BibleInvulnerableSkill(),
                SkillId.Bible_Invulnerable);
            CombatSkillActionInfo action = CombatSkillActionEvents.Start(
                target,
                skill,
                SkillExecutionContext.ForSelf(target));
            CombatSkillActionEvents.Execute(action, () =>
                target.StatusEffects.ApplyInvulnerable(5f, source: target));

            target.Health.TakeDamage(7, attacker);

            Assert.That(reported.Target, Is.SameAs(target));
            Assert.That(reported.Amount, Is.EqualTo(7));
            Assert.That(reported.AttackSource.Character, Is.SameAs(attacker));
            Assert.That(reported.PreventionSource.Character, Is.SameAs(target));
            Assert.That(reported.PreventionSource.SkillId, Is.EqualTo(SkillId.Bible_Invulnerable));
        }
        finally
        {
            CombatDamageEvents.Resolved -= Handle;
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(attackerGo);
        }
    }

    [Test]
    public void Health_RaisesEffectiveHealingAmount()
    {
        GameObject characterGo = new GameObject("Character");
        Character reportedTarget = null;
        int reportedHealing = 0;
        void Handle(Character target, int amount)
        {
            reportedTarget = target;
            reportedHealing = amount;
        }

        CombatHealingEvents.HealingApplied += Handle;
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(20);
            character.Health.TakeDamage(8);

            character.Health.Heal(20);

            Assert.That(reportedTarget, Is.SameAs(character));
            Assert.That(reportedHealing, Is.EqualTo(8));
        }
        finally
        {
            CombatHealingEvents.HealingApplied -= Handle;
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_ResolvedHealingIdentifiesTheHealer()
    {
        GameObject targetGo = new GameObject("Target");
        GameObject healerGo = new GameObject("Healer");
        CombatHealingEvent reported = default;
        void Handle(CombatHealingEvent healing) => reported = healing;

        CombatHealingEvents.Resolved += Handle;
        try
        {
            Character target = targetGo.AddComponent<Character>();
            Character healer = healerGo.AddComponent<Character>();
            target.Health.Initialize(20, 10);

            target.Health.Heal(5, healer);

            Assert.That(reported.Target, Is.SameAs(target));
            Assert.That(reported.Amount, Is.EqualTo(5));
            Assert.That(reported.Source.Character, Is.SameAs(healer));
        }
        finally
        {
            CombatHealingEvents.Resolved -= Handle;
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(healerGo);
        }
    }

    [Test]
    public void Health_EntersRetreatWhenHpReachesZero()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);

            int damage = health.TakeDamage(25);

            Assert.That(damage, Is.EqualTo(20));
            Assert.That(health.HP, Is.EqualTo(0));
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Retreating));
            Assert.That(health.IsTargetable, Is.False);
            Assert.That(health.CanAct, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_RestoreFullReturnsToActive()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);
            health.TakeDamage(20);

            health.RestoreFull();

            Assert.That(health.HP, Is.EqualTo(20));
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Active));
            Assert.That(health.IsTargetable, Is.True);
            Assert.That(health.CanAct, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_HealDoesNotReviveRetreatingCharacter()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);
            health.TakeDamage(20);

            int healed = health.Heal(5);

            Assert.That(healed, Is.EqualTo(0));
            Assert.That(health.HP, Is.EqualTo(0));
            Assert.That(health.IsAlive, Is.False);
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Retreating));
            Assert.That(health.IsTargetable, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_InvulnerablePreventsDamage()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);
            character.StatusEffects.ApplyInvulnerable(5f);

            int damage = health.TakeDamage(7);

            Assert.That(damage, Is.EqualTo(0));
            Assert.That(health.HP, Is.EqualTo(20));
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Active));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_DoesNotMarkRetreatDestinationWhenReturnMoveCannotStart()
    {
        GameObject mapGo = new GameObject("CombatMapSystem");
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatMapSystem mapSystem = mapGo.AddComponent<CombatMapSystem>();
            var map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(3f, 0f, 3f)));
            mapSystem.SetCurrentMap(map);

            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character character = characterGo.AddComponent<Character>();
            system.AllyCharacters.Add(character);
            system.AssignTeamsFromLists();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);

            health.TakeDamage(20);

            Assert.That(health.LifeState, Is.EqualTo(LifeState.Retreating));
            Assert.That(health.HasRetreatDestination, Is.False);
            Assert.That(health.TryCompleteRetreatIfArrived(), Is.False);
            Assert.That(health.HP, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(mapGo);
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_RetriesReturnMoveAfterHomePositionBecomesAvailable()
    {
        GameObject characterGo = new GameObject("Character");
        GameObject systemGo = null;
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);

            health.TakeDamage(20);
            Assert.That(health.HasRetreatDestination, Is.False);

            systemGo = new GameObject("CombatCharacterSystem");
            systemGo.AddComponent<CombatCharacterSystem>();
            var update = typeof(CombatHealth).GetMethod(
                "Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(health, null);

            Assert.That(health.HasRetreatDestination, Is.True);
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Retreating));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void Health_RevivesFifteenSecondsAfterReachingHome()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character character = characterGo.AddComponent<Character>();
            system.AllyCharacters.Add(character);
            system.AssignTeamsFromLists();
            CombatHealth health = character.Health;
            health.Initialize(maxHP: 20);
            var serializedHealth = new UnityEditor.SerializedObject(health);

            Assert.That(
                serializedHealth.FindProperty("_reviveDelayAfterArrival").floatValue,
                Is.EqualTo(15f));

            health.TakeDamage(20);

            Assert.That(health.TryCompleteRetreatIfArrived(), Is.False);
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Retreating));

            CombatEditModeTestUtil.SetPrivateField(health, "_reviveAtTime", Time.time - 0.01f);

            Assert.That(health.TryCompleteRetreatIfArrived(), Is.True);
            Assert.That(health.HP, Is.EqualTo(20));
            Assert.That(health.LifeState, Is.EqualTo(LifeState.Active));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CharacterSystem_ResolvesHomePositionFromTeamMainStoneOrInitialPosition()
    {
        GameObject mapGo = new GameObject("CombatMapSystem");
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            CombatMapSystem mapSystem = mapGo.AddComponent<CombatMapSystem>();
            var map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 2f)));
            map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(3f, 0f, 2f)));
            mapSystem.SetCurrentMap(map);

            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            CombatEditModeTestUtil.WireMapSystem(system, mapSystem);
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            allyGo.transform.position = new Vector3(8f, 0f, 8f);
            enemyGo.transform.position = new Vector3(9f, 0f, 9f);
            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            Assert.That(system.TryGetHomePosition(ally, out Vector3 allyHome), Is.True);
            Assert.That(system.TryGetHomePosition(enemy, out Vector3 enemyHome), Is.True);
            Assert.That(allyHome, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(enemyHome, Is.EqualTo(new Vector3(3f, 0f, 2f)));

            map.Features.Clear();
            Assert.That(system.TryGetHomePosition(ally, out Vector3 fallbackHome), Is.True);
            Assert.That(fallbackHome, Is.EqualTo(allyGo.transform.position));
        }
        finally
        {
            Object.DestroyImmediate(mapGo);
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void Weapons_ExposeExpectedCombatValues()
    {
        AssertWeapon(new Sword(), 2f, 12, 0.9f, CombatStat.STR);
        AssertWeapon(new Wand(), 20f, 10, 1.4f, CombatStat.INT);
        AssertWeapon(new Grimoire(), 30f, 14, 2f, CombatStat.INT);
        AssertWeapon(new Bible(), 30f, 10, 1.6f, CombatStat.FAI);
        AssertWeapon(new Rosary(), 15f, 8, 1.2f, CombatStat.FAI);
        AssertWeapon(new Shield(), 1.8f, 6, 1.3f, CombatStat.STR);
    }

    [Test]
    public void Character_BuildsSupportSkillsFromCatalogWhenEquippedWithBibleOrRosary()
    {
        GameObject bibleGo = new GameObject("BibleCharacter");
        GameObject rosaryGo = new GameObject("RosaryCharacter");
        CombatSkillCatalog catalog = CombatSkillCatalog.CreateDefaultRuntimeCatalog();
        try
        {
            Character bibleCharacter = bibleGo.AddComponent<Character>();
            Character rosaryCharacter = rosaryGo.AddComponent<Character>();
            CombatEditModeTestUtil.WireSkillCatalog(bibleCharacter, catalog);
            CombatEditModeTestUtil.WireSkillCatalog(rosaryCharacter, catalog);

            bibleCharacter.EquipWeapon(new Bible());
            rosaryCharacter.EquipWeapon(new Rosary());

            Assert.That(bibleCharacter.AvailableCombatSkills.Count, Is.EqualTo(8));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_Smite));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_StrBuff));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_IntBuff));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_FaiBuff));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_AgiBuff));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_Invulnerable));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_Gotsume));
            Assert.That(bibleCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Bible_CarryRush));
            Assert.That(rosaryCharacter.AvailableCombatSkills.Count, Is.EqualTo(6));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_Strike));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_DistantHeal));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_CloseHeal));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_Regeneration));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_HealingArea));
            Assert.That(rosaryCharacter.AvailableCombatSkills, Has.Some.Matches<SkillBase>(skill =>
                skill is IdentifiedSkill identified && identified.SkillId == SkillId.Rosary_SacrificeThunder));
        }
        finally
        {
            Object.DestroyImmediate(bibleGo);
            Object.DestroyImmediate(rosaryGo);
            Object.DestroyImmediate(catalog);
        }
    }

    private static void AssertWeapon(
        WeaponBase weapon,
        float range,
        int power,
        float cooldown,
        CombatStat stat)
    {
        Assert.That(weapon.Range, Is.EqualTo(range).Within(0.001f));
        Assert.That(weapon.PrimaryStatBonus, Is.EqualTo(power));
        Assert.That(weapon.CooldownSeconds, Is.EqualTo(cooldown).Within(0.001f));
        Assert.That(weapon.ScalingStat, Is.EqualTo(stat));
    }
}
