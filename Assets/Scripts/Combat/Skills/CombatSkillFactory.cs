public static class CombatSkillFactory
{
    public static SkillBase Create(SkillId skillId)
    {
        SkillBase inner = skillId switch
        {
            SkillId.Sword_Slash => new SwordSlashSkill(),
            SkillId.Shield_Guard => new ShieldGuardSkill(),
            SkillId.Wand_Bolt => new WandBoltSkill(),
            SkillId.Grimoire_StrDebuff => new GrimoireStrDebuffSkill(),
            SkillId.Bible_Heal => new BibleHealSkill(),
            SkillId.Rosary_FaithBuff => new RosaryFaithBuffSkill(),
            _ => null,
        };

        if (inner == null) return null;

        return new IdentifiedSkill(inner, skillId);
    }
}
