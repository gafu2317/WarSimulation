using System.Collections.Generic;

public static class CombatSkillLoadoutBuilder
{
    public static IReadOnlyList<SkillBase> Build(
        CombatSkillCatalog catalog,
        WeaponKind equippedKind,
        IReadOnlyList<SkillId> learnedSkillIds,
        IReadOnlyList<SkillId> grantedSkillIds,
        bool unlockAllCatalogSkillsForKindWhenLearnedEmpty,
        WeaponBase equippedWeapon = null)
    {
        if (catalog == null || equippedKind == WeaponKind.Unarmed)
        {
            return System.Array.Empty<SkillBase>();
        }

        var resolvedIds = new HashSet<SkillId>();

        if (learnedSkillIds != null && learnedSkillIds.Count > 0)
        {
            for (int i = 0; i < learnedSkillIds.Count; i++)
            {
                SkillId skillId = learnedSkillIds[i];
                if (skillId == SkillId.None) continue;
                if (!catalog.TryGetDefinition(skillId, out SkillDefinition definition)) continue;
                if (definition.RequiredWeaponKind != equippedKind) continue;

                resolvedIds.Add(skillId);
            }
        }
        else if (unlockAllCatalogSkillsForKindWhenLearnedEmpty)
        {
            IReadOnlyList<SkillDefinition> kindDefinitions = catalog.GetDefinitionsForKind(equippedKind);
            for (int i = 0; i < kindDefinitions.Count; i++)
            {
                SkillDefinition definition = kindDefinitions[i];
                if (definition == null || definition.SkillId == SkillId.None) continue;

                resolvedIds.Add(definition.SkillId);
            }
        }

        if (grantedSkillIds != null)
        {
            for (int i = 0; i < grantedSkillIds.Count; i++)
            {
                SkillId skillId = grantedSkillIds[i];
                if (skillId == SkillId.None) continue;
                if (!catalog.TryGetDefinition(skillId, out SkillDefinition definition)) continue;
                if (definition.RequiredWeaponKind != equippedKind) continue;

                resolvedIds.Add(skillId);
            }
        }

        var skills = new List<SkillBase>(resolvedIds.Count);
        foreach (SkillId skillId in resolvedIds)
        {
            SkillBase skill = CombatSkillFactory.Create(skillId, equippedWeapon);
            if (skill != null)
            {
                skills.Add(skill);
            }
        }

        return skills;
    }
}
