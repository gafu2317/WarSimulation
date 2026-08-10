public readonly struct CombatEffectSource
{
    public static readonly CombatEffectSource None = new CombatEffectSource(null, SkillId.None, null);

    public CombatEffectSource(
        Character character,
        SkillId skillId,
        string skillName,
        bool isReactiveDamage = false)
    {
        Character = character;
        SkillId = skillId;
        SkillName = skillName;
        IsReactiveDamage = isReactiveDamage;
    }

    public Character Character { get; }
    public SkillId SkillId { get; }
    public string SkillName { get; }
    public bool HasCharacter => Character != null;
    public bool IsReactiveDamage { get; }

    public static CombatEffectSource Capture(Character character)
    {
        return CombatSkillActionEvents.ResolveSource(character);
    }

    public CombatEffectSource AsReactiveDamage()
    {
        return new CombatEffectSource(Character, SkillId, SkillName, isReactiveDamage: true);
    }
}
