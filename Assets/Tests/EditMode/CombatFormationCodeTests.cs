using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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

        Assert.That(code, Is.EqualTo("ぬあ"));

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

    [Test]
    public void BuiltInProfile_RegistersAttentionSeekerNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.AttentionSeeker);
        try
        {
            Assert.That(profile.DisplayNameJapanese, Is.EqualTo("陽キャ"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("集まる場所"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsGatekeeperWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Shield, CombatAiPersonalityKind.Gatekeeper),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Shield));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.Gatekeeper));
    }

    [Test]
    public void BuiltInProfile_RegistersGatekeeperNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Gatekeeper);
        try
        {
            Assert.That(profile.DisplayNameJapanese, Is.EqualTo("門番"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("魔石"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsTagalongWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Bible, CombatAiPersonalityKind.Tagalong),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Bible));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.Tagalong));
    }

    [Test]
    public void BuiltInProfile_RegistersTagalongNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Tagalong);
        try
        {
            Assert.That(profile.DisplayNameJapanese, Is.EqualTo("便乗屋"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("近い味方"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsAvengerWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Sword, CombatAiPersonalityKind.Avenger),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Sword));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.Avenger));
    }

    [Test]
    public void BuiltInProfile_RegistersAvengerNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Avenger);
        try
        {
            Assert.That(profile.DisplayNameJapanese, Is.EqualTo("復讐鬼"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("攻撃した敵"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsBigMagicWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Wand, CombatAiPersonalityKind.BigMagic),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Wand));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.BigMagic));
    }

    [Test]
    public void BuiltInProfile_RegistersBigMagicNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.BigMagic);
        try
        {
        Assert.That(profile.DisplayNameJapanese, Is.EqualTo("浪漫派"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("基本攻撃"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsHighGroundWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Grimoire, CombatAiPersonalityKind.HighGround),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Grimoire));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.HighGround));
    }

    [Test]
    public void BuiltInProfile_RegistersHighGroundNameAndDescription()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.HighGround);
        try
        {
        Assert.That(profile.DisplayNameJapanese, Is.EqualTo("高所好き"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("高所"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EncodeAndDecode_SupportsStandoffSiegeWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Wand, CombatAiPersonalityKind.StandoffSiege),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Wand));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.StandoffSiege));
    }

    [Test]
    public void BuiltInProfile_RegistersStandoffSiegeAsScared()
    {
        CombatAiPersonalityProfile profile =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.StandoffSiege);
        try
        {
            Assert.That(profile.DisplayNameJapanese, Is.EqualTo("怖がり"));
            Assert.That(profile.BehaviorDescriptionJapanese, Does.Contain("敵魔石"));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }
}
