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
        CombatAiPersonalityKind[] kinds = CombatAiPersonalityProfile.BuiltInKinds;
        if (!Kind.HasValue)
        {
            Kind = kinds[0];
            return;
        }

        int index = System.Array.IndexOf(kinds, Kind.Value);
        if (index < 0 || index >= kinds.Length - 1)
        {
            Kind = null;
            return;
        }

        Kind = kinds[index + 1];
    }
}
