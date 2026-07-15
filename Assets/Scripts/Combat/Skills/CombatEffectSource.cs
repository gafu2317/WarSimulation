public readonly struct CombatEffectSource
{
    public static readonly CombatEffectSource None = new CombatEffectSource(null, SkillId.None, null);

    public CombatEffectSource(Character character, SkillId skillId, string skillName)
    {
        Character = character;
        SkillId = skillId;
        SkillName = skillName;
    }

    public Character Character { get; }
    public SkillId SkillId { get; }
    public string SkillName { get; }
    public bool HasCharacter => Character != null;

    public static CombatEffectSource Capture(Character character)
    {
        return CombatSkillActionEvents.ResolveSource(character);
    }
}
