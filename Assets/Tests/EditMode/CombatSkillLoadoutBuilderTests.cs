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
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Bible_Smite, WeaponKind.Bible),
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Bible_StrBuff, WeaponKind.Bible));

        IReadOnlyList<SkillBase> skills = CombatSkillLoadoutBuilder.Build(
            catalog,
            WeaponKind.Bible,
            learnedSkillIds: System.Array.Empty<SkillId>(),
            grantedSkillIds: new[] { SkillId.Bible_StrBuff },
            unlockAllCatalogSkillsForKindWhenLearnedEmpty: false);

        Assert.That(skills.Count, Is.EqualTo(1));
        Assert.That(((IdentifiedSkill)skills[0]).SkillId, Is.EqualTo(SkillId.Bible_StrBuff));
    }

    [Test]
    public void Build_UsesEquippedWeaponRangeAndCooldownForBasicAttack()
    {
        CombatSkillCatalog catalog = CreateCatalog(
            CombatEditModeTestUtil.CreateTestSkillDefinition(SkillId.Grimoire_Bolt, WeaponKind.Grimoire));
        var weapon = new Grimoire(range: 18f, cooldown: 3f);

        IReadOnlyList<SkillBase> skills = CombatSkillLoadoutBuilder.Build(
            catalog,
            WeaponKind.Grimoire,
            learnedSkillIds: new[] { SkillId.Grimoire_Bolt },
            grantedSkillIds: System.Array.Empty<SkillId>(),
            unlockAllCatalogSkillsForKindWhenLearnedEmpty: false,
            equippedWeapon: weapon);

        Assert.That(skills, Has.Count.EqualTo(1));
        Assert.That(skills[0].MaxRange, Is.EqualTo(18f));
        Assert.That(skills[0].CooldownSeconds, Is.EqualTo(3f));
    }

    private static CombatSkillCatalog CreateCatalog(params SkillDefinition[] definitions)
    {
        return CombatEditModeTestUtil.CreateTestSkillCatalog(definitions);
    }
}
