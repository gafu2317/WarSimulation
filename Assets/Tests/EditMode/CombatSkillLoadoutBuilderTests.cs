using System.Collections.Generic;
using NUnit.Framework;

public sealed class CombatSkillLoadoutBuilderTests
{
    [Test]
    public void Build_UnlocksAllKindSkillsWhenLearnedListEmptyAndStubEnabled()
    {
        CombatSkillCatalog catalog = CreateCatalog(
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Sword_Slash, WeaponKind.Sword),
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Wand_Bolt, WeaponKind.Wand));

        IReadOnlyList<SkillBase> skills = CombatSkillLoadoutBuilder.Build(
            catalog,
            WeaponKind.Sword,
            learnedSkillIds: System.Array.Empty<SkillId>(),
            grantedSkillIds: System.Array.Empty<SkillId>(),
            unlockAllCatalogSkillsForKindWhenLearnedEmpty: true);

        Assert.That(skills.Count, Is.EqualTo(1));
        Assert.That(skills[0], Is.TypeOf<IdentifiedSkill>());
        Assert.That(((IdentifiedSkill)skills[0]).SkillId, Is.EqualTo(SkillId.Sword_Slash));
    }

    [Test]
    public void Build_FiltersLearnedSkillsByEquippedKind()
    {
        CombatSkillCatalog catalog = CreateCatalog(
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Sword_Slash, WeaponKind.Sword),
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Wand_Bolt, WeaponKind.Wand));

        IReadOnlyList<SkillBase> skills = CombatSkillLoadoutBuilder.Build(
            catalog,
            WeaponKind.Sword,
            learnedSkillIds: new[] { SkillId.Sword_Slash, SkillId.Wand_Bolt },
            grantedSkillIds: System.Array.Empty<SkillId>(),
            unlockAllCatalogSkillsForKindWhenLearnedEmpty: false);

        Assert.That(skills.Count, Is.EqualTo(1));
        Assert.That(((IdentifiedSkill)skills[0]).SkillId, Is.EqualTo(SkillId.Sword_Slash));
    }

    [Test]
    public void Build_UnionsGrantedSkillsWithLearnedSkills()
    {
        CombatSkillCatalog catalog = CreateCatalog(
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Sword_Slash, WeaponKind.Sword),
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Shield_Guard, WeaponKind.Shield));

        IReadOnlyList<SkillBase> skills = CombatSkillLoadoutBuilder.Build(
            catalog,
            WeaponKind.Shield,
            learnedSkillIds: System.Array.Empty<SkillId>(),
            grantedSkillIds: new[] { SkillId.Shield_Guard },
            unlockAllCatalogSkillsForKindWhenLearnedEmpty: false);

        Assert.That(skills.Count, Is.EqualTo(1));
        Assert.That(((IdentifiedSkill)skills[0]).SkillId, Is.EqualTo(SkillId.Shield_Guard));
    }

    private static CombatSkillCatalog CreateCatalog(params SkillDefinition[] definitions)
    {
        return CombatEditModeTestUtil.CreateTestSkillCatalog(definitions);
    }
}
