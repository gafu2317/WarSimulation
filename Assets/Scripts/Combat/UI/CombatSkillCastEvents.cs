using System;

public static class CombatSkillCastEvents
{
    public static event Action<Character, SkillBase, float> CastStarted;
    public static event Action<Character, SkillBase> CastCompleted;
    public static event Action<Character, SkillBase> CastCancelled;

    public static void RaiseCastStarted(Character owner, SkillBase skill, float castTimeSeconds)
    {
        CastStarted?.Invoke(owner, skill, castTimeSeconds);
    }

    public static void RaiseCastCompleted(Character owner, SkillBase skill)
    {
        CastCompleted?.Invoke(owner, skill);
    }

    public static void RaiseCastCancelled(Character owner, SkillBase skill)
    {
        CastCancelled?.Invoke(owner, skill);
    }
}
