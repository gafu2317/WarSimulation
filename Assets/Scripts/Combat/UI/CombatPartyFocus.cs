using System;

public static class CombatPartyFocus
{
    public static Character Selected { get; private set; }

    public static event Action Changed;

    public static void Toggle(Character character)
    {
        if (character == null)
        {
            return;
        }

        Selected = Selected == character ? null : character;
        Changed?.Invoke();
    }

    public static void Clear()
    {
        if (ReferenceEquals(Selected, null))
        {
            return;
        }

        Selected = null;
        Changed?.Invoke();
    }
}
