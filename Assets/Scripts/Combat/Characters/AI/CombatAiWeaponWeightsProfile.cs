using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatAiWeaponWeightsProfile",
    menuName = "WarSimulation/Combat/AI Weapon Weights Profile")]
public sealed class CombatAiWeaponWeightsProfile : ScriptableObject
{
    [SerializeField] private CombatAiWeaponWeightsEntry _sword = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Sword);
    [SerializeField] private CombatAiWeaponWeightsEntry _shield = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Shield);
    [SerializeField] private CombatAiWeaponWeightsEntry _wand = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Wand);
    [SerializeField] private CombatAiWeaponWeightsEntry _grimoire = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Grimoire);
    [SerializeField] private CombatAiWeaponWeightsEntry _bible = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Bible);
    [SerializeField] private CombatAiWeaponWeightsEntry _rosary = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Rosary);
    [SerializeField] private CombatAiWeaponWeightsEntry _unarmed = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Unarmed);

    public float GetObjectiveWeight(WeaponKind kind, CombatObjective objective)
    {
        return GetEntry(kind).GetObjectiveWeight(objective);
    }

    public float GetMoveWeight(WeaponKind kind, string moveCode)
    {
        return GetEntry(kind).GetMoveWeight(moveCode);
    }

    public float GetDamageSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).DamageSkillWeight;
    }

    public float GetProtectSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).ProtectSkillWeight;
    }

    public float GetHealSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).HealSkillWeight;
    }

    public float GetBuffSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).BuffSkillWeight;
    }

    public float GetDebuffSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).DebuffSkillWeight;
    }

    public float GetStealthSkillWeight(WeaponKind kind)
    {
        return GetEntry(kind).StealthSkillWeight;
    }

    public void SetObjectiveWeight(WeaponKind kind, CombatObjective objective, float value)
    {
        GetEntry(kind).SetObjectiveWeight(objective, value);
    }

    [ContextMenu("Apply Current Defaults")]
    public void ApplyCurrentDefaults()
    {
        _sword = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Sword);
        _shield = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Shield);
        _wand = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Wand);
        _grimoire = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Grimoire);
        _bible = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Bible);
        _rosary = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Rosary);
        _unarmed = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Unarmed);
    }

    private void Reset()
    {
        ApplyCurrentDefaults();
    }

    private void OnValidate()
    {
        _sword ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Sword);
        _shield ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Shield);
        _wand ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Wand);
        _grimoire ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Grimoire);
        _bible ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Bible);
        _rosary ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Rosary);
        _unarmed ??= CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Unarmed);
    }

    private CombatAiWeaponWeightsEntry GetEntry(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => _sword,
            WeaponKind.Shield => _shield,
            WeaponKind.Wand => _wand,
            WeaponKind.Grimoire => _grimoire,
            WeaponKind.Bible => _bible,
            WeaponKind.Rosary => _rosary,
            _ => _unarmed,
        };
    }
}

[Serializable]
public sealed class CombatAiWeaponWeightsEntry
{
    [SerializeField] private CombatAiObjectiveWeights _objectives = new();
    [SerializeField] private CombatAiMoveWeights _moves = new();
    [SerializeField] private CombatAiSkillWeights _skills = new();

    public float DamageSkillWeight => _skills.Damage;
    public float ProtectSkillWeight => _skills.Protect;
    public float HealSkillWeight => _skills.Heal;
    public float BuffSkillWeight => _skills.Buff;
    public float DebuffSkillWeight => _skills.Debuff;
    public float StealthSkillWeight => _skills.Stealth;

    public static CombatAiWeaponWeightsEntry CreateDefault(WeaponKind kind)
    {
        var entry = new CombatAiWeaponWeightsEntry();
        entry.ApplyDefaults(kind);
        return entry;
    }

