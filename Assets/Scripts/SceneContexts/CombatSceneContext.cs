using UnityEngine;

public class CombatSceneContext : SceneContextBase<CombatSceneContext>
{
    [field: SerializeField] public CombatCharacterSystem CharacterSystem { get; private set; }
    [field: SerializeField] public CombatMapSystem MapSystem { get; private set; }
}
