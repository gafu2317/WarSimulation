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
    public IReadOnlyList<CombatAiAssaultRoute> AssaultRoutes { get; }
    public IReadOnlyList<Vector3> HighGroundCandidates { get; }
    public IReadOnlyList<Vector3> ForestCandidates { get; }
    public IReadOnlyList<CombatAiPendingDamage> AllyPendingDamage { get; }
    public IReadOnlyList<CombatAiPendingDamage> EnemyPendingDamage { get; }
    public IReadOnlyList<CombatAiPendingHealing> AllyPendingHealing { get; }
    public IReadOnlyList<CombatAiPendingHealing> EnemyPendingHealing { get; }
    public bool HasBlockedMoveDestination { get; }
    public Vector3 BlockedMoveDestination { get; }
    public Character RecentAttacker { get; }
    public Character MarkedStoneAttacker { get; }

    public CombatAiContext(
        Character owner,
        IReadOnlyList<CombatCharacterIntel> enemyIntel,
        IReadOnlyList<CombatCharacterIntel> allyIntel,
        CombatMapSystem.Weather weather,
        bool hasOwnStonePosition,
        Vector3 ownStonePosition,
        bool hasEnemyStonePosition,
        Vector3 enemyStonePosition,
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
        Vector3 blockedMoveDestination = default,
        IReadOnlyList<CombatAiAssaultRoute> assaultRoutes = null,
        Character recentAttacker = null,
        Character markedStoneAttacker = null)
    {
        Owner = owner;
        EnemyIntel = Snapshot(enemyIntel);
        AllyIntel = Snapshot(allyIntel);
        Weather = weather;
        HasOwnStonePosition = hasOwnStonePosition;
        OwnStonePosition = ownStonePosition;
        HasEnemyStonePosition = hasEnemyStonePosition;
        EnemyStonePosition = enemyStonePosition;
        HasEnemyStoneHealth = hasEnemyStoneHealth;
        EnemyStoneHP = enemyStoneHP;
        EnemyStoneMaxHP = enemyStoneMaxHP;
        AssaultRoutes = Snapshot(assaultRoutes);
        HighGroundCandidates = Snapshot(highGroundCandidates);
        ForestCandidates = Snapshot(forestCandidates);
        AllyPendingDamage = Snapshot(allyPendingDamage);
        EnemyPendingDamage = Snapshot(enemyPendingDamage);
        AllyPendingHealing = Snapshot(allyPendingHealing);
        EnemyPendingHealing = Snapshot(enemyPendingHealing);
        HasBlockedMoveDestination = hasBlockedMoveDestination;
        BlockedMoveDestination = blockedMoveDestination;
        RecentAttacker = recentAttacker;
        MarkedStoneAttacker = markedStoneAttacker;
    }

    public bool IsMoveDestinationBlocked(Vector3 destination)
    {
        if (!HasBlockedMoveDestination) return false;

        destination.y = 0f;
        Vector3 blocked = BlockedMoveDestination;
        blocked.y = 0f;
        return Vector3.Distance(destination, blocked) <= 2f;
    }

    public CombatCharacterIntel FindEnemyIntel(Character character)
    {
        return FindIntel(EnemyIntel, character);
    }

    public CombatCharacterIntel FindAllyIntel(Character character)
    {
        CombatCharacterIntel intel = FindIntel(AllyIntel, character);
        if (intel.Character != null || Owner != character || character == null || character.Health == null) return intel;

        return new CombatCharacterIntel(
            character,
            character.Team,
            character.transform.position,
            hasDirectSight: true,
            hasMemory: false,
            hasKnownPosition: true,
            knownPosition: character.transform.position,
            memoryAgeSeconds: 0f,
            recognizesOwner: false,
            hp: character.Health.HP,
            maxHp: character.Health.MaxHP,
            canAct: character.Health.CanAct,
            weaponKind: character.EquippedWeapon != null ? character.EquippedWeapon.Kind : WeaponKind.Unarmed,
            weaponRange: character.EquippedWeapon != null ? character.EquippedWeapon.Range : WeaponBase.Unarmed.Range,
            statusEffects: character.StatusEffects != null
                ? character.StatusEffects.GetActiveEffectSnapshots()
                : Array.Empty<CombatStatusEffectSnapshot>(),
            hasObjective: false,
            objective: default);
    }

    public int GetAllyPendingDamage(Character target)
    {
        return SumDamage(AllyPendingDamage, target);
    }

    public int GetEnemyPendingDamage(Character target)
    {
        return SumDamage(EnemyPendingDamage, target);
    }

    public int GetAllyPendingHealing(Character target)
    {
        int healing = 0;
        for (int i = 0; i < AllyPendingHealing.Count; i++)
        {
            if (AllyPendingHealing[i].Target == target) healing += AllyPendingHealing[i].Healing;
        }

        return healing;
    }

    private static CombatCharacterIntel FindIntel(
        IReadOnlyList<CombatCharacterIntel> characters,
        Character target)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].Character == target) return characters[i];
        }

        return default;
    }

    private static int SumDamage(IReadOnlyList<CombatAiPendingDamage> pendingDamage, Character target)
    {
        int damage = 0;
        for (int i = 0; i < pendingDamage.Count; i++)
        {
            if (pendingDamage[i].Target == target) damage += pendingDamage[i].Damage;
        }

        return damage;
    }

    private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<T>();

        var snapshot = new T[source.Count];
        for (int i = 0; i < source.Count; i++) snapshot[i] = source[i];
        return snapshot;
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

    public static bool IsAssaultWeaponKind(WeaponKind kind)
    {
        return kind == WeaponKind.Sword || kind == WeaponKind.Wand;
    }

    public static bool IsAssaultWeapon(WeaponBase weapon)
    {
        return weapon != null && IsAssaultWeaponKind(weapon.Kind);
    }

    /// <summary>盾の追従・体当たり保護の対象: 双剣／杖、または前進中の味方。</summary>
    public static bool IsFrontlineFollowAlly(CombatAiContext context, CombatCharacterIntel ally)
    {
        return IsAssaultWeaponKind(ally.WeaponKind) || IsAdvancingAlly(context, ally);
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
