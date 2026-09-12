using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Status Effect Icons")]
public sealed class CombatStatusEffectIconCatalog : ScriptableObject
{
    [SerializeField] private Sprite[] _sprites;

    public static CombatStatusEffectIconCatalog Default =>
        Resources.Load<CombatStatusEffectIconCatalog>("Combat/UI/StatusEffectIcons");

    public Sprite GetSprite(CombatStatusIconKind kind)
    {
        int index = (int)kind;
        return _sprites != null && index >= 0 && index < _sprites.Length ? _sprites[index] : null;
    }
}
