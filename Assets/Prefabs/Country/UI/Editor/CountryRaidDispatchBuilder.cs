using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;

public static class CountryRaidDispatchBuilder
{
    private const string PaperPath = "Assets/Images/UI/Country/RaidDispatchPaper-v1.png";
    private static readonly Color Ink = new Color32(52, 38, 34, 255);
    private static readonly Color Red = new Color32(122, 40, 36, 255);
    private static readonly Color Pale = new Color32(242, 226, 195, 255);
    private static readonly Color Rule = new Color32(155, 113, 87, 255);

    [MenuItem("WarSim/Country/Set Up Raid Dispatch")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the raid popup.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        const string glyphs = "緊急指令防衛メンバー未設定";
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing dispatch glyphs: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        using (var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryDefense.prefab")) { Apply(scope.prefabContentsRoot); CountryPaletteBuilder.Apply(scope.prefabContentsRoot); }
        Debug.Log("Raid dispatch updated. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        if (paper == null) return;
        var card = root.transform.Find("Window/Shell/Alert/Card");
        var background = card.GetComponent<Image>(); background.sprite = paper; background.color = Color.white;
        card.Find("Workboard").gameObject.SetActive(false);
        var ribbon = TopFill(card, "Ribbon", 32, 150, 64, 64);
        ribbon.GetComponent<Image>().color = Color.clear;
        ribbon.Find("Castle").gameObject.SetActive(false);
        var stamp = Top(ribbon, "UrgentBand", 20, 34, 238, 64); Image(stamp, Red); stamp.SetAsFirstSibling();
        var caption = Top(ribbon, "Caption", 20, 34, 238, 64).GetComponent<TextMeshProUGUI>();
        caption.text = "緊急指令"; caption.color = Pale; caption.fontSize = 34; caption.fontStyle = FontStyles.Bold; caption.alignment = TextAlignmentOptions.Center;
        var title = TopFill(ribbon, "Title", 24, 84, 290, 16).GetComponent<TextMeshProUGUI>();
        title.fontSize = 46; title.color = Ink; title.fontStyle = FontStyles.Bold;
        Image(TopFill(card, "DispatchRule", 182, 2, 84, 84), Rule);
        var body = TopFill(card, "Body", 202, 112, 100, 100).GetComponent<TextMeshProUGUI>();
        body.color = Ink; body.fontSize = 38;
        var info = TopFill(card, "Info", 330, 52, 84, 84);
        info.Find("Surface").gameObject.SetActive(false); info.Find("Rim").gameObject.SetActive(false);
        var oldStrip = info.Find("DispatchStrip"); if (oldStrip != null) oldStrip.gameObject.SetActive(false);
        var heading = Label(Top(info, "DispatchHeading", 20, 0, 300, 52), "防衛メンバー", 30, Red); heading.fontStyle = FontStyles.Bold;
        var summary = TopFill(info, "Text", 0, 52, 330, 24).GetComponent<TextMeshProUGUI>();
        summary.color = Ink; summary.fontSize = 26; summary.alignment = TextAlignmentOptions.MidlineRight;
        var strip = TopFill(card, "RaidMembers", 398, 128, 84, 84);
        var scroll = Add<ScrollRect>(strip.gameObject); scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = ScrollSensitivity;
        var empty = Label(Top(strip, "EmptyMessage", 140, 4, 430, 88), "メンバー未設定", 30, Ink);
        empty.gameObject.SetActive(false);
        var viewport = Fill(strip, "Viewport"); Image(viewport, Color.white).raycastTarget = true; Add<Mask>(viewport.gameObject).showMaskGraphic = false;
        var members = Child(viewport, "Content"); members.anchorMin = members.anchorMax = members.pivot = Vector2.up;
        members.sizeDelta = new Vector2(0, 128); members.anchoredPosition = Vector2.zero;
        Horizontal(members, 12); Add<ContentSizeFitter>(members.gameObject).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = members;
        var track = TopFill(card, "RaidScrollbar", 534, 12, 100, 100); Image(track, new Color32(180, 154, 124, 160)).raycastTarget = true;
        var sliding = Fill(track, "Sliding", new Vector2(2, 2), new Vector2(-2, -2));
        var handle = Fill(sliding, "Handle"); var handleImage = Image(handle, Red); handleImage.raycastTarget = true;
        var scrollbar = Add<Scrollbar>(track.gameObject); scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scroll.horizontalScrollbar = scrollbar; scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        Set(root.GetComponent<CountryDefensePanel>(), ("_raidMembers", members), ("_raidEmptyMessage", empty.gameObject));
        var actions = BottomFill(card, "Actions", 160); actions.anchoredPosition = new Vector2(0, 40);
        var skip = actions.Find("Skip"); if (skip != null) UnityEngine.Object.DestroyImmediate(skip.gameObject);
        Top(actions, "Edit", 150, 8, 540, 112);
        Right(actions, "Fight", 150, 8, 540, 112);
        StyleButton(actions.Find("Edit").GetComponent<Button>(), new Color32(220, 203, 175, 255), Ink, Rule);
        StyleButton(actions.Find("Fight").GetComponent<Button>(), Red, Pale, new Color32(177, 91, 71, 255));
    }

    private static void StyleButton(Button button, Color face, Color text, Color border)
    {
        CountryGovernmentTheme.Colors(button, face);
        var colors = button.colors; colors.disabledColor = CountryGovernmentTheme.DisabledFace; button.colors = colors;
        button.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = text;
        button.transform.Find("Focus").GetComponent<Graphic>().color = face == Red ? Pale : Red;
        var outline = button.transform.Find("Outline");
        if (outline != null) outline.GetComponent<Graphic>().color = border;
        var frame = button.transform.Find("Frame");
        if (frame != null) frame.GetComponent<Graphic>().color = border;
        var shadow = button.transform.Find("Shadow");
        if (shadow != null) shadow.GetComponent<Graphic>().color = new Color32(82, 30, 29, 255);
    }
}
