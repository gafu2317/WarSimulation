using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CombatSceneContext : SceneContextBase<CombatSceneContext>
{
    [field: SerializeField] public CombatCharacterSystem CharacterSystem { get; private set; }
    [field: SerializeField] public CombatMapSystem MapSystem { get; private set; }
    [field: SerializeField] public CombatMagicStoneSystem MagicStoneSystem { get; private set; }
    [field: SerializeField] public CombatBattleFlow BattleFlow { get; private set; }
    [field: SerializeField] public CombatSkillCatalog SkillCatalog { get; private set; }
    [field: SerializeField] public CombatAiWeaponWeightsProfile AiWeaponWeightsProfile { get; private set; }

    protected override void OnAwakeInitialize()
    {
        EnsureBattleSystems();
    }

    private void EnsureBattleSystems()
    {
        MagicStoneSystem ??= GetComponent<CombatMagicStoneSystem>();
        if (MagicStoneSystem == null)
        {
            MagicStoneSystem = gameObject.AddComponent<CombatMagicStoneSystem>();
        }

        BattleFlow ??= GetComponent<CombatBattleFlow>();
        if (BattleFlow == null)
        {
            BattleFlow = gameObject.AddComponent<CombatBattleFlow>();
        }

        BattleFlow.SetMagicStoneSystem(MagicStoneSystem);
    }
}
