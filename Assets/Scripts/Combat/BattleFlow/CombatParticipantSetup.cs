public sealed class CombatParticipantSetup
{
    public Character Character { get; }
    public WeaponConfig Weapon { get; }
    public CombatAiPersonalityProfile Personality { get; }

    public CombatParticipantSetup(
        Character character,
        WeaponConfig weapon,
        CombatAiPersonalityProfile personality)
    {
        Character = character;
        Weapon = weapon;
        Personality = personality;
    }
}
