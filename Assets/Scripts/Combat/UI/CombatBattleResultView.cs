using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatBattleResultView : MonoBehaviour
{
    private const string SummaryRootName = "BattleSummary";
    private const float CardWidth = 720f;
    private const float CardHeight = 620f;
    private const float TeamCardScale = 1.2f;
    private const float PersonalityColumnWidth = 110f;
    private const float NameColumnWidth = 170f;
    private const float WeaponColumnWidth = 70f;
    private const float MetricColumnWidth = 54f;
    private const float SupportNameColumnWidth = 135f;
    private const float SupportEffectColumnWidth = 220f;
    private const float SupportUsageColumnWidth = 160f;
    private const float SupportDurationColumnWidth = 145f;

    private sealed class RowUi
    {
        public GameObject Root { get; set; }
        public List<TMP_Text> Cells { get; } = new List<TMP_Text>();
    }

    private sealed class TeamUi
    {
        public TMP_Text Header { get; set; }
        public RectTransform Rows { get; set; }
        public RowUi HeaderRow { get; set; }
        public RowUi TotalRow { get; set; }
        public TMP_Text SupportSummaryText { get; set; }
        public TMP_Text SupportDurationText { get; set; }
        public RectTransform SupportRows { get; set; }
    }

    private RectTransform _summaryRoot;
    private TMP_FontAsset _font;
    private Material _fontMaterial;
    private TeamUi _allyUi;
    private TeamUi _enemyUi;

    public void EnsureBuilt()
    {
        if (_summaryRoot != null) return;

        TMP_Text sourceText = GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (sourceText == null)
        {
            Debug.LogWarning($"[{nameof(CombatBattleResultView)}] A TextMeshPro source is required.", this);
            return;
        }

        _font = sourceText.font;
        _fontMaterial = sourceText.fontSharedMaterial;
        _summaryRoot = CreateRoot();
        _allyUi = CreateTeamUi("AlliesCard", "味方", new Vector2(-450f, -20f), new Color(0.08f, 0.16f, 0.26f, 0.96f));
        _enemyUi = CreateTeamUi("EnemiesCard", "敵", new Vector2(450f, -20f), new Color(0.26f, 0.12f, 0.12f, 0.96f));

        MoveChild("ResultTitle", new Vector2(0f, 450f));
        MoveChild("BackToSelectionButton", new Vector2(0f, -490f));
        _summaryRoot.gameObject.SetActive(false);
    }

    public void Show(CombatBattleResult result)
    {
        EnsureBuilt();
        if (_summaryRoot == null || result == null) return;

        PopulateTeam(_allyUi, result.Allies);
        PopulateTeam(_enemyUi, result.Enemies);
        _summaryRoot.gameObject.SetActive(true);
    }

    public void Clear()
    {
        if (_summaryRoot != null)
        {
            _summaryRoot.gameObject.SetActive(false);
        }
    }

    private RectTransform CreateRoot()
    {
        GameObject rootObject = new GameObject(
            SummaryRootName,
            typeof(RectTransform),
            typeof(Image));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, -10f);
        root.sizeDelta = new Vector2(1800f, 860f);

        Image image = root.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.22f);
        image.raycastTarget = false;
        return root;
    }

    private TeamUi CreateTeamUi(
        string objectName,
        string teamName,
        Vector2 anchoredPosition,
        Color cardColor)
    {
        GameObject cardObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        RectTransform card = cardObject.GetComponent<RectTransform>();
        card.SetParent(_summaryRoot, false);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = anchoredPosition;
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.localScale = Vector3.one * TeamCardScale;

        Image image = cardObject.GetComponent<Image>();
        image.color = cardColor;
        image.raycastTarget = false;

        VerticalLayoutGroup layout = cardObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var teamUi = new TeamUi
        {
            Header = CreateLabel(card, "TeamHeader", teamName, 21f, TextAlignmentOptions.Left, 34f, Color.white),
        };
        teamUi.HeaderRow = CreateRow(card, "HeaderRow", isHeader: true, rowColor: new Color(0f, 0f, 0f, 0.18f));

        teamUi.Rows = CreateScrollableRows(card, "Rows", spacing: 3f);

        teamUi.TotalRow = CreateRow(card, "TotalRow", isHeader: false, rowColor: new Color(0f, 0f, 0f, 0.26f));
        CreateLabel(
            card,
            "SupportHeader",
            "支援・弱体",
            16f,
            TextAlignmentOptions.Left,
            24f,
            new Color(1f, 1f, 1f, 0.92f));
        teamUi.SupportSummaryText = CreateLabel(
            card,
            "SupportSummary",
            string.Empty,
            14f,
            TextAlignmentOptions.Left,
            22f,
            new Color(1f, 1f, 1f, 0.86f));
        teamUi.SupportSummaryText.richText = true;
        teamUi.SupportDurationText = CreateLabel(
            card,
            "SupportDuration",
            string.Empty,
            14f,
            TextAlignmentOptions.Left,
            22f,
            new Color(1f, 1f, 1f, 0.86f));
        teamUi.SupportDurationText.richText = true;

        RowUi supportHeader = CreateSupportRow(card, "SupportHeaderRow", true, new Color(0f, 0f, 0f, 0.18f));
        SetSupportRowValues(supportHeader, new[] { "名前", "付与内容", "使用量", "累積効果時間" });

        teamUi.SupportRows = CreateScrollableRows(card, "SupportRows", spacing: 2f);
        return teamUi;
    }

    private static RectTransform CreateScrollableRows(
        Transform parent,
        string objectName,
        float spacing)
    {
        GameObject scrollObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(ScrollRect),
            typeof(LayoutElement));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.SetParent(parent, false);
        scrollObject.GetComponent<LayoutElement>().flexibleHeight = 1f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRectTransform, false);
        Stretch(viewport);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.clear;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = spacing;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private RowUi CreateRow(Transform parent, string objectName, bool isHeader, Color rowColor)
    {
        GameObject rowObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        Image image = rowObject.GetComponent<Image>();
        image.color = rowColor;
        image.raycastTarget = false;

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 3);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredHeight = isHeader ? 38f : 32f;

        var rowUi = new RowUi { Root = rowObject };
        string[] labels = { "名前", "性格", "武器", "キャラ\n与ダメ", "魔石\n与ダメ", "被ダメ", "回復", "撃破", "死亡" };
        for (int i = 0; i < labels.Length; i++)
        {
            TextAlignmentOptions alignment = i <= 1
                ? TextAlignmentOptions.Left
                : (isHeader ? TextAlignmentOptions.Center : TextAlignmentOptions.Right);
            rowUi.Cells.Add(CreateCell(
                row,
                labels[i],
                alignment,
                isHeader,
                i == 0,
                i == 1,
                i == 2));
        }

        return rowUi;
    }

    private RowUi CreateSupportRow(Transform parent, string objectName, bool isHeader, Color rowColor)
    {
        GameObject rowObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        Image image = rowObject.GetComponent<Image>();
        image.color = rowColor;
        image.raycastTarget = false;

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 2, 2);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredHeight = isHeader ? 25f : 24f;

        var rowUi = new RowUi { Root = rowObject };
        rowUi.Cells.Add(CreateSupportCell(row, "名前", isHeader, SupportNameColumnWidth, TextAlignmentOptions.Left));
        rowUi.Cells.Add(CreateSupportCell(row, "付与内容", isHeader, SupportEffectColumnWidth, TextAlignmentOptions.Left));
        rowUi.Cells.Add(CreateSupportCell(row, "使用量", isHeader, SupportUsageColumnWidth, TextAlignmentOptions.Right));
        rowUi.Cells.Add(CreateSupportCell(row, "累積効果時間", isHeader, SupportDurationColumnWidth, TextAlignmentOptions.Right));
        return rowUi;
    }

    private TMP_Text CreateSupportCell(
        Transform parent,
        string textValue,
        bool isHeader,
        float width,
        TextAlignmentOptions alignment)
    {
        GameObject cellObject = new GameObject(
            "Cell",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        RectTransform cell = cellObject.GetComponent<RectTransform>();
        cell.SetParent(parent, false);

        LayoutElement element = cellObject.GetComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;

        TextMeshProUGUI text = cellObject.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _fontMaterial;
        text.text = textValue;
        text.fontSize = isHeader ? 13f : 13.5f;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private TMP_Text CreateCell(
        Transform parent,
        string textValue,
        TextAlignmentOptions alignment,
        bool isHeader,
        bool isName,
        bool isPersonality,
        bool isWeapon)
    {
        GameObject cellObject = new GameObject(
            "Cell",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        RectTransform cell = cellObject.GetComponent<RectTransform>();
        cell.SetParent(parent, false);

        LayoutElement element = cellObject.GetComponent<LayoutElement>();
        if (isName)
        {
            element.preferredWidth = NameColumnWidth;
            element.flexibleWidth = 1f;
        }
        else if (isPersonality)
        {
            element.minWidth = PersonalityColumnWidth;
            element.preferredWidth = PersonalityColumnWidth;
        }
        else if (isWeapon)
        {
            element.minWidth = WeaponColumnWidth;
            element.preferredWidth = WeaponColumnWidth;
        }
        else
        {
            element.minWidth = MetricColumnWidth;
            element.preferredWidth = MetricColumnWidth;
        }

        TextMeshProUGUI text = cellObject.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _fontMaterial;
        text.text = textValue;
        text.fontSize = isHeader ? 16f : 17f;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = isName || isPersonality
            ? TextOverflowModes.Ellipsis
            : TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private TMP_Text CreateLabel(
        Transform parent,
        string objectName,
        string textValue,
        float fontSize,
        TextAlignmentOptions alignment,
        float height,
        Color color)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _fontMaterial;
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void PopulateTeam(TeamUi teamUi, CombatBattleTeamResult result)
    {
        teamUi.Header.text = string.Format(
            CultureInfo.InvariantCulture,
            "{0}    魔石HP {1}/{2}",
            result.Team == CombatTeam.Ally ? "味方" : "敵",
            result.MagicStoneHp,
            result.MagicStoneMaxHp);
        SetRowValues(teamUi.HeaderRow, new[]
        {
            "名前",
            "性格",
            "武器",
            "キャラ\n与ダメ",
            "魔石\n与ダメ",
            "被ダメ",
            "回復",
            "撃破",
            "死亡",
        });
        SetRowValues(teamUi.TotalRow, new[]
        {
            "合計",
            string.Empty,
            string.Empty,
            result.DamageDealt.ToString(CultureInfo.InvariantCulture),
            result.MagicStoneDamage.ToString(CultureInfo.InvariantCulture),
            result.DamageTaken.ToString(CultureInfo.InvariantCulture),
            result.HealingDone.ToString(CultureInfo.InvariantCulture),
            SumDefeats(result).ToString(CultureInfo.InvariantCulture),
            SumDeaths(result).ToString(CultureInfo.InvariantCulture),
        });
        PopulateSupport(teamUi, result);
        ClearRows(teamUi.Rows);

        for (int i = 0; i < result.Characters.Count; i++)
        {
            CombatBattleCharacterResult character = result.Characters[i];
            RowUi row = CreateRow(
                teamUi.Rows,
                "CharacterRow",
                isHeader: false,
                rowColor: i % 2 == 0
                    ? new Color(0f, 0f, 0f, 0.12f)
                    : new Color(0f, 0f, 0f, 0.04f));
            SetRowValues(row, new[]
            {
                character.DisplayName,
                character.PersonalityDisplayName,
                character.WeaponDisplayName,
                character.DamageDealt.ToString(CultureInfo.InvariantCulture),
                character.MagicStoneDamage.ToString(CultureInfo.InvariantCulture),
                character.DamageTaken.ToString(CultureInfo.InvariantCulture),
                character.HealingDone.ToString(CultureInfo.InvariantCulture),
                character.Defeats.ToString(CultureInfo.InvariantCulture),
                character.Deaths.ToString(CultureInfo.InvariantCulture),
            });
        }
    }

    private void PopulateSupport(TeamUi teamUi, CombatBattleTeamResult result)
    {
        CombatBattleSupportSummary summary = result.SupportSummary;
        teamUi.SupportSummaryText.text = FormatSupportSummary(summary);
        teamUi.SupportDurationText.text = FormatSupportDurationSummary(summary, result.DamagePrevented);
        ClearRows(teamUi.SupportRows);

        for (int i = 0; i < result.Characters.Count; i++)
        {
            CombatBattleCharacterResult character = result.Characters[i];
            RowUi row = CreateSupportRow(
                teamUi.SupportRows,
                "SupportCharacterRow",
                false,
                i % 2 == 0
                    ? new Color(0f, 0f, 0f, 0.12f)
                    : new Color(0f, 0f, 0f, 0.04f));
            SetSupportRowValues(row, new[]
            {
                character.DisplayName,
                FormatEffectNames(character.SupportEffects),
                FormatUsage(character.SupportSummary),
                FormatDuration(character.SupportSummary),
            });
        }
    }

    private static string FormatSupportSummary(CombatBattleSupportSummary summary)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "<color=#8FE3A1>↑バフ {0}回 / {1}人</color>    <color=#C59BFF>↓デバフ {2}回 / {3}人</color>",
            summary.BuffActivationCount,
            summary.BuffTargetCount,
            summary.DebuffActivationCount,
            summary.DebuffTargetCount);
    }

    private static string FormatSupportDurationSummary(
        CombatBattleSupportSummary summary,
        int damagePrevented)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "累積効果時間 <color=#8FE3A1>↑{0:0.0}秒</color> / <color=#C59BFF>↓{1:0.0}秒</color>    無効化D {2}",
            summary.BuffDurationSeconds,
            summary.DebuffDurationSeconds,
            damagePrevented);
    }

    private static string FormatEffectNames(IReadOnlyList<CombatBattleStatusEffectResult> effects)
    {
        if (effects == null || effects.Count == 0) return "—";

        var names = new List<string>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            CombatBattleStatusEffectResult effect = effects[i];
            string color = effect.IsBuff ? "#8FE3A1" : "#C59BFF";
            names.Add(string.Format(
                CultureInfo.InvariantCulture,
                "<color={0}>{1}</color>",
                color,
                effect.Stat));
        }

        return string.Join("・", names);
    }

    private static string FormatUsage(CombatBattleSupportSummary summary)
    {
        string buff = string.Format(
            CultureInfo.InvariantCulture,
            "<color=#8FE3A1>↑{0}回/{1}人</color>",
            summary.BuffActivationCount,
            summary.BuffTargetCount);
        string debuff = string.Format(
            CultureInfo.InvariantCulture,
            "<color=#C59BFF>↓{0}回/{1}人</color>",
            summary.DebuffActivationCount,
            summary.DebuffTargetCount);
        if (summary.BuffActivationCount == 0 && summary.DebuffActivationCount == 0) return "—";
        if (summary.BuffActivationCount == 0) return debuff;
        if (summary.DebuffActivationCount == 0) return buff;
        return buff + " " + debuff;
    }

    private static string FormatDuration(CombatBattleSupportSummary summary)
    {
        string buff = string.Format(
            CultureInfo.InvariantCulture,
            "<color=#8FE3A1>↑{0:0.0}秒</color>",
            summary.BuffDurationSeconds);
        string debuff = string.Format(
            CultureInfo.InvariantCulture,
            "<color=#C59BFF>↓{0:0.0}秒</color>",
            summary.DebuffDurationSeconds);
        if (summary.BuffActivationCount == 0 && summary.DebuffActivationCount == 0) return "—";
        if (summary.BuffActivationCount == 0) return debuff;
        if (summary.DebuffActivationCount == 0) return buff;
        return buff + " " + debuff;
    }

    private static int SumDefeats(CombatBattleTeamResult result)
    {
        int defeats = 0;
        for (int i = 0; i < result.Characters.Count; i++)
        {
            defeats += result.Characters[i].Defeats;
        }

        return defeats;
    }

    private static int SumDeaths(CombatBattleTeamResult result)
    {
        int deaths = 0;
        for (int i = 0; i < result.Characters.Count; i++)
        {
            deaths += result.Characters[i].Deaths;
        }

        return deaths;
    }

    private static void SetRowValues(RowUi row, IReadOnlyList<string> values)
    {
        for (int i = 0; i < row.Cells.Count && i < values.Count; i++)
        {
            row.Cells[i].text = values[i];
        }
    }

    private static void SetSupportRowValues(RowUi row, IReadOnlyList<string> values)
    {
        for (int i = 0; i < row.Cells.Count && i < values.Count; i++)
        {
            row.Cells[i].text = values[i];
        }
    }

    private static void ClearRows(Transform rows)
    {
        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(rows.GetChild(i).gameObject);
        }
    }

    private void MoveChild(string childName, Vector2 anchoredPosition)
    {
        RectTransform child = transform.Find(childName) as RectTransform;
        if (child != null)
        {
            child.anchoredPosition = anchoredPosition;
        }
    }
}
