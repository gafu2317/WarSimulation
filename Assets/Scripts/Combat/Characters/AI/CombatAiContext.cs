using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatAiContext
{
    public Character Owner { get; }
    public IReadOnlyList<Character> VisibleEnemies { get; }
    public IReadOnlyList<Character> RememberedEnemies { get; }
    public IReadOnlyList<Character> Allies { get; }
    public IReadOnlyList<CombatCharacterIntel> EnemyIntel { get; }
    public IReadOnlyList<CombatCharacterIntel> AllyIntel { get; }
    public CombatMapSystem.Weather Weather { get; }
    public Vector3 WindVector { get; }
    public bool HasOwnStonePosition { get; }
    public Vector3 OwnStonePosition { get; }
    public bool HasEnemyStonePosition { get; }
    public Vector3 EnemyStonePosition { get; }
    public IReadOnlyList<Vector3> RockPositions { get; }
    public IReadOnlyList<Vector3> BridgePositions { get; }
    public IReadOnlyList<Vector3> HighGroundCandidates { get; }
    public IReadOnlyList<Vector3> ForestCandidates { get; }

    public CombatAiContext(
        Character owner,
        IReadOnlyList<Character> visibleEnemies,
        IReadOnlyList<Character> rememberedEnemies,
        IReadOnlyList<Character> allies,
        IReadOnlyList<CombatCharacterIntel> enemyIntel,
        IReadOnlyList<CombatCharacterIntel> allyIntel,
        CombatMapSystem.Weather weather,
        Vector3 windVector,
        bool hasOwnStonePosition,
        Vector3 ownStonePosition,
        bool hasEnemyStonePosition,
        Vector3 enemyStonePosition,
        IReadOnlyList<Vector3> rockPositions,
        IReadOnlyList<Vector3> bridgePositions,
        IReadOnlyList<Vector3> highGroundCandidates,
        IReadOnlyList<Vector3> forestCandidates)
    {
        Owner = owner;
        VisibleEnemies = visibleEnemies ?? Array.Empty<Character>();
        RememberedEnemies = rememberedEnemies ?? Array.Empty<Character>();
        Allies = allies ?? Array.Empty<Character>();
        EnemyIntel = enemyIntel ?? Array.Empty<CombatCharacterIntel>();
        AllyIntel = allyIntel ?? Array.Empty<CombatCharacterIntel>();
        Weather = weather;
        WindVector = windVector;
        HasOwnStonePosition = hasOwnStonePosition;
        OwnStonePosition = ownStonePosition;
        HasEnemyStonePosition = hasEnemyStonePosition;
        EnemyStonePosition = enemyStonePosition;
        RockPositions = rockPositions ?? Array.Empty<Vector3>();
        BridgePositions = bridgePositions ?? Array.Empty<Vector3>();
        HighGroundCandidates = highGroundCandidates ?? Array.Empty<Vector3>();
        ForestCandidates = forestCandidates ?? Array.Empty<Vector3>();
    }
}

public readonly struct CombatCharacterIntel
{
    public Character Character { get; }
    public CombatTeam Team { get; }
    public Vector3 CurrentPosition { get; }
    public bool HasDirectSight { get; }
    public bool HasMemory { get; }
    public bool HasLastKnownPosition { get; }
    public Vector3 LastKnownPosition { get; }
    public float MemoryAgeSeconds { get; }
    public int HP { get; }
    public int MaxHP { get; }
    public bool CanAct { get; }
    public WeaponKind WeaponKind { get; }
    public float WeaponRange { get; }
    public IReadOnlyList<CombatStatusEffectSnapshot> StatusEffects { get; }
    public bool HasObjective { get; }
    public CombatObjective Objective { get; }

    public CombatCharacterIntel(
        Character character,
        CombatTeam team,
        Vector3 currentPosition,
        bool hasDirectSight,
        bool hasMemory,
        bool hasLastKnownPosition,
        Vector3 lastKnownPosition,
        float memoryAgeSeconds,
        int hp,
        int maxHp,
        bool canAct,
        WeaponKind weaponKind,
        float weaponRange,
        IReadOnlyList<CombatStatusEffectSnapshot> statusEffects,
        bool hasObjective,
        CombatObjective objective)
    {
        Character = character;
        Team = team;
        CurrentPosition = currentPosition;
        HasDirectSight = hasDirectSight;
        HasMemory = hasMemory;
        HasLastKnownPosition = hasLastKnownPosition;
        LastKnownPosition = lastKnownPosition;
        MemoryAgeSeconds = memoryAgeSeconds;
        HP = hp;
        MaxHP = maxHp;
        CanAct = canAct;
        WeaponKind = weaponKind;
        WeaponRange = weaponRange;
        StatusEffects = statusEffects ?? Array.Empty<CombatStatusEffectSnapshot>();
        HasObjective = hasObjective;
        Objective = objective;
    }
}
