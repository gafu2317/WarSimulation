using System.Collections.Generic;

public sealed class CombatParticipantSetup
{
    public Character Character { get; }
    public WeaponConfig Weapon { get; }
    public CombatAiPersonalityProfile Personality { get; }
    public Character TagalongTarget { get; }
    public float MovementSpeedMultiplier { get; }
    public IReadOnlyDictionary<CombatStat, int> StatAdjustments { get; }

    public CombatParticipantSetup(
        Character character,
        WeaponConfig weapon,
        CombatAiPersonalityProfile personality,
        float movementSpeedMultiplier = 1f,
        Character tagalongTarget = null,
        IReadOnlyDictionary<CombatStat, int> statAdjustments = null)
    {
        Character = character;
        Weapon = weapon;
        Personality = personality;
        TagalongTarget = tagalongTarget;
        MovementSpeedMultiplier = movementSpeedMultiplier;
        var copiedAdjustments = new Dictionary<CombatStat, int>();
        StatAdjustments = copiedAdjustments;
        if (statAdjustments == null) return;

        foreach (KeyValuePair<CombatStat, int> adjustment in statAdjustments)
        {
            if (adjustment.Value != 0)
            {
                copiedAdjustments[adjustment.Key] = adjustment.Value;
            }
        }
    }
}