    public float GetObjectiveWeight(CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.AttackEnemy => _objectives.AttackEnemy,
            CombatObjective.DefendOwnStone => _objectives.DefendOwnStone,
            CombatObjective.SupportAlly => _objectives.SupportAlly,
            CombatObjective.DestroyEnemyStone => _objectives.DestroyEnemyStone,
            CombatObjective.Search => _objectives.Search,
            CombatObjective.Retreat => _objectives.Retreat,
            _ => 0f,
        };
    }

    public float GetMoveWeight(string moveCode)
    {
        return moveCode switch
        {
            "AdvanceEnemyStone" => _moves.AdvanceEnemyStone,
            "ReturnOwnStone" => _moves.ReturnOwnStone,
            "PursueEnemy" => _moves.PursueEnemy,
            "SupportAlly" => _moves.SupportAlly,
            "TakeHighGround" => _moves.TakeHighGround,
            "MoveForest" => _moves.MoveForest,
            "SearchLastKnown" => _moves.SearchLastKnown,
            "HoldPosition" => _moves.HoldPosition,
            _ => 0f,
        };
    }

    public void SetObjectiveWeight(CombatObjective objective, float value)
    {
        switch (objective)
        {
            case CombatObjective.AttackEnemy:
                _objectives.AttackEnemy = value;
                break;
            case CombatObjective.DefendOwnStone:
                _objectives.DefendOwnStone = value;
                break;
            case CombatObjective.SupportAlly:
                _objectives.SupportAlly = value;
                break;
            case CombatObjective.DestroyEnemyStone:
                _objectives.DestroyEnemyStone = value;
                break;
            case CombatObjective.Search:
                _objectives.Search = value;
                break;
            case CombatObjective.Retreat:
                _objectives.Retreat = value;
                break;
        }
    }

    public void ApplyDefaults(WeaponKind kind)
    {
        _objectives = new CombatAiObjectiveWeights();
        _moves = new CombatAiMoveWeights();
        _skills = new CombatAiSkillWeights();

        switch (kind)
        {
            case WeaponKind.Sword:
                _objectives.AttackEnemy = 24f;
                _objectives.DestroyEnemyStone = 22f;
                _objectives.DefendOwnStone = 4f;
                _objectives.Search = -4f;
                _objectives.SupportAlly = -14f;

                _moves.PursueEnemy = 22f;
                _moves.AdvanceEnemyStone = 16f;

                _skills.Damage = 18f;
                _skills.Buff = -8f;
                _skills.Heal = -8f;
                _skills.Protect = -8f;
                _skills.Debuff = -8f;
                _skills.Stealth = -8f;
                break;
            case WeaponKind.Shield:
                _objectives.DefendOwnStone = 28f;
                _objectives.SupportAlly = 18f;
                _objectives.AttackEnemy = 8f;
                _objectives.DestroyEnemyStone = 4f;
                _objectives.Search = -6f;

                _moves.ReturnOwnStone = 22f;
                _moves.SupportAlly = 20f;
                _moves.PursueEnemy = 4f;

                _skills.Damage = 4f;
                _skills.Protect = 24f;
                break;
            case WeaponKind.Wand:
                _objectives.AttackEnemy = 24f;
                _objectives.DestroyEnemyStone = 12f;
                _objectives.Search = 10f;
                _objectives.Retreat = 8f;
                _objectives.SupportAlly = -12f;
                _objectives.DefendOwnStone = -6f;

                _moves.TakeHighGround = 20f;
                _moves.MoveForest = 12f;
                _moves.PursueEnemy = -10f;
                _moves.AdvanceEnemyStone = 6f;

                _skills.Damage = 20f;
                _skills.Protect = -10f;
                _skills.Heal = -10f;
                break;
            case WeaponKind.Grimoire:
                _objectives.AttackEnemy = 22f;
                _objectives.DestroyEnemyStone = 16f;
                _objectives.Search = 8f;
                _objectives.Retreat = 8f;
                _objectives.SupportAlly = -10f;
                _objectives.DefendOwnStone = -6f;

                _moves.TakeHighGround = 10f;
                _moves.MoveForest = 12f;
                _moves.PursueEnemy = -4f;
                _moves.AdvanceEnemyStone = 8f;

                _skills.Damage = 8f;
                _skills.Debuff = 24f;
                _skills.Stealth = 12f;
                break;
            case WeaponKind.Bible:
                _objectives.SupportAlly = 28f;
                _objectives.DefendOwnStone = 20f;
                _objectives.AttackEnemy = 2f;
                _objectives.DestroyEnemyStone = -4f;
                _objectives.Retreat = 6f;

                _moves.SupportAlly = 24f;
                _moves.ReturnOwnStone = 12f;
                _moves.MoveForest = 8f;

                _skills.Buff = 24f;
                _skills.Protect = 24f;
                break;
            case WeaponKind.Rosary:
                _objectives.SupportAlly = 32f;
                _objectives.Retreat = 16f;
                _objectives.DefendOwnStone = 12f;
                _objectives.AttackEnemy = -4f;
                _objectives.DestroyEnemyStone = -16f;
                _objectives.Search = 2f;

                _moves.SupportAlly = 26f;
                _moves.ReturnOwnStone = 8f;
                _moves.MoveForest = 10f;
                _moves.PursueEnemy = -12f;

                _skills.Damage = -6f;
                _skills.Protect = 10f;
                _skills.Heal = 28f;
                break;
        }
    }
}

[Serializable]
public sealed class CombatAiObjectiveWeights
{
    public float AttackEnemy;
    public float DefendOwnStone;
    public float SupportAlly;
    public float DestroyEnemyStone;
    public float Search;
    public float Retreat;
}

[Serializable]
public sealed class CombatAiMoveWeights
{
    public float AdvanceEnemyStone;
    public float ReturnOwnStone;
    public float PursueEnemy;
    public float SupportAlly;
    public float TakeHighGround;
    public float MoveForest;
    public float SearchLastKnown;
    public float HoldPosition;
}

[Serializable]
public sealed class CombatAiSkillWeights
{
    public float Damage;
    public float Protect;
    public float Heal;
    public float Buff;
    public float Debuff;
    public float Stealth;
}
