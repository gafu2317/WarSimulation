using System;
using System.Collections.Generic;
using System.Text;

public readonly struct CombatFormationCodeEntry
{
    public bool Selected { get; }
    public WeaponKind Weapon { get; }
    public CombatAiPersonalityKind Personality { get; }

    public CombatFormationCodeEntry(
        bool selected,
        WeaponKind weapon,
        CombatAiPersonalityKind personality)
    {
        Selected = selected;
        Weapon = weapon;
        Personality = personality;
    }
}

public sealed class CombatFormationCodeData
{
    public IReadOnlyList<CombatFormationCodeEntry> Entries { get; }

    public CombatFormationCodeData(IReadOnlyList<CombatFormationCodeEntry> entries)
    {
        Entries = entries;
    }
}

public static class CombatFormationCode
{
    private const string Alphabet =
        "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん" +
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly int AlphabetSize = Alphabet.Length;

    private static readonly WeaponKind[] CodeWeaponKinds =
    {
        WeaponKind.Unarmed,
        WeaponKind.Sword,
        WeaponKind.Shield,
        WeaponKind.Wand,
        WeaponKind.Grimoire,
        WeaponKind.Bible,
        WeaponKind.Rosary,
    };

    // 性格追加時は末尾へ追加する。途中へ挿入・並べ替えすると既存コードの意味が変わるため。
    // 削除した性格の枠を詰めると既存コードの意味が変わるため、空きスロットを保持する。
    private static readonly CombatAiPersonalityKind?[] CodePersonalityKinds =
    {
        CombatAiPersonalityKind.Neutral,
        CombatAiPersonalityKind.AttentionSeeker,
        CombatAiPersonalityKind.BattleJunkie,
        CombatAiPersonalityKind.Cunning,
        CombatAiPersonalityKind.Devoted,
        CombatAiPersonalityKind.Lonely,
        CombatAiPersonalityKind.Reckless,
        CombatAiPersonalityKind.Gatekeeper,
        CombatAiPersonalityKind.Tagalong,
        CombatAiPersonalityKind.Avenger,
        null,
        CombatAiPersonalityKind.HighGround,
        null,
    };
    // 21性格を超える場合はAlphabetへ文字を追加し、PersonalitySlotCountも増やす。
    // その場合は形式が変わるため、旧コード互換が必要なら旧デコーダを残す。
    private const int PersonalitySlotCount = 21;

    public static string Encode(
        IReadOnlyList<CombatFormationCodeEntry> entries)
    {
        ValidateInputs(entries);

        var builder = new StringBuilder(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            builder.Append(EncodeEntry(entries[i]));
        }

        return builder.ToString();
    }

    public static bool TryDecode(
        string code,
        int entryCount,
        out CombatFormationCodeData data,
        out string error)
    {
        data = null;
        error = string.Empty;
        string normalized = Normalize(code);
        if (entryCount < 0)
        {
            error = "候補キャラ数が正しくありません。";
            return false;
        }

        int expectedLength = entryCount;
        if (normalized.Length != expectedLength)
        {
            error = $"コードの文字数が違います。必要な文字数: {expectedLength}文字";
            return false;
        }

        var entries = new List<CombatFormationCodeEntry>(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            if (!TryDecodeEntry(normalized[i], out CombatFormationCodeEntry entry))
            {
                error = $"コードの{i + 1}文字目が対応していません。";
                return false;
            }

            entries.Add(entry);
        }

        data = new CombatFormationCodeData(entries);
        return true;
    }

    private static void ValidateInputs(IReadOnlyList<CombatFormationCodeEntry> entries)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));

        int requiredAlphabetSize = 1 + CodeWeaponKinds.Length * PersonalitySlotCount;
        if (AlphabetSize < requiredAlphabetSize || CodePersonalityKinds.Length > PersonalitySlotCount)
        {
            throw new InvalidOperationException(
                "編成コードの容量が不足しています。AlphabetまたはPersonalitySlotCountを拡張してください。");
        }
    }

    private static char EncodeEntry(CombatFormationCodeEntry entry)
    {
        if (!entry.Selected) return Alphabet[0];

        int weaponIndex = Array.IndexOf(CodeWeaponKinds, entry.Weapon);
        int personalityIndex = Array.IndexOf(CodePersonalityKinds, (CombatAiPersonalityKind?)entry.Personality);
        if (weaponIndex < 0 || personalityIndex < 0)
        {
            throw new ArgumentException("編成コードに対応していない武器または性格です。");
        }

        int codeIndex = 1 + weaponIndex * PersonalitySlotCount + personalityIndex;
        return Alphabet[codeIndex];
    }

    private static bool TryDecodeEntry(char value, out CombatFormationCodeEntry entry)
    {
        entry = default;
        if (!TryGetAlphabetIndex(value, out int codeIndex)) return false;
        if (codeIndex == 0)
        {
            entry = new CombatFormationCodeEntry(
                false,
                WeaponKind.Unarmed,
                CombatAiPersonalityKind.Neutral);
            return true;
        }

        int combinationIndex = codeIndex - 1;
        int weaponIndex = combinationIndex / PersonalitySlotCount;
        int personalityIndex = combinationIndex % PersonalitySlotCount;
        if (weaponIndex >= CodeWeaponKinds.Length) return false;
        if (personalityIndex >= CodePersonalityKinds.Length || !CodePersonalityKinds[personalityIndex].HasValue) return false;

        entry = new CombatFormationCodeEntry(
            true,
            CodeWeaponKinds[weaponIndex],
            CodePersonalityKinds[personalityIndex].Value);
        return true;
    }

    private static bool TryGetAlphabetIndex(char value, out int index)
    {
        index = Alphabet.IndexOf(value);
        return index >= 0;
    }

    private static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        var builder = new StringBuilder(code.Length);
        for (int i = 0; i < code.Length; i++)
        {
            if (!char.IsWhiteSpace(code[i])) builder.Append(code[i]);
        }

        return builder.ToString();
    }
}
