using UnityEngine;

public class CountrySceneContext : SceneContextBase<CountrySceneContext>
{
    [field: SerializeField] public CountryCycleSystem CountryCycleSystem { get; private set; }
}
