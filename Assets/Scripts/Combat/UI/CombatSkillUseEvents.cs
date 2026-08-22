using System;
using UnityEngine;

public static class CombatSkillUseEvents
{
    public static event Action<Character, string> SkillUsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        SkillUsed = null;
    }

    public static void RaiseSkillUsed(Character user, string skillName)
    {
        if (user == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        Action<Character, string> handlers = SkillUsed;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate subscriber in handlers.GetInvocationList())
        {
            if (subscriber is not Action<Character, string> action)
            {
                continue;
            }

            if (action.Target is UnityEngine.Object targetObject && targetObject == null)
            {
                SkillUsed -= action;
                continue;
            }

            try
            {
                action(user, skillName);
            }
            catch (MissingReferenceException)
            {
                SkillUsed -= action;
            }
        }
    }
}
