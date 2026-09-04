public sealed class CombatParticipantSetup
{
    public Character Character { get; }
    public WeaponConfig Weapon { get; }
    public CombatAiPersonalityProfile Personality { get; }
    public Character TagalongTarget { get; }
    public float MovementSpeedMultiplier { get; }

    public CombatParticipantSetup(
        Character character,
        WeaponConfig weapon,
        CombatAiPersonalityProfile personality,
        float movementSpeedMultiplier = 1f,
        Character tagalongTarget = null)
    {
        Character = character;
        Weapon = weapon;
        Personality = personality;
        TagalongTarget = tagalongTarget;
        MovementSpeedMultiplier = movementSpeedMultiplier;
    }
}
