public static class CombatAiPersonalityHighlight
{
    public static bool HasHighlight => Kind.HasValue;

    public static CombatAiPersonalityKind? Kind { get; private set; }

    public static string DisplayLabel =>
        Kind.HasValue ? CombatAiPersonalityProfile.GetDisplayNameJapanese(Kind.Value) : "なし";

    public static void Set(CombatAiPersonalityKind? kind)
    {
        Kind = kind;
    }

    public static bool Matches(CombatAiPersonalityProfile profile)
    {
        return HasHighlight && profile != null && profile.Kind == Kind.Value;
    }

    public static void CycleNext()
    {
        if (!Kind.HasValue)
        {
            Kind = CombatAiPersonalityKind.Neutral;
            return;
        }

        int next = (int)Kind.Value + 1;
        if (next > (int)CombatAiPersonalityKind.Unstable)
        {
            Kind = null;
            return;
        }

        Kind = (CombatAiPersonalityKind)next;
    }
}
