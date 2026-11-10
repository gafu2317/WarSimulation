using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

    [TestCase("ヘ")]
    [TestCase("マ")]
    public void Decode_RejectsRemovedPersonalitySlots(string code)
    {
        Assert.That(
            CombatFormationCode.TryDecode(code, 1, out _, out string error),
            Is.False);
        Assert.That(error, Does.Contain("対応していません"));
    }

    [Test]
    public void EncodeAndDecode_SupportsHighGroundWithoutChangingEntryShape()
    {
        var entries = new List<CombatFormationCodeEntry>
        {
            new(true, WeaponKind.Grimoire, CombatAiPersonalityKind.HighGround),
        };

        string code = CombatFormationCode.Encode(entries);

        Assert.That(code, Is.EqualTo("E"));
        Assert.That(
            CombatFormationCode.TryDecode(code, entries.Count, out CombatFormationCodeData data, out string error),
            Is.True,
            error);
        Assert.That(data.Entries[0].Selected, Is.True);
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Grimoire));
        Assert.That(data.Entries[0].Personality, Is.EqualTo(CombatAiPersonalityKind.HighGround));
    }

    [TestCase(CombatAiPersonalityKind.LowPresence)]
    public void FormationCode_RoundTripsPersonalitySlots(CombatAiPersonalityKind kind)
    {
        string code = CombatFormationCode.Encode(new[]
        {
            new CombatFormationCodeEntry(true, WeaponKind.Bible, kind),
        });

        Assert.That(CombatFormationCode.TryDecode(code, 1, out var data, out string error), Is.True, error);
        Assert.That(data.Entries[0].Personality, Is.EqualTo(kind));
        Assert.That(data.Entries[0].Weapon, Is.EqualTo(WeaponKind.Bible));
    }

    [Test]
    public void BuiltInProfilesMatchSelectablePersonalities()
    {
        var profiles = CombatAiPersonalityProfile.CreateBuiltInProfiles();
        try
        {
            foreach (CombatAiPersonalityKind kind in System.Enum.GetValues(typeof(CombatAiPersonalityKind)))
            {
                Assert.That(profiles.Exists(profile => profile.Kind == kind), Is.True);
                Assert.That(CombatAiPersonalityProfile.IsSelectable(kind), Is.True);
            }
            Assert.That(CombatAiPersonalityProfile.IsSelectable((CombatAiPersonalityKind)int.MaxValue), Is.False);
        }
        finally
        {
            profiles.ForEach(profile => Object.DestroyImmediate(profile));
        }
    }
}

public sealed class CombatPersonalitySelectionTests
{
    [Test]
    public void AllBuiltInPersonalitiesCanBeSelectedAndConfirmed()
    {
        GameObject panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab"));
        var ally = new GameObject("Ally");
        var enemy = new GameObject("Enemy");
        try
        {
            CombatCharacterSelection selection = panel.GetComponentInChildren<CombatCharacterSelection>(true);
            int confirmations = 0;
            selection.Initialize(new[] { ally.AddComponent<Character>() }, new[] { enemy.AddComponent<Character>() },
                (_, _) => confirmations++);
            object row = Read<IList>(selection, "_allyRows")[0];
            var personalityButton = (Button)row.GetType().GetField("PersonalityButton").GetValue(row);
            personalityButton.onClick.Invoke();
            Assert.That(Read<Transform>(selection, "_pickerContent").childCount, Is.EqualTo(12));

            foreach (CombatAiPersonalityKind kind in CombatAiPersonalityProfile.BuiltInKinds)
            {
                Read<TMP_InputField>(selection, "_formationCodeInput").text = CombatFormationCode.Encode(new[]
                {
                    new CombatFormationCodeEntry(true, WeaponKind.Sword, kind),
                });
                Read<Button>(selection, "_formationCodeApplyButton").onClick.Invoke();
                int index = (int)row.GetType().GetField("PersonalityIndex").GetValue(row);
                Assert.That(Read<List<CombatAiPersonalityProfile>>(selection, "_personalityOptions")[index].Kind,
                    Is.EqualTo(kind));
                typeof(CombatCharacterSelection).GetMethod("ConfirmSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(selection, null);
            }
            Assert.That(confirmations, Is.EqualTo(12));
        }
        finally
        {
            Object.DestroyImmediate(panel);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(enemy);
        }
    }

    private static T Read<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
}
