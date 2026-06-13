using System;

public static class CombatSkillUseEvents
{
    public static event Action<Character, string> SkillUsed;

    public static void RaiseSkillUsed(Character user, string skillName)
    {
        if (user == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        SkillUsed?.Invoke(user, skillName);
    }
}
