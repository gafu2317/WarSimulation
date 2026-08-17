using System.Collections.Generic;
using NUnit.Framework;

public sealed class CombatFormationCodeTests
{
    [Test]
    public void EncodeAndDecode_UsesOneCharacterPerCandidateAndPreservesStates()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Sword, CombatAiPersonalityKind.Neutral),
            new(false, WeaponKind.Wand, CombatAiPersonalityKind.Reckless),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(code.Length, Is.EqualTo(entries.Count));
        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Sword));
        Assert.That(data.Entries[1].Selected, Is.False);
        Assert.That(data.Entries[1].Personality, Is.EqualTo(CombatAiPersonalityKind.Neutral));
    }

    [Test]
    public void Decode_RejectsUnsupportedFormationCharacter()
    {
        string modified = "!";

        Assert.That(
            CombatFormationCode.TryDecode(modified, 1, out _, out string error),
            Is.False);
        Assert.That(error, Does.Contain("対応していません"));
    }
}
