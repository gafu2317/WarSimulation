public static class CombatAiSkillClassifier
{
    public static bool IsBasicAttack(SkillBase skill)
    {
        return skill != null && skill.Name == "通常攻撃";
    }

    public static bool IsDamage(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return IsBasicAttack(skill)
            || code.Contains("Bolt")
            || code.Contains("Blast")
            || code.Contains("Slash")
            || code.Contains("Smite")
            || code.Contains("Strike")
            || code.Contains("Thunder");
    }

    public static bool IsBuff(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Buff") || code.Contains("Invulnerable") || code.Contains("Gotsume");
    }

    public static bool IsDebuff(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Debuff") || code.Contains("Poison") || code.Contains("Bind");
    }

    public static bool IsHeal(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Heal") || code.Contains("Regeneration") || code.Contains("HealingArea");
    }

    public static bool IsProtect(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Invulnerable") || code.Contains("Gotsume") || code.Contains("ShoulderGuard");
    }

    public static bool IsMobility(SkillBase skill)
    {
        return GetCode(skill).Contains("CarryRush");
    }

    public static bool IsStealth(SkillBase skill)
    {
        return GetCode(skill).Contains("Stealth");
    }

    public static bool IsSupport(SkillBase skill)
    {
        return IsBuff(skill) || IsHeal(skill) || IsProtect(skill);
    }

    private static string GetCode(SkillBase skill)
    {
        if (skill == null) return string.Empty;
        return skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
    }
}
