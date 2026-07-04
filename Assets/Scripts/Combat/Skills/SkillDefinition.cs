using UnityEngine;

[CreateAssetMenu(
    fileName = "SkillDefinition",
    menuName = "WarSimulation/Combat/Skill Definition")]
public sealed class SkillDefinition : ScriptableObject
{
    [SerializeField] private SkillId _skillId = SkillId.None;
    [SerializeField] private WeaponKind _requiredWeaponKind = WeaponKind.Unarmed;
    [SerializeField, Min(0)] private int _unlockFacilityLevel;
    [SerializeField] private string _displayName;

    public SkillId SkillId => _skillId;
    public WeaponKind RequiredWeaponKind => _requiredWeaponKind;
    public int UnlockFacilityLevel => _unlockFacilityLevel;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? _skillId.ToString() : _displayName;

    public void ConfigureForTests(
        SkillId skillId,
        WeaponKind requiredWeaponKind,
        string displayName = null,
        int unlockFacilityLevel = 0)
    {
        _skillId = skillId;
        _requiredWeaponKind = requiredWeaponKind;
        _unlockFacilityLevel = unlockFacilityLevel;
        _displayName = displayName ?? skillId.ToString();
    }
}
