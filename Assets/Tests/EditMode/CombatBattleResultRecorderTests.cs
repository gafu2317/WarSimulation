using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatBattleResultRecorderTests
{
    [Test]
    public void CombatBattleResultRecorder_ReportsAppliedDamageHealingPreventionAndDefeat()
    {
        GameObject recorderObject = new GameObject("ResultRecorder");
        GameObject stoneSystemObject = new GameObject("MagicStoneSystem");
        GameObject allyObject = new GameObject("Ally");
        GameObject enemyObject = new GameObject("Enemy");
        CombatAiPersonalityProfile personality = null;
        try
        {
            CombatBattleResultRecorder recorder = recorderObject.AddComponent<CombatBattleResultRecorder>();
            CombatMagicStoneSystem stoneSystem = stoneSystemObject.AddComponent<CombatMagicStoneSystem>();
            stoneSystem.Initialize(CreateMapWithMainStones());
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, 100, 95);
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, 25);
            personality = CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Devoted);
            ally.ConfigureForBattle(null, personality);
            ally.EquipWeapon(new Sword());

            recorder.Begin(new[] { ally }, new[] { enemy });
            CombatDamageEvents.RaiseDamageApplied(enemy, 10, ally);
            CombatDamageEvents.RaiseDamagePrevented(ally, 7, enemy);
            ally.Health.Heal(20, ally);
            stoneSystem.TakeDamage(1, 12, ally);
            enemy.Health.TakeDamage(25, ally);

            CombatBattleResult result = recorder.Complete(CombatBattleState.Victory);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Allies.AliveCount, Is.EqualTo(1));
            Assert.That(result.Enemies.AliveCount, Is.EqualTo(0));
            Assert.That(result.Allies.DamageDealt, Is.EqualTo(35));
            Assert.That(result.Enemies.DamageTaken, Is.EqualTo(35));
            Assert.That(result.Allies.DamagePrevented, Is.EqualTo(7));
            Assert.That(result.Allies.MagicStoneDamage, Is.EqualTo(12));
            Assert.That(result.Allies.Characters[0].MagicStoneDamage, Is.EqualTo(12));
            Assert.That(result.Allies.Characters[0].PersonalityDisplayName, Is.EqualTo("献身的"));
            Assert.That(result.Allies.Characters[0].WeaponDisplayName, Is.EqualTo("剣"));
            Assert.That(result.Allies.HealingDone, Is.EqualTo(5));
            Assert.That(result.Allies.Characters[0].Defeats, Is.EqualTo(1));
            Assert.That(result.Enemies.Characters[0].Deaths, Is.EqualTo(1));
            Assert.That(result.Enemies.Characters[0].IsAlive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(recorderObject);
            Object.DestroyImmediate(stoneSystemObject);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(personality);
        }
    }

    [Test]
    public void CombatBattleResultRecorder_ReportsDeathWithoutKiller()
    {
        GameObject recorderObject = new GameObject("ResultRecorder");
        GameObject characterObject = new GameObject("Character");
        try
        {
            CombatBattleResultRecorder recorder = recorderObject.AddComponent<CombatBattleResultRecorder>();
            Character character = CreateCharacter(characterObject, CombatTeam.Ally, 30);

            recorder.Begin(new[] { character }, System.Array.Empty<Character>());
            character.Health.TakeDamage(30);

            CombatBattleResult result = recorder.Complete(CombatBattleState.Defeat);

            Assert.That(result.Allies.Characters[0].Deaths, Is.EqualTo(1));
            Assert.That(result.Allies.Characters[0].Defeats, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(recorderObject);
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void CombatBattleResultRecorder_ClearDiscardsCurrentBattle()
    {
        GameObject recorderObject = new GameObject("ResultRecorder");
        GameObject allyObject = new GameObject("Ally");
        try
        {
            CombatBattleResultRecorder recorder = recorderObject.AddComponent<CombatBattleResultRecorder>();
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, 30);

            recorder.Begin(new[] { ally }, System.Array.Empty<Character>());
            CombatDamageEvents.RaiseDamageApplied(ally, 4, null);
            recorder.Clear();

            Assert.That(recorder.CurrentResult, Is.Null);

            recorder.Begin(new[] { ally }, System.Array.Empty<Character>());
            CombatBattleResult result = recorder.Complete(CombatBattleState.Defeat);

            Assert.That(result.Allies.DamageTaken, Is.EqualTo(0));
            Assert.That(result.Allies.Characters[0].DamageDealt, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(recorderObject);
            Object.DestroyImmediate(allyObject);
        }
    }

    private static Character CreateCharacter(
        GameObject target,
        CombatTeam team,
        int maxHp,
        int currentHp = -1)
    {
        Character character = target.AddComponent<Character>();
        character.SetTeam(team);
        character.Health.Initialize(maxHp, currentHp);
        return character;
    }

    private static MapData CreateMapWithMainStones()
    {
        var map = new MapData(
            new HeightMap(4, 4, 1f),
            new GroundStateGrid(4, 4, 1f),
            seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(3f, 0f, 3f)));
        return map;
    }
}
