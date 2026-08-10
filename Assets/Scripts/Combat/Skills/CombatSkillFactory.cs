public static class CombatSkillFactory
{
    public static SkillBase Create(SkillId skillId, WeaponBase weapon = null)
    {
        SkillBase inner = skillId switch
        {
            SkillId.Sword_Slash => new SwordSlashSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Sword, 2f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Sword, 0.9f)),
            SkillId.Bible_StrBuff => CreateStatBuff(
                CombatStatusEffects.StatKind.STR,
                buffMultiplier: 1.25f,
                durationSeconds: 5f,
                cooldownSeconds: 5f,
                name: "STRバフ"),
            SkillId.Wand_Bolt => new WandBoltSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Wand, 20f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Wand, 1.4f)),
            SkillId.Wand_ArcaneBlast => new WandArcaneBlastSkill(),
            SkillId.Wand_AreaBlast => new WandAreaBlastSkill(),
            SkillId.Wand_GodsHand => new WandGodsHandSkill(),
            SkillId.Grimoire_StrDebuff => CreateStatDebuff(
                CombatStatusEffects.StatKind.STR,
                name: "STRデバフ"),
            SkillId.Grimoire_Bind => new GrimoireBindSkill(),
            SkillId.Grimoire_Poison => new GrimoirePoisonSkill(),
            SkillId.Grimoire_Stealth => new GrimoireStealthSkill(),
            SkillId.Bible_FaiBuff => CreateStatBuff(
                CombatStatusEffects.StatKind.FAI,
                buffMultiplier: 1.2f,
                durationSeconds: 6f,
                cooldownSeconds: 6f,
                name: "FAIバフ"),
            SkillId.Bible_Invulnerable => new BibleInvulnerableSkill(),
            SkillId.Bible_Gotsume => new BibleGotsumeSkill(),
            SkillId.Bible_CarryRush => new BibleCarryRushSkill(),
            SkillId.Bible_IntBuff => CreateStatBuff(CombatStatusEffects.StatKind.INT),
            SkillId.Bible_AgiBuff => CreateStatBuff(CombatStatusEffects.StatKind.AGI),
            SkillId.StatDebuff_INT => CreateStatDebuff(CombatStatusEffects.StatKind.INT),
            SkillId.StatDebuff_FAI => CreateStatDebuff(CombatStatusEffects.StatKind.FAI),
            SkillId.StatDebuff_AGI => CreateStatDebuff(CombatStatusEffects.StatKind.AGI),
            SkillId.Shield_Slash => new ShieldSlashSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Shield, 2f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Shield, 1.1f)),
            SkillId.Shield_ShoulderGuard => new ShieldShoulderGuardSkill(),
            SkillId.Grimoire_Bolt => new GrimoireBoltSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Grimoire, 30f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Grimoire, 1.3f)),
            SkillId.Bible_Smite => new BibleSmiteSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Bible, 30f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Bible, 1.5f)),
            SkillId.Rosary_Strike => new RosaryStrikeSkill(
                maxRange: ResolveRange(weapon, WeaponKind.Rosary, 15f),
                cooldownSeconds: ResolveCooldown(weapon, WeaponKind.Rosary, 1.3f)),
            SkillId.Rosary_DistantHeal => new RosaryDistantHealSkill(),
            SkillId.Rosary_CloseHeal => new RosaryCloseHealSkill(),
            SkillId.Rosary_Regeneration => new RosaryRegenerationSkill(),
            SkillId.Rosary_HealingArea => new RosaryHealingAreaSkill(),
            SkillId.Rosary_SacrificeThunder => new RosarySacrificeThunderSkill(),
            _ => null,
        };

        if (inner == null) return null;

        return new IdentifiedSkill(inner, skillId);
    }

    private static float ResolveRange(WeaponBase weapon, WeaponKind expectedKind, float fallback)
    {
        return weapon != null && weapon.Kind == expectedKind ? weapon.Range : fallback;
    }

    private static float ResolveCooldown(WeaponBase weapon, WeaponKind expectedKind, float fallback)
    {
        return weapon != null && weapon.Kind == expectedKind ? weapon.CooldownSeconds : fallback;
    }

    private static StatBuffSkill CreateStatBuff(
        CombatStatusEffects.StatKind stat,
        float buffMultiplier = 1.25f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        return new StatBuffSkill(
            stat,
            buffMultiplier,
            durationSeconds,
            cooldownSeconds,
            name);
    }

    private static StatDebuffSkill CreateStatDebuff(
        CombatStatusEffects.StatKind stat,
        float debuffMultiplier = 0.7f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        return new StatDebuffSkill(stat, debuffMultiplier, durationSeconds, cooldownSeconds, name);
    }
}
