using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatHealthAttackTests
{
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
        AssertWeapon(new Sword(), 2f, 12, 1f, CombatStat.STR);
        AssertWeapon(new Wand(), 8f, 10, 1.4f, CombatStat.INT);
        AssertWeapon(new Grimoire(), 7f, 14, 2f, CombatStat.INT);
        AssertWeapon(new Bible(), 6f, 10, 1.6f, CombatStat.FAI);
        AssertWeapon(new Rosary(), 5f, 8, 1.2f, CombatStat.FAI);
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

            Assert.That(bibleCharacter.AvailableCombatSkills.Count, Is.EqualTo(1));
            Assert.That(((IdentifiedSkill)bibleCharacter.AvailableCombatSkills[0]).SkillId, Is.EqualTo(SkillId.Bible_Heal));
            Assert.That(rosaryCharacter.AvailableCombatSkills.Count, Is.EqualTo(1));
            Assert.That(((IdentifiedSkill)rosaryCharacter.AvailableCombatSkills[0]).SkillId, Is.EqualTo(SkillId.Rosary_FaithBuff));
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
        Assert.That(weapon.BasePower, Is.EqualTo(power));
        Assert.That(weapon.CooldownSeconds, Is.EqualTo(cooldown).Within(0.001f));
        Assert.That(weapon.ScalingStat, Is.EqualTo(stat));
    }
}
