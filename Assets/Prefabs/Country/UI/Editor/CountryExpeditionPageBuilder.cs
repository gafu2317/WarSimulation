using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;
using Object = UnityEngine.Object;

public static class CountryExpeditionPageBuilder
{
    private const int PageInset = 32;
    private const int PageGap = 16;

    [MenuItem("WarSim/Country/Set Up Expedition Page")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the expedition page.");
        using var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryExpedition.prefab");
        Apply(scope.prefabContentsRoot);
        Debug.Log("Expedition destination and formation pages updated. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var shell = root.transform.Find("Window/Shell");
        var header = BottomFill(shell, "Header", Theme.PageBarHeight);
        foreach (string name in new[] { "Map", "Formation" })
        {
            var tab = header.Find(name);
            if (tab != null) Object.DestroyImmediate(tab.gameObject);
        }
        var title = header.Find("Title");
        if (title != null) Object.DestroyImmediate(title.gameObject);
        var main = Fill(shell, "Main", new Vector2(0, Theme.PageBarHeight + PageGap), new Vector2(0, -PageGap));
        var map = (RectTransform)(shell.Find("Map") ?? main.Find("Map") ?? main.Find("Viewport/Content/Map"));
        map.SetParent(shell, false); map.SetAsFirstSibling();
        Stretch(map);
        map.offsetMin = new Vector2(PageInset, main.offsetMin.y); map.offsetMax = new Vector2(-PageInset, main.offsetMax.y);
        var mapArea = Fill(map, "MapArea");
        var itinerary = Fill(map, "Details");
        mapArea.anchorMax = new Vector2(.70f, 1); mapArea.offsetMax = new Vector2(-PageGap / 2, 0);
        itinerary.anchorMin = new Vector2(.70f, 0); itinerary.offsetMin = new Vector2(PageGap / 2, 0);
        if (map.TryGetComponent(out UnityEngine.UI.LayoutElement oldMapSize)) Object.DestroyImmediate(oldMapSize);
        bool hadPageScroll = main.TryGetComponent(out ScrollRect oldPage);
        if (hadPageScroll)
        {
            var oldFormation = oldPage.content.Find("Formation");
            oldFormation.SetParent(main, false);
            Object.DestroyImmediate(oldFormation.GetComponent<LayoutElement>());
            foreach (var relay in oldFormation.GetComponentsInChildren<CountryNestedScrollInput>(true))
                Object.DestroyImmediate(relay);
            Object.DestroyImmediate(oldPage.viewport.gameObject);
            Object.DestroyImmediate(oldPage.verticalScrollbar.gameObject);
            Object.DestroyImmediate(oldPage);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(main.gameObject);
        }
        var formation = Fill(main, "Formation");
        CountryRosterSelectionBuilder.SetPageInsets(formation);
        header.SetAsLastSibling();
        map.gameObject.SetActive(true); main.gameObject.SetActive(false);
        var selection = (RectTransform)formation.Find("Selection");
        Stretch(selection);
        var fixedMembers = main.Find("FixedMembers");
        if (hadPageScroll || fixedMembers != null)
        {
            foreach (string path in new[] { "Body", "Body/Roster/Right", "Body/Roster/Right/Name",
                         "Body/Roster/Right/Subtitle", "Body/Roster/Right/Body",
                         "Body/Roster/Right/Body/Portrait", "Body/Roster/Right/Body/Details" })
                PrefabUtility.RevertObjectOverride(selection.Find(path), InteractionMode.AutomatedAction);
            foreach (string path in new[] { "Body/Roster/Left/List", "Body/Roster/Right/Body/Details" })
            {
                var scroll = selection.Find(path).GetComponent<ScrollRect>();
                PrefabUtility.RevertObjectOverride(scroll, InteractionMode.AutomatedAction);
                PrefabUtility.RevertObjectOverride(scroll.verticalScrollbar.gameObject, InteractionMode.AutomatedAction);
            }
        }
        var context = fixedMembers?.Find("Context") ?? formation.Find("Header/Context") ?? header.Find("Context") ?? shell.Find("Context");
        if (context != null) context.SetParent(selection, false);
        if (fixedMembers != null) Object.DestroyImmediate(fixedMembers.gameObject);
        var oldHeader = formation.Find("Header");
        if (oldHeader != null)
        {
            var equipment = oldHeader.Find("Equipment");
            if (equipment != null) equipment.SetParent(formation, false);
            Object.DestroyImmediate(oldHeader.gameObject);
        }
        var contextLabel = Label(TopFill(selection, "Context", 0, 56, 480, 16), "", 28, Theme.Text);
        contextLabel.alignment = TextAlignmentOptions.MidlineRight;
        contextLabel.overflowMode = TextOverflowModes.Ellipsis;
        contextLabel.gameObject.SetActive(true);
        var panel = root.GetComponent<CountryExpeditionPanel>();
        Set(panel, ("_destinationPage", map.gameObject), ("_formationPage", main.gameObject), ("_context", contextLabel));
        ApplyControls(panel, header, shell.Find("Footer"));
        CountryExpeditionDetailsBuilder.Apply(root);
    }

    private static void ApplyControls(CountryExpeditionPanel panel, RectTransform header, Transform oldFooter)
    {
        const float footerHeight = Theme.PageBarHeight + Theme.PageBarNoticeHeight;
        header.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, footerHeight);
        foreach (string page in new[] { "Map", "Main" })
        {
            var rect = (RectTransform)header.parent.Find(page);
            rect.offsetMin = new Vector2(rect.offsetMin.x, footerHeight + PageGap);
        }
        header.GetComponent<Image>().enabled = false;
        header.Find("Date").gameObject.SetActive(false);
        header.Find("Active").gameObject.SetActive(false);

        var active = header.Find("Active").GetComponent<Button>();
        var book = Part(CountryCommandMotifsBuilder.Atlas, "Command-BookCover");
        var helmet = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/soldiers-v1.png");
        Artwork(active, book);
        Icon(active.transform, helmet, 22, 28, 52, 52);
        var activeLabel = active.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        Fill(active.transform, "Label", new Vector2(84, 12), new Vector2(-18, -12));
        activeLabel.fontSize = 30; activeLabel.color = Theme.Text;
        CountryButtonFeedbackBuilder.Configure(active);

        if (oldFooter != null)
        {
            oldFooter.Find("Back").SetParent(header, false);
            oldFooter.Find("Depart").SetParent(header, false);
            Object.DestroyImmediate(oldFooter.gameObject);
        }
        const int backWidth = 216;
        var back = PageButton(PageItem(header, "Back", 0, PageInset, backWidth), Theme.CountryReturnText);
        var next = PageButton(PageItem(header, "Next", 1, -PageInset, Theme.PageBarActionWidth), "編成へ ›");
        var depart = PageButton(PageItem(header, "Depart", 1, -PageInset, Theme.PageBarActionWidth), "出発確認 ›");
        Artwork(depart, Part(CountryCommandMotifsBuilder.TrainingAtlas, "Training-Pennant"));
        var flag = Part("Assets/Images/UI/Country/Buttons/ExpeditionParts-v1.png", "Expedition-Flag");
        Icon(depart.transform, flag, 26, 13, 54, 62);
        Fill(depart.transform, "Label", new Vector2(88, 8), new Vector2(-42, -8));
        CountryButtonFeedbackBuilder.Configure(depart);

        Label(PageItem(header, "ExpeditionTitle", 0, PageInset + backWidth + 20, 168), "遠征計画", 32, Theme.Text);
        foreach (string name in new[] { "DestinationStep", "MemberStep", "StepArrow" })
            if (header.Find(name) is Transform current) Object.DestroyImmediate(current.gameObject);
        var target = PageStep(PageItem(header, "DestinationStep", .5f, -100, 176), "領地選択");
        PageStep(PageItem(header, "StepArrow", .5f, 0, 44), "→");
        var members = PageStep(PageItem(header, "MemberStep", .5f, 100, 176), "編成");
        var notice = Child(header, "DepartureStatus");
        Theme.PlacePageItem(notice, 1, -PageInset, 620, Theme.PageBarHeight + Theme.PageBarNoticeHeight / 2, Theme.PageBarNoticeHeight);
        var status = Label(notice, "", 24, Theme.Text);
        status.alignment = TextAlignmentOptions.MidlineRight;
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.textWrappingMode = TextWrappingModes.NoWrap;
        Set(panel, ("_back", back), ("_next", next), ("_depart", depart), ("_targetStep", target), ("_memberStep", members),
            ("_departureStatus", status));
    }

