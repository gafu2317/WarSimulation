public static class CombatAiWeaponWeights
{
    private static readonly CombatAiWeaponWeightsEntry DefaultSword = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Sword);
    private static readonly CombatAiWeaponWeightsEntry DefaultShield = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Shield);
    private static readonly CombatAiWeaponWeightsEntry DefaultWand = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Wand);
    private static readonly CombatAiWeaponWeightsEntry DefaultGrimoire = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Grimoire);
    private static readonly CombatAiWeaponWeightsEntry DefaultBible = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Bible);
    private static readonly CombatAiWeaponWeightsEntry DefaultRosary = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Rosary);
    private static readonly CombatAiWeaponWeightsEntry DefaultUnarmed = CombatAiWeaponWeightsEntry.CreateDefault(WeaponKind.Unarmed);

    public static float GetObjectiveWeight(WeaponKind kind, CombatObjective objective)
    {
        return GetDefaultEntry(kind).GetObjectiveWeight(objective);
    }

    public static float GetMoveWeight(WeaponKind kind, string moveCode)
    {
        return GetDefaultEntry(kind).GetMoveWeight(moveCode);
    }

    public static float GetSkillWeight(WeaponKind kind, SkillBase skill)
    {
        return GetDefaultEntry(kind).GetSkillWeight(skill);
    }

    private static CombatAiWeaponWeightsEntry GetDefaultEntry(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => DefaultSword,
            WeaponKind.Shield => DefaultShield,
            WeaponKind.Wand => DefaultWand,
            WeaponKind.Grimoire => DefaultGrimoire,
            WeaponKind.Bible => DefaultBible,
            WeaponKind.Rosary => DefaultRosary,
            _ => DefaultUnarmed,
        };
    }
}

public sealed class CombatAiWeaponWeightsEntry
{
    private CombatAiObjectiveWeights _objectives = new();
    private CombatAiMoveWeights _moves = new();
    private CombatAiSkillWeights _skills = new();

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
            CombatAiMoveCode.AdvanceEnemyStone => _moves.AdvanceEnemyStone,
            CombatAiMoveCode.ReturnOwnStone => _moves.ReturnOwnStone,
            CombatAiMoveCode.PursueEnemy => _moves.PursueEnemy,
            CombatAiMoveCode.SupportAlly => _moves.SupportAlly,
            CombatAiMoveCode.InterceptThreat => _moves.InterceptThreat,
            CombatAiMoveCode.TakeHighGround => _moves.TakeHighGround,
            CombatAiMoveCode.MoveForest => _moves.MoveForest,
            CombatAiMoveCode.SearchLastKnown => _moves.SearchLastKnown,
            CombatAiMoveCode.HoldPosition => _moves.HoldPosition,
            _ => 0f,
        };
    }

    public float GetSkillWeight(SkillBase skill)
    {
        if (CombatAiSkillClassifier.IsDamage(skill)) return DamageSkillWeight;
        if (CombatAiSkillClassifier.IsProtect(skill)) return ProtectSkillWeight;
        if (CombatAiSkillClassifier.IsHeal(skill)) return HealSkillWeight;
        if (CombatAiSkillClassifier.IsBuff(skill)) return BuffSkillWeight;
        if (CombatAiSkillClassifier.IsDebuff(skill)) return DebuffSkillWeight;
        if (CombatAiSkillClassifier.IsStealth(skill)) return StealthSkillWeight;
        return 0f;
    }

    public void ApplyDefaults(WeaponKind kind)
    {
        _objectives = new CombatAiObjectiveWeights();
        _moves = new CombatAiMoveWeights();
        _skills = new CombatAiSkillWeights();
        _moves.InterceptThreat = kind == WeaponKind.Shield ? 28f : -40f;

        switch (kind)
        {
            case WeaponKind.Sword:
                _objectives.AttackEnemy = 24f;
                _objectives.DestroyEnemyStone = 36f;
                _objectives.DefendOwnStone = 4f;
                _objectives.Search = -14f;
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
                _objectives.SupportAlly = 24f;
                _objectives.AttackEnemy = 8f;
                _objectives.DestroyEnemyStone = 4f;
                _objectives.Search = -12f;

                _moves.ReturnOwnStone = 22f;
                _moves.SupportAlly = 24f;
                _moves.PursueEnemy = 4f;

                _skills.Damage = 4f;
                _skills.Protect = 24f;
                break;
            case WeaponKind.Wand:
                _objectives.AttackEnemy = 24f;
                _objectives.DestroyEnemyStone = 36f;
                _objectives.Search = -8f;
                _objectives.Retreat = 8f;
                _objectives.SupportAlly = -12f;
                _objectives.DefendOwnStone = -6f;

                _moves.TakeHighGround = 20f;
                _moves.MoveForest = 12f;
                _moves.PursueEnemy = -10f;
                _moves.AdvanceEnemyStone = 16f;

                _skills.Damage = 20f;
                _skills.Protect = -10f;
                _skills.Heal = -10f;
                break;
            case WeaponKind.Grimoire:
                _objectives.AttackEnemy = 22f;
                _objectives.DestroyEnemyStone = 8f;
                _objectives.Search = 10f;
                _objectives.Retreat = 8f;
                _objectives.SupportAlly = -6f;
                _objectives.DefendOwnStone = -6f;

                _moves.TakeHighGround = 42f;
                _moves.MoveForest = 12f;
                _moves.PursueEnemy = -4f;
                _moves.AdvanceEnemyStone = 4f;
                _moves.SearchLastKnown = 8f;

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
                _moves.TakeHighGround = 40f;

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
                _moves.TakeHighGround = 20f;

                _skills.Damage = -6f;
                _skills.Protect = 10f;
                _skills.Heal = 28f;
                break;
        }
    }
}

public sealed class CombatAiObjectiveWeights
{
    public float AttackEnemy;
    public float DefendOwnStone;
    public float SupportAlly;
    public float DestroyEnemyStone;
    public float Search;
    public float Retreat;
}

public sealed class CombatAiMoveWeights
{
    public float AdvanceEnemyStone;
    public float ReturnOwnStone;
    public float PursueEnemy;
    public float SupportAlly;
    public float InterceptThreat;
    public float TakeHighGround;
    public float MoveForest;
    public float SearchLastKnown;
    public float HoldPosition;
}

public sealed class CombatAiSkillWeights
{
    public float Damage;
    public float Protect;
    public float Heal;
    public float Buff;
    public float Debuff;
    public float Stealth;
}
