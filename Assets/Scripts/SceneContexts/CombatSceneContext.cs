using UnityEngine;

public class CombatSceneContext : SceneContextBase<CombatSceneContext>
{
    [field: SerializeField] public CombatMapSystem MapSystem { get; private set; }
}
