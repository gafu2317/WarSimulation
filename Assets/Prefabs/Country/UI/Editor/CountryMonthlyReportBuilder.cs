using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;

public static class CountryMonthlyReportBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string PaperPath = "Assets/Images/UI/Country/ReportPaper-v1.png";
    private static readonly Color Ink = CountryGovernmentTheme.Ink, Secondary = CountryGovernmentTheme.PaperMuted;
    private static readonly Color Line = new Color32(172, 161, 137, 255), Accent = CountryGovernmentTheme.Ink;

    [MenuItem("WarSim/Country/Set Up Monthly Report")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the report prefab.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
        importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        const string glyphs = "報告書資源の収入項目受取額現在の合計量国庫税収";
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing report glyphs: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        SavePrefab("CountryExpeditionReportRow", BuildRow);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryGovernment.prefab")) { Apply(scope.prefabContentsRoot); CountryPaletteBuilder.Apply(scope.prefabContentsRoot); }
        Debug.Log("Monthly report updated. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var page = root.transform.Find("Window/Shell/Main/Income");
        if (page == null) return;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperPath);
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryExpeditionReportRow.prefab");
        if (texture == null || rowPrefab == null) return;
        var paper = Fill(page, "ReportPaper", new Vector2(40, 16), new Vector2(-40, -16));
        paper.SetAsFirstSibling();
        var surface = Add<CountryReportPaper>(paper.gameObject); surface.color = Color.white; surface.raycastTarget = false; surface.Configure(texture, 48);
        var fold = Right(paper, "Fold", 0, 0, 48, 48);
        var foldGraphic = Add<CountryGovernmentFold>(fold.gameObject); foldGraphic.color = new Color32(183, 173, 150, 255); foldGraphic.raycastTarget = false;
        var title = TopFill(page, "Title", 38, 72, 96, 640).GetComponent<TextMeshProUGUI>();
        title.text = "報告書"; title.color = Ink; title.fontSize = 44; title.fontStyle = FontStyles.Bold;
        var state = Right(page, "State", 96, 48, 540, 56).GetComponent<TextMeshProUGUI>();
        state.color = Secondary; state.fontSize = 30; state.alignment = TextAlignmentOptions.MidlineRight;
        Image(TopFill(page, "HeaderRule", 120, 2, 96, 96), Line).raycastTarget = false;
        var caption = Label(TopFill(page, "ResourceHeading", 128, 48, 96, 96), "資源の収入", 32, Accent);
        caption.fontStyle = FontStyles.Bold;
        var headings = TopFill(page, "IncomeColumns", 180, 48, 96, 96);
        var itemHeading = Label(ColumnCell(headings, "Item", 0, .33f, 8, 0), "項目", 28, Secondary);
        var gainHeading = Label(ColumnCell(headings, "Amount", .33f, .56f, 0, 16), "受取額", 28, Secondary);
        gainHeading.alignment = TextAlignmentOptions.MidlineRight;
        var noteHeading = Label(ColumnCell(headings, "Note", .60f, 1, 8, 8), "現在の合計量", 28, Secondary);
        noteHeading.alignment = TextAlignmentOptions.MidlineRight;
        itemHeading.fontStyle = gainHeading.fontStyle = noteHeading.fontStyle = FontStyles.Bold;
        Image(BottomFill(headings, "Rule", 2), Line).raycastTarget = false;
        var resources = TopFill(page, "Resources", 230, 192, 96, 96);
        var horizontal = resources.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) UnityEngine.Object.DestroyImmediate(horizontal);
        var vertical = Add<VerticalLayoutGroup>(resources.gameObject);
        vertical.spacing = 0; vertical.childControlWidth = vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
        foreach (Transform item in resources)
        {
            Layout((RectTransform)item, 64);
            item.Find("Surface").gameObject.SetActive(false); item.Find("Accent").gameObject.SetActive(false);
            Top(item, "Icon", 8, 6, 52, 52).gameObject.SetActive(true);
            var name = ColumnCell(item, "Caption", 0, .33f, 72, 8).GetComponent<TextMeshProUGUI>();
            name.fontSize = 32; name.color = Ink; if (item.name == "Tax") name.text = "税収";
            var gain = ColumnCell(item, "Gain", .33f, .56f, 0, 16).GetComponent<TextMeshProUGUI>();
            gain.fontSize = 36; gain.color = Ink; gain.alignment = TextAlignmentOptions.MidlineRight;
            var stock = ColumnCell(item, "Stock", .60f, 1, 8, 8).GetComponent<TextMeshProUGUI>();
            stock.fontSize = 32; stock.color = Ink; stock.alignment = TextAlignmentOptions.MidlineRight;
            var divider = BottomFill(item, "Divider", 1); divider.gameObject.SetActive(true);
            Image(divider, Line).raycastTarget = false;
        }
        var returnTitle = TopFill(page, "ReturnTitle", 442, 56, 96, 96).GetComponent<TextMeshProUGUI>();
        returnTitle.color = Accent; returnTitle.fontStyle = FontStyles.Bold;
        Image(TopFill(page, "ReturnRule", 502, 2, 96, 96), Line).raycastTarget = false;
        Fill(page, "Returns", new Vector2(96, 48), new Vector2(-96, -514));
        var empty = TopFill(page, "NoReturns", 522, 72, 112, 112).GetComponent<TextMeshProUGUI>(); empty.color = Secondary;
        Set(page.GetComponent<CountryExpeditionReportView>(), ("_rowPrefab", rowPrefab.GetComponent<CountryExpeditionRow>()));
        Set(root.GetComponent<CountryGovernmentPanel>(), ("_taxStock", resources.Find("Tax/Stock").GetComponent<TextMeshProUGUI>()));
    }

    private static void BuildRow(GameObject root)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1200, 144); Layout(rect, 144);
        var button = Button(rect, "", Color.clear);
        var colors = button.colors;
        colors.normalColor = colors.selectedColor = Color.clear;
        colors.highlightedColor = new Color(.38f, .32f, .24f, .12f);
        colors.pressedColor = new Color(.38f, .32f, .24f, .24f);
        button.colors = colors;
        root.transform.Find("Label").gameObject.SetActive(false); root.transform.Find("Outline").gameObject.SetActive(false);
        var data = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        var castle = Image(Top(rect, "Castle", 16, 28, 88, 88), Color.white); castle.sprite = data.Castle; castle.preserveAspect = true; castle.raycastTarget = false;
        var name = Label(TopFill(rect, "Name", 8, 64, 128, 320), "", 36, Ink);
        var detail = Label(TopFill(rect, "Detail", 74, 48, 128, 320), "", 32, Secondary);
        var status = Label(Right(rect, "Status", 24, 24, 280, 88), "", 30, CountryGovernmentTheme.PositiveInk); status.alignment = TextAlignmentOptions.MidlineRight;
        Image(BottomFill(rect, "Divider", 1), Line).raycastTarget = false;
        Set(Add<CountryExpeditionRow>(root), ("_button", button), ("_name", name), ("_detail", detail), ("_status", status));
    }

    private static RectTransform ColumnCell(Transform parent, string name, float min, float max, float left, float right)
    {
        var rect = Fill(parent, name);
        rect.anchorMin = new Vector2(min, 0); rect.anchorMax = new Vector2(max, 1);
        rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-right, 0);
        return rect;
    }
}
