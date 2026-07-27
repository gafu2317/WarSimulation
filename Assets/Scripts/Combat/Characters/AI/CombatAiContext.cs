using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatAiContext
{
    public Character Owner { get; }
    public IReadOnlyList<CombatCharacterIntel> EnemyIntel { get; }
    public IReadOnlyList<CombatCharacterIntel> AllyIntel { get; }
    public CombatMapSystem.Weather Weather { get; }
    public bool HasOwnStonePosition { get; }
    public Vector3 OwnStonePosition { get; }
    public bool HasEnemyStonePosition { get; }
    public Vector3 EnemyStonePosition { get; }
    public bool HasEnemyStoneHealth { get; }
    public int EnemyStoneHP { get; }
    public int EnemyStoneMaxHP { get; }
    public IReadOnlyList<Vector3> BridgePositions { get; }
    public IReadOnlyList<Vector3> HighGroundCandidates { get; }
    public IReadOnlyList<Vector3> ForestCandidates { get; }
    public IReadOnlyList<CombatAiPendingDamage> AllyPendingDamage { get; }
    public IReadOnlyList<CombatAiPendingDamage> EnemyPendingDamage { get; }
    public IReadOnlyList<CombatAiPendingHealing> AllyPendingHealing { get; }
    public IReadOnlyList<CombatAiPendingHealing> EnemyPendingHealing { get; }
    public bool HasBlockedMoveDestination { get; }
    public Vector3 BlockedMoveDestination { get; }

    public CombatAiContext(
        Character owner,
        IReadOnlyList<CombatCharacterIntel> enemyIntel,
        IReadOnlyList<CombatCharacterIntel> allyIntel,
        CombatMapSystem.Weather weather,
        bool hasOwnStonePosition,
        Vector3 ownStonePosition,
        bool hasEnemyStonePosition,
        Vector3 enemyStonePosition,
        IReadOnlyList<Vector3> bridgePositions,
        IReadOnlyList<Vector3> highGroundCandidates,
        IReadOnlyList<Vector3> forestCandidates,
        bool hasEnemyStoneHealth = false,
        int enemyStoneHP = 0,
        int enemyStoneMaxHP = 0,
        IReadOnlyList<CombatAiPendingDamage> allyPendingDamage = null,
        IReadOnlyList<CombatAiPendingDamage> enemyPendingDamage = null,
        IReadOnlyList<CombatAiPendingHealing> allyPendingHealing = null,
        IReadOnlyList<CombatAiPendingHealing> enemyPendingHealing = null,
        bool hasBlockedMoveDestination = false,
        Vector3 blockedMoveDestination = default)
    {
        Owner = owner;
        EnemyIntel = enemyIntel ?? Array.Empty<CombatCharacterIntel>();
        AllyIntel = allyIntel ?? Array.Empty<CombatCharacterIntel>();
        Weather = weather;
        HasOwnStonePosition = hasOwnStonePosition;
        OwnStonePosition = ownStonePosition;
        HasEnemyStonePosition = hasEnemyStonePosition;
        EnemyStonePosition = enemyStonePosition;
        HasEnemyStoneHealth = hasEnemyStoneHealth;
        EnemyStoneHP = enemyStoneHP;
        EnemyStoneMaxHP = enemyStoneMaxHP;
        BridgePositions = bridgePositions ?? Array.Empty<Vector3>();
        HighGroundCandidates = highGroundCandidates ?? Array.Empty<Vector3>();
        ForestCandidates = forestCandidates ?? Array.Empty<Vector3>();
        AllyPendingDamage = allyPendingDamage ?? Array.Empty<CombatAiPendingDamage>();
        EnemyPendingDamage = enemyPendingDamage ?? Array.Empty<CombatAiPendingDamage>();
        AllyPendingHealing = allyPendingHealing ?? Array.Empty<CombatAiPendingHealing>();
        EnemyPendingHealing = enemyPendingHealing ?? Array.Empty<CombatAiPendingHealing>();
        HasBlockedMoveDestination = hasBlockedMoveDestination;
        BlockedMoveDestination = blockedMoveDestination;
    }

    public bool IsMoveDestinationBlocked(Vector3 destination)
    {
        if (!HasBlockedMoveDestination) return false;

        destination.y = 0f;
        Vector3 blocked = BlockedMoveDestination;
        blocked.y = 0f;
        return Vector3.Distance(destination, blocked) <= 2f;
    }
}

