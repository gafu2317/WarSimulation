using UnityEngine;

public static class CombatMagicStoneSystemResolver
{
    public static CombatMagicStoneSystem Resolve()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.MagicStoneSystem != null)
        {
            return context.MagicStoneSystem;
        }

        CombatBattleFlow battleFlow = context != null ? context.BattleFlow : null;
        battleFlow ??= Object.FindAnyObjectByType<CombatBattleFlow>();
        if (battleFlow != null)
        {
            CombatMagicStoneSystem onBattleFlow = battleFlow.GetComponent<CombatMagicStoneSystem>();
            if (onBattleFlow != null)
            {
                return onBattleFlow;
            }
        }

        return Object.FindAnyObjectByType<CombatMagicStoneSystem>();
    }
}