    private static RectTransform PageItem(Transform parent, string name, float anchor, float x, float width)
    {
        var rect = Child(parent, name);
        Theme.PlacePageItem(rect, anchor, x, width);
        return rect;
    }

    private static TextMeshProUGUI PageStep(RectTransform rect, string text)
    {
        var label = Label(rect, text, 30, Theme.Muted);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Sprite Part(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First(sprite => sprite.name == name);

    private static void Artwork(Button button, Sprite sprite)
    {
        var root = button.transform;
        if (root.Find("Outline") is Transform outline) outline.gameObject.SetActive(false);
        if (root.Find("Frame") is Transform frame) frame.gameObject.SetActive(false);
        var shadow = Picture(Fill(root, "Shadow", new Vector2(-6, -8), new Vector2(6, 4)), sprite);
        shadow.color = new Color(.08f, .10f, .14f, .8f); shadow.transform.SetAsFirstSibling();
        var focus = Picture(Fill(root, "Focus"), sprite);
        focus.color = Theme.Accent; focus.enabled = false; focus.transform.SetSiblingIndex(1);
        var face = Picture(Fill(root, "Surface", Vector2.one * 4, Vector2.one * -4), sprite);
        face.transform.SetSiblingIndex(2); button.targetGraphic = face;
        Theme.Colors(button, Color.white);
    }

    private static void Icon(Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        var icon = Image(Top(parent, "Icon", x, y, width, height), Color.white);
        icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
    }
}
