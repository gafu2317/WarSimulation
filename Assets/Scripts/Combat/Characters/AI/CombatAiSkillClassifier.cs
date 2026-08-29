public static class CombatAiSkillClassifier
{
    public static bool IsBasicAttack(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Grimoire_Bolt or
            SkillId.Bible_Smite or
            SkillId.Rosary_Strike;
    }

    public static bool IsDamage(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Sword_Slash or
            SkillId.Shield_Slash or
            SkillId.Wand_Bolt or
            SkillId.Wand_ArcaneBlast or
            SkillId.Wand_AreaBlast or
            SkillId.Wand_GodsHand or
            SkillId.Grimoire_Bolt or
            SkillId.Bible_Smite or
            SkillId.Rosary_Strike or
            SkillId.Rosary_SacrificeThunder;
    }

    public static bool IsHighImpactSkill(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Wand_ArcaneBlast or
            SkillId.Wand_AreaBlast or
            SkillId.Wand_GodsHand or
            SkillId.Rosary_CloseHeal or
            SkillId.Rosary_Regeneration or
            SkillId.Rosary_HealingArea or
            SkillId.Rosary_SacrificeThunder;
    }

    public static bool IsBuff(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Bible_StrBuff or
            SkillId.Bible_FaiBuff or
            SkillId.Bible_IntBuff or
            SkillId.Bible_AgiBuff or
            SkillId.Bible_Invulnerable or
            SkillId.Bible_Gotsume;
    }

    public static bool IsDebuff(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Grimoire_StrDebuff or
            SkillId.StatDebuff_INT or
            SkillId.StatDebuff_FAI or
            SkillId.StatDebuff_AGI or
            SkillId.Grimoire_Bind or
            SkillId.Grimoire_Poison;
    }

    public static bool IsHeal(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Rosary_DistantHeal or
            SkillId.Rosary_CloseHeal or
            SkillId.Rosary_Regeneration or
            SkillId.Rosary_HealingArea;
    }

    public static bool IsProtect(SkillBase skill)
    {
        return skill != null && skill.Id is
            SkillId.Bible_Invulnerable or
            SkillId.Bible_Gotsume or
            SkillId.Shield_ShoulderGuard;
    }

    public static bool IsMobility(SkillBase skill)
    {
        return skill != null && skill.Id == SkillId.Bible_CarryRush;
    }

    public static bool IsStealth(SkillBase skill)
    {
        return skill != null && skill.Id == SkillId.Grimoire_Stealth;
    }

    public static bool IsSupport(SkillBase skill)
    {
        return IsBuff(skill) || IsHeal(skill) || IsProtect(skill);
    }
}