public readonly struct CombatAiPendingHealing
{
    public Character Target { get; }
    public int Healing { get; }

    public CombatAiPendingHealing(Character target, int healing)
    {
        Target = target;
        Healing = Mathf.Max(0, healing);
    }
}

public readonly struct CombatAiPendingDamage
{
    public Character Target { get; }
    public int Damage { get; }

    public CombatAiPendingDamage(Character target, int damage)
    {
        Target = target;
        Damage = Mathf.Max(0, damage);
    }
}

public static class CombatAiPositioning
{
    public static float GetAdvanceProgress(CombatAiContext context, Vector3 position)
    {
        if (context == null || !context.HasOwnStonePosition || !context.HasEnemyStonePosition) return 0f;

        Vector3 battleAxis = context.EnemyStonePosition - context.OwnStonePosition;
        battleAxis.y = 0f;
        float axisLengthSquared = battleAxis.sqrMagnitude;
        if (axisLengthSquared <= 0.01f) return 0f;

        Vector3 offset = position - context.OwnStonePosition;
        offset.y = 0f;
        return Mathf.Clamp01(Vector3.Dot(offset, battleAxis) / axisLengthSquared);
    }

    public static bool IsAdvancingAlly(CombatAiContext context, CombatCharacterIntel ally)
    {
        if (!ally.CanAct || !ally.HasObjective) return false;
        if (ally.Objective == CombatObjective.AttackEnemy || ally.Objective == CombatObjective.DestroyEnemyStone)
        {
            return true;
        }

        return ally.Objective == CombatObjective.Search &&
            ally.HasIntendedDestination &&
            GetAdvanceProgress(context, ally.IntendedDestination) > GetAdvanceProgress(context, ally.CurrentPosition) + 0.01f;
    }
}

public readonly struct CombatCharacterIntel
{
    public Character Character { get; }
    public CombatTeam Team { get; }
    public Vector3 CurrentPosition { get; }
    public bool HasDirectSight { get; }
    public bool HasMemory { get; }
    public bool HasKnownPosition { get; }
    public Vector3 KnownPosition { get; }
    public float MemoryAgeSeconds { get; }
    public bool RecognizesOwner { get; }
    public int HP { get; }
    public int MaxHP { get; }
    public bool IsAlive => HP > 0;
    public bool CanAct { get; }
    public WeaponKind WeaponKind { get; }
    public float WeaponRange { get; }
    public float MoveSpeed { get; }
    public IReadOnlyList<CombatStatusEffectSnapshot> StatusEffects { get; }
    public bool HasObjective { get; }
    public CombatObjective Objective { get; }
    public Character IntendedTarget { get; }
    public bool HasIntendedDestination { get; }
    public Vector3 IntendedDestination { get; }

    public CombatCharacterIntel(
        Character character,
        CombatTeam team,
        Vector3 currentPosition,
        bool hasDirectSight,
        bool hasMemory,
        bool hasKnownPosition,
        Vector3 knownPosition,
        float memoryAgeSeconds,
        bool recognizesOwner,
        int hp,
        int maxHp,
        bool canAct,
        WeaponKind weaponKind,
        float weaponRange,
        IReadOnlyList<CombatStatusEffectSnapshot> statusEffects,
        bool hasObjective,
        CombatObjective objective,
        float moveSpeed = 3.5f,
        Character intendedTarget = null,
        bool hasIntendedDestination = false,
        Vector3 intendedDestination = default)
    {
        Character = character;
        Team = team;
        CurrentPosition = currentPosition;
        HasDirectSight = hasDirectSight;
        HasMemory = hasMemory;
        HasKnownPosition = hasKnownPosition;
        KnownPosition = knownPosition;
        MemoryAgeSeconds = memoryAgeSeconds;
        RecognizesOwner = recognizesOwner;
        HP = hp;
        MaxHP = maxHp;
        CanAct = canAct;
        WeaponKind = weaponKind;
        WeaponRange = weaponRange;
        MoveSpeed = Mathf.Max(0.1f, moveSpeed);
        StatusEffects = statusEffects ?? Array.Empty<CombatStatusEffectSnapshot>();
        HasObjective = hasObjective;
        Objective = objective;
        IntendedTarget = intendedTarget;
        HasIntendedDestination = hasIntendedDestination;
        IntendedDestination = intendedDestination;
    }
}
