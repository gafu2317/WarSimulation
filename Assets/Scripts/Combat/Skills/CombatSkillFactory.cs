public static class CombatSkillFactory
{
    public static SkillBase Create(SkillId skillId)
    {
        SkillBase inner = skillId switch
        {
            SkillId.Sword_Slash => new SwordSlashSkill(),
            SkillId.Bible_StrBuff => CreateStatBuff(
                CombatStatusEffects.StatKind.STR,
                buffMultiplier: 1.25f,
                durationSeconds: 5f,
                cooldownSeconds: 5f,
                name: "守護"),
            SkillId.Wand_Bolt => new WandBoltSkill(),
            SkillId.Wand_ArcaneBlast => new WandArcaneBlastSkill(),
            SkillId.Grimoire_StrDebuff => CreateStatDebuff(
                CombatStatusEffects.StatKind.STR,
                name: "STRデバフ"),
            SkillId.Bible_FaiBuff => CreateStatBuff(
                CombatStatusEffects.StatKind.FAI,
                buffMultiplier: 1.2f,
                durationSeconds: 6f,
                cooldownSeconds: 6f,
                name: "信仰バフ"),
            SkillId.Bible_IntBuff => CreateStatBuff(CombatStatusEffects.StatKind.INT),
            SkillId.Bible_AgiBuff => CreateStatBuff(CombatStatusEffects.StatKind.AGI),
            SkillId.StatDebuff_INT => CreateStatDebuff(CombatStatusEffects.StatKind.INT),
            SkillId.StatDebuff_FAI => CreateStatDebuff(CombatStatusEffects.StatKind.FAI),
            SkillId.StatDebuff_AGI => CreateStatDebuff(CombatStatusEffects.StatKind.AGI),
            SkillId.Shield_Slash => new ShieldSlashSkill(),
            SkillId.Grimoire_Bolt => new GrimoireBoltSkill(),
            SkillId.Bible_Smite => new BibleSmiteSkill(),
            SkillId.Rosary_Strike => new RosaryStrikeSkill(),
            SkillId.Rosary_DistantHeal => new RosaryDistantHealSkill(),
            SkillId.Rosary_CloseHeal => new RosaryCloseHealSkill(),
            _ => null,
        };

        if (inner == null) return null;

        return new IdentifiedSkill(inner, skillId);
    }

    private static StatBuffSkill CreateStatBuff(
        CombatStatusEffects.StatKind stat,
        float buffMultiplier = 1.25f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        return new StatBuffSkill(stat, buffMultiplier, durationSeconds, cooldownSeconds, name);
    }

    private static StatDebuffSkill CreateStatDebuff(
        CombatStatusEffects.StatKind stat,
        float debuffMultiplier = 0.7f,
        float durationSeconds = 5f,
        float maxRange = 7f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        return new StatDebuffSkill(stat, debuffMultiplier, durationSeconds, maxRange, cooldownSeconds, name);
    }
}
