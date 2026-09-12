using System.Collections.Generic;

public static class CombatStatusIconSource
{
    public static string GetLabel(CombatStatusIconKind kind) => kind switch
    {
        CombatStatusIconKind.STRBuff => "STRバフ",
        CombatStatusIconKind.INTBuff => "INTバフ",
        CombatStatusIconKind.FAIBuff => "FAIバフ",
        CombatStatusIconKind.AGIBuff => "AGIバフ",
        CombatStatusIconKind.STRDebuff => "STRデバフ",
        CombatStatusIconKind.INTDebuff => "INTデバフ",
        CombatStatusIconKind.FAIDebuff => "FAIデバフ",
        CombatStatusIconKind.AGIDebuff => "AGIデバフ",
        CombatStatusIconKind.Invulnerable => "無敵",
        CombatStatusIconKind.Stealth => "不可視",
        CombatStatusIconKind.Reflection => "ゴツメ",
        CombatStatusIconKind.ShoulderGuard => "肩代わり",
        CombatStatusIconKind.CarryRush => "高速移動",
        CombatStatusIconKind.Root => "移動不能",
        CombatStatusIconKind.Poison => "毒",
        CombatStatusIconKind.Bind => "行動不能",
        _ => kind.ToString(),
    };

    public static void Collect(Character character, List<CombatStatusIconKind> icons)
    {
        icons.Clear();
        if (character == null || character.Health == null || !character.Health.IsAlive) return;

        foreach (CombatStatusEffectSnapshot effect in character.StatusEffects.GetActiveEffectSnapshots())
        {
            if (TryResolve(effect, out CombatStatusIconKind kind) && !icons.Contains(kind))
                icons.Add(kind);
        }

        if (character.GetComponent<BibleGotsumeEffect>()?.IsActive == true)
            icons.Add(CombatStatusIconKind.Reflection);
        if (character.GetComponent<ShieldShoulderGuardEffect>()?.IsActive == true)
            icons.Add(CombatStatusIconKind.ShoulderGuard);

        // 高速移動のコンポーネントは運搬者だけにあるため、同乗者は親から参照する。
        BibleCarryRushEffect carry = character.GetComponentInParent<BibleCarryRushEffect>();
        if (carry != null && carry.Affects(character)) icons.Add(CombatStatusIconKind.CarryRush);
        icons.Sort();
    }

    public static bool TryResolve(CombatStatusEffectSnapshot effect, out CombatStatusIconKind kind)
    {
        kind = default;
        switch (effect.Type)
        {
            case CombatStatusEffects.EffectType.StatModifier:
                if (!effect.IsBuff && !effect.IsDebuff) return false;
                kind = (CombatStatusIconKind)((int)(effect.IsBuff
                    ? CombatStatusIconKind.STRBuff : CombatStatusIconKind.STRDebuff) + (int)effect.Stat);
                return true;
            case CombatStatusEffects.EffectType.Invulnerable: kind = CombatStatusIconKind.Invulnerable; return true;
            case CombatStatusEffects.EffectType.Stealth: kind = CombatStatusIconKind.Stealth; return true;
            case CombatStatusEffects.EffectType.Root: kind = CombatStatusIconKind.Root; return true;
            case CombatStatusEffects.EffectType.Bind: kind = CombatStatusIconKind.Bind; return true;
            case CombatStatusEffects.EffectType.Poison: kind = CombatStatusIconKind.Poison; return true;
            default: return false;
        }
    }
}
