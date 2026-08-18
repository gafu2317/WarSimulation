using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatBattleResultView : MonoBehaviour
{
    private const string SummaryRootName = "BattleSummary";
    private const float CardWidth = 560f;
    private const float CardHeight = 440f;
    private const float NameColumnWidth = 170f;
    private const float WeaponColumnWidth = 70f;
    private const float MetricColumnWidth = 54f;

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
        public TMP_Text DefenseText { get; set; }
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
        _allyUi = CreateTeamUi("AlliesCard", "味方", new Vector2(-290f, 0f), new Color(0.08f, 0.16f, 0.26f, 0.96f));
        _enemyUi = CreateTeamUi("EnemiesCard", "敵", new Vector2(290f, 0f), new Color(0.26f, 0.12f, 0.12f, 0.96f));

        MoveChild("ResultTitle", new Vector2(0f, 285f));
        MoveChild("BackToSelectionButton", new Vector2(0f, -300f));
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
        root.sizeDelta = new Vector2(1140f, 520f);

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

        GameObject rowsObject = new GameObject(
            "Rows",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        teamUi.Rows = rowsObject.GetComponent<RectTransform>();
        teamUi.Rows.SetParent(card, false);
        LayoutElement rowsElement = rowsObject.GetComponent<LayoutElement>();
        rowsElement.flexibleHeight = 1f;
        VerticalLayoutGroup rowsLayout = rowsObject.GetComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 3f;
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        teamUi.TotalRow = CreateRow(card, "TotalRow", isHeader: false, rowColor: new Color(0f, 0f, 0f, 0.26f));
        teamUi.DefenseText = CreateLabel(
            card,
            "DefenseTotal",
            "無効化ダメージ 0",
            16f,
            TextAlignmentOptions.Right,
            24f,
            new Color(1f, 1f, 1f, 0.82f));
        return teamUi;
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
        string[] labels = { "名前", "武器", "キャラ\n与ダメ", "魔石\n与ダメ", "被ダメ", "回復", "撃破" };
        for (int i = 0; i < labels.Length; i++)
        {
            TextAlignmentOptions alignment = i == 0 || isHeader
                ? (i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center)
                : TextAlignmentOptions.Right;
            rowUi.Cells.Add(CreateCell(
                row,
                labels[i],
                alignment,
                isHeader,
                i == 0,
                i == 1));
        }

        return rowUi;
    }

    private TMP_Text CreateCell(
        Transform parent,
        string textValue,
        TextAlignmentOptions alignment,
        bool isHeader,
        bool isName,
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
        text.overflowMode = isName ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
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
            "武器",
            "キャラ\n与ダメ",
            "魔石\n与ダメ",
            "被ダメ",
            "回復",
            "撃破",
        });
        SetRowValues(teamUi.TotalRow, new[]
        {
            "合計",
            string.Empty,
            result.DamageDealt.ToString(CultureInfo.InvariantCulture),
            result.MagicStoneDamage.ToString(CultureInfo.InvariantCulture),
            result.DamageTaken.ToString(CultureInfo.InvariantCulture),
            result.HealingDone.ToString(CultureInfo.InvariantCulture),
            SumDefeats(result).ToString(CultureInfo.InvariantCulture),
        });
        teamUi.DefenseText.text = "無効化ダメージ " + result.DamagePrevented.ToString(CultureInfo.InvariantCulture);
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
                character.WeaponDisplayName,
                character.DamageDealt.ToString(CultureInfo.InvariantCulture),
                character.MagicStoneDamage.ToString(CultureInfo.InvariantCulture),
                character.DamageTaken.ToString(CultureInfo.InvariantCulture),
                character.HealingDone.ToString(CultureInfo.InvariantCulture),
                character.Defeats.ToString(CultureInfo.InvariantCulture),
            });
        }
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

    private static void SetRowValues(RowUi row, IReadOnlyList<string> values)
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
