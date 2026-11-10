using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;
using Object = UnityEngine.Object;

public static class CountryExpeditionPageBuilder
{
    [MenuItem("WarSim/Country/Set Up Expedition Page")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the expedition page.");
        using var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryExpedition.prefab");
        Apply(scope.prefabContentsRoot);
        Debug.Log("Expedition map and formation connected in one scrolling page. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var shell = root.transform.Find("Window/Shell");
        var header = shell.Find("Header");
        foreach (string name in new[] { "Map", "Formation" })
        {
            var tab = header.Find(name);
            if (tab != null) Object.DestroyImmediate(tab.gameObject);
        }
        var title = header.Find("Title");
        if (title != null) Object.DestroyImmediate(title.gameObject);
        var context = shell.Find("Context") ?? header.Find("Context");
        context.SetParent(header, false);
        var contextRect = (RectTransform)context;
        contextRect.anchorMin = Vector2.zero; contextRect.anchorMax = Vector2.one;
        contextRect.offsetMin = new Vector2(280, 0); contextRect.offsetMax = new Vector2(-400, 0);
        context.GetComponent<TextMeshProUGUI>().fontSize = 32;
        var main = (RectTransform)shell.Find("Main");
        main.offsetMin = new Vector2(40, 0); main.offsetMax = new Vector2(-40, 0);
        var map = main.Find("Map") ?? main.Find("Viewport/Content/Map");
        var formation = main.Find("Formation") ?? main.Find("Viewport/Content/Formation");
        var page = Scroll(main);
        page.viewport.offsetMax = Vector2.zero;
        page.scrollSensitivity = 54;
        page.inertia = true;
        var bar = (RectTransform)page.verticalScrollbar.transform;
        bar.anchoredPosition = new Vector2(28, 0);
        bar.offsetMin = new Vector2(bar.offsetMin.x, 24);
        bar.offsetMax = new Vector2(bar.offsetMax.x, -24);
        var layout = page.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 72; layout.padding.top = 128; layout.padding.bottom = 144;
        header.SetAsLastSibling(); shell.Find("Footer").SetAsLastSibling();
        map.SetParent(page.content, false); formation.SetParent(page.content, false);
        map.SetSiblingIndex(0); formation.SetSiblingIndex(1);
        var mapSize = Add<UnityEngine.UI.LayoutElement>(map.gameObject);
        var formationSize = Add<UnityEngine.UI.LayoutElement>(formation.gameObject);
        mapSize.minHeight = mapSize.preferredHeight = 736;
        formationSize.minHeight = formationSize.preferredHeight = 808;
        map.gameObject.SetActive(true); formation.gameObject.SetActive(true);
        var list = formation.Find("Selection/Body/Roster/Left/List").GetComponent<UnityEngine.UI.ScrollRect>();
        var right = (RectTransform)formation.Find("Selection/Body/Roster/Right");
        var details = right.Find("Body/Details").GetComponent<UnityEngine.UI.ScrollRect>();
        right.anchorMin = new Vector2(right.anchorMin.x, 1);
        right.anchorMax = new Vector2(right.anchorMax.x, 1);
        right.pivot = new Vector2(right.pivot.x, 1);
        right.anchoredPosition = new Vector2(right.anchoredPosition.x, 0);
        right.sizeDelta = new Vector2(right.sizeDelta.x, 600);
        var body = (RectTransform)right.Find("Body");
        body.offsetMax = new Vector2(body.offsetMax.x, 0);
        var portrait = (RectTransform)right.Find("Body/Portrait");
        portrait.anchorMin = portrait.anchorMax = new Vector2(0, 1);
        portrait.pivot = new Vector2(0, 1);
        portrait.anchoredPosition = Vector2.zero;
        portrait.sizeDelta = new Vector2(340, 680);
        var detailsRect = (RectTransform)details.transform;
        detailsRect.anchorMin = new Vector2(0, detailsRect.anchorMin.y);
        detailsRect.offsetMin = new Vector2(356, detailsRect.offsetMin.y);
        detailsRect.offsetMax = new Vector2(detailsRect.offsetMax.x, -128);
        var nameRect = (RectTransform)right.Find("Name");
        nameRect.offsetMin = new Vector2(356, nameRect.offsetMin.y);
        nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -28);
        var subtitleRect = (RectTransform)right.Find("Subtitle");
        subtitleRect.offsetMin = new Vector2(356, subtitleRect.offsetMin.y);
        right.Find("Name").GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
        right.Find("Subtitle").GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
        list.enabled = false;
        if (list.verticalScrollbar != null) list.verticalScrollbar.gameObject.SetActive(false);
        details.enabled = false;
        if (details.verticalScrollbar != null) details.verticalScrollbar.gameObject.SetActive(false);
        Set(Add<CountryExpeditionPageLayout>(main.gameObject), ("_viewport", page.viewport), ("_map", mapSize), ("_formation", formationSize), ("_list", list), ("_details", details), ("_right", right));
        Set(root.GetComponent<CountryExpeditionPanel>(), ("_page", page));
        foreach (var inner in formation.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
        {
            var relay = Add<CountryNestedScrollInput>(inner.viewport.gameObject);
            Set(relay, ("_inner", inner), ("_outer", page));
            relay.enabled = inner != list && inner != details;
        }
        foreach (var value in new Component[] { main, page, page.viewport, bar, page.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() })
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        ApplyControls(header, shell.Find("Footer"));
    }

    private static void ApplyControls(Transform header, Transform footer)
    {
        header.GetComponent<UnityEngine.UI.Image>().enabled = false;
        footer.GetComponent<UnityEngine.UI.Image>().enabled = false;
        header.Find("Date").gameObject.SetActive(false);
        header.Find("Context").gameObject.SetActive(false);
        header.Find("Active").gameObject.SetActive(false);

        var active = header.Find("Active").GetComponent<UnityEngine.UI.Button>();
        var back = footer.Find("Back").GetComponent<UnityEngine.UI.Button>();
        var depart = footer.Find("Depart").GetComponent<UnityEngine.UI.Button>();
        var book = Part(CountryCommandMotifsBuilder.Atlas, "Command-BookCover");
        var sign = Part(CountryConstructionSignBuilder.Atlas, "Construction-Planks");
        var pennant = Part(CountryCommandMotifsBuilder.TrainingAtlas, "Training-Pennant");
        var flag = Part("Assets/Images/UI/Country/Buttons/ExpeditionParts-v1.png", "Expedition-Flag");
        var helmet = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/soldiers-v1.png");

        Artwork(active, book);
        Icon(active.transform, helmet, 22, 28, 52, 52);
        var activeLabel = active.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        Fill(active.transform, "Label", new Vector2(84, 12), new Vector2(-18, -12));
        activeLabel.fontSize = 30; activeLabel.color = Theme.Text;
        CountryButtonFeedbackBuilder.Configure(active);

        Top(footer, "Back", 32, 0, 320, 128);
        Artwork(back, sign);
        var backLabel = back.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        backLabel.fontSize = 30; backLabel.color = Theme.Text;
        CountryButtonFeedbackBuilder.Configure(back);

        Right(footer, "Depart", 32, 32, 352, 96);
        Artwork(depart, pennant);
        Icon(depart.transform, flag, 26, 17, 54, 62);
        var departLabel = depart.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        Fill(depart.transform, "Label", new Vector2(88, 8), new Vector2(-42, -8));
        departLabel.fontSize = 36;
        CountryButtonFeedbackBuilder.Configure(depart);

        var summary = Right(footer, "Summary", 32, 0, 720, 30).GetComponent<TextMeshProUGUI>();
        summary.fontSize = 26; summary.alignment = TextAlignmentOptions.MidlineRight;
        summary.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static Sprite Part(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First(sprite => sprite.name == name);

    private static void Artwork(UnityEngine.UI.Button button, Sprite sprite)
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

    private static UnityEngine.UI.Image Picture(RectTransform rect, Sprite sprite)
    {
        foreach (var graphic in rect.GetComponents<UnityEngine.UI.Graphic>().Where(g => g is not UnityEngine.UI.Image).ToArray())
            Object.DestroyImmediate(graphic);
        var image = Image(rect, Color.white); image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4; image.raycastTarget = false;
        return image;
    }

    private static void Icon(Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        var icon = Image(Top(parent, "Icon", x, y, width, height), Color.white);
        icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
    }
}
