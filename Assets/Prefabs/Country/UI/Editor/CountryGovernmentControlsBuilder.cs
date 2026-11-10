using TMPro;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountryGovernmentControlsBuilder
{
    internal const string TabArt = "Assets/Images/UI/Country/Controls/GameTab-v1.png";
    internal const string SchoolArt = "Assets/Images/UI/Country/Controls/SchoolEmblems-v1.png";

    public static void Government(GameObject root)
    {
        var shell = root.transform.Find("Window/Shell");
        foreach (Transform tab in shell.Find("Header/Navigation"))
        {
            TabArtwork(tab.GetComponent<UnityEngine.UI.Button>());
            var icon = Top(tab, "Icon", 14, 28, 56, 56).GetComponent<UnityEngine.UI.Image>();
            if (tab.name == "Income") icon.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/UI/Country/Controls/AllocationCoin-v1.png").OfType<Sprite>().Single();
            var label = tab.Find("TabLabel").GetComponent<TextMeshProUGUI>(); label.fontSize = 36;
            label.rectTransform.offsetMin = new Vector2(84, 0); label.rectTransform.offsetMax = new Vector2(-12, 0);
            tab.Find("Label").gameObject.SetActive(false);
        }
        foreach (string mode in new[] { "Upgrade", "New" }) TabArtwork(shell.Find("Main/Construction/Modes/" + mode).GetComponent<UnityEngine.UI.Button>());
        FacilityIllustrations(root);
        var bar = shell.Find("Main/Budget/Summary");
        var taxCaption = Label(Top(bar, "TaxCaption", 104, 4, 340, 44), "1月の税収", 26, Theme.Muted);
        Label(Top(bar, "TotalCaption", 480, 4, 280, 44), "施設へ配分", 26, Theme.Muted);
        var flowCaption = Label(Top(bar, "FlowCaption", 800, 4, 360, 44), "国庫へ", 26, Theme.Muted);
        Readout(bar, "Tax", 104, 340, Theme.Text);
        Readout(bar, "Total", 480, 280, Theme.Text);
        Readout(bar, "Remainder", 800, 360, Theme.Text);
        var taxIcon = (RectTransform)bar.Find("TaxIcon"); taxIcon.sizeDelta = new Vector2(64, 64); taxIcon.anchoredPosition = new Vector2(24, -24);
        TreasurySwitch(bar.Find("UseTreasury"));
        var confirm = shell.Find("Footer/Confirm").GetComponent<UnityEngine.UI.Button>();
        ((RectTransform)confirm.transform).sizeDelta = new Vector2(440, 112);
        Command(confirm, MenuIcon("budget-v1"));
        ((RectTransform)shell.Find("Footer/Status")).offsetMax = new Vector2(-504, -8);
        var panel = root.GetComponent<CountryGovernmentPanel>();
        Set(panel, ("_taxBudgetCaption", taxCaption), ("_cashFlowCaption", flowCaption), ("_confirmIcon", confirm.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>()));
        SetArray(panel, "_confirmIcons", new[] { MenuIcon("budget-v1"), MenuIcon("budget-v1"), MenuIcon("construction-v1"), MenuIcon("training-v1") });
        CountryConstructionSignBuilder.Apply(root);
        CountryCommandMotifsBuilder.ApplyGovernment(root);
    }

    public static void Allocation(GameObject root)
    {
        Slider(root.transform.Find("Funding/Slider"));
        FacilityIllustrations(root);
        Top(root.transform.Find("Facility"), "Symbol", 0, 20, 88, 88);
        foreach (string name in new[] { "Name", "Minimum" })
        {
            var rect = (RectTransform)root.transform.Find("Facility/" + name); rect.offsetMin = new Vector2(108, rect.offsetMin.y);
        }
        var details = root.transform.Find("Effect/Details");
        foreach (string name in new[] { "Caption", "EffectText" })
            ((RectTransform)details.Find(name)).offsetMax = new Vector2(-136, ((RectTransform)details.Find(name)).offsetMax.y);
        var more = Label(Right(details, "More", 16, 46, 104, 52), "詳細 ›", 28, Theme.Text);
        more.alignment = TextAlignmentOptions.Center;
    }

    public static void Roster(GameObject root)
    {
        var action = root.transform.Find("Right/Actions/Action").GetComponent<UnityEngine.UI.Button>();
        var equipment = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/sword.png");
        Command(action, equipment);
        var view = root.GetComponent<CountryRosterView>();
        Set(view, ("_actionIcon", action.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>()),
            ("_equipmentIcon", equipment), ("_memberIcon", MenuIcon("soldiers-v1")), ("_trainingIcon", MenuIcon("training-v1")));
    }

    public static void Note(GameObject root)
    {
        var face = root.transform.Find("Surface").GetComponent<CountryGovernmentPlate>();
        face.SetTexture(null); face.Configure(0, 0, 0); face.color = Theme.Panel;
        var edge = root.transform.Find("Edge").GetComponent<CountryGovernmentPlate>();
        edge.Configure(0, 2.5f, 0); edge.color = Theme.Border;
        var shadow = root.transform.Find("Shadow").GetComponent<CountryGovernmentPlate>(); shadow.Configure(0, 0, 0);
        root.transform.Find("Fold").GetComponent<CountryGovernmentFold>().color = Theme.Muted;
    }

    public static void Slider(Transform root)
    {
        var handle = root.Find("HandleArea/Handle");
        ((RectTransform)handle).sizeDelta = new Vector2(28, 28);
        ((RectTransform)handle).pivot = new Vector2(.5f, .5f);
        var plate = handle.GetComponent<CountryGovernmentPlate>();
        if (plate != null) UnityEngine.Object.DestroyImmediate(plate);
        foreach (Transform child in handle) child.gameObject.SetActive(false);
        var image = Image((RectTransform)handle, Color.white);
        image.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/UI/Country/Controls/AllocationCoin-v1.png").OfType<Sprite>().Single(sprite => sprite.name == "Allocation-Coin");
        image.preserveAspect = true; image.raycastTarget = true;
        ((RectTransform)handle.parent).sizeDelta = new Vector2(-28, 0);
        foreach (string name in new[] { "Track", "FillArea" })
        {
            var area = (RectTransform)root.Find(name);
            area.offsetMin = new Vector2(14, -3); area.offsetMax = new Vector2(-14, 3);
        }
        var slider = root.GetComponent<UnityEngine.UI.Slider>(); slider.targetGraphic = image;
        Theme.Colors(slider, Color.white);
        var scaleRect = Fill(root, "Scale"); scaleRect.anchorMin = new Vector2(0, .5f); scaleRect.anchorMax = new Vector2(1, .5f);
        scaleRect.sizeDelta = new Vector2(-28, 112); scaleRect.anchoredPosition = Vector2.zero;
        var scale = Add<CountrySliderScale>(scaleRect.gameObject); scale.raycastTarget = false;
        var labels = new TextMeshProUGUI[6];
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = Child(scaleRect, "ScaleLabel" + i); rect.sizeDelta = new Vector2(128, 36);
            labels[i] = Label(rect, "", 24, Theme.Muted); labels[i].raycastTarget = false;
        }
        Set(scale, ("_slider", slider)); SetArray(scale, "_labels", labels);
    }

    public static void Training(GameObject root)
    {
        var icons = SchoolIcons();
        for (int i = 0; i < icons.Length; i++)
        {
            var button = root.transform.Find("Schools/School" + (i + 1)).GetComponent<UnityEngine.UI.Button>(); TabArtwork(button);
            var icon = Image(Top(button.transform, "Icon", 16, 24, 64, 64), Color.white); icon.sprite = icons[i]; icon.preserveAspect = true;
            var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>(); label.fontSize = 32;
            label.rectTransform.offsetMin = new Vector2(88, 8); label.rectTransform.offsetMax = new Vector2(-12, -8);
        }
        FacilityIllustrations(root);
    }
    public static void Personnel(GameObject root)
    {
        foreach (string name in new[] { "Soldiers", "Weapons" }) TabArtwork(root.transform.Find("Window/Shell/Header/" + name).GetComponent<UnityEngine.UI.Button>());
    }
    public static void FacilityChoice(GameObject root)
    {
        FacilityIllustrations(root); Top(root.transform, "Symbol", 12, 24, 80, 80);
        foreach (string name in new[] { "Name", "Value" })
        {
            var rect = (RectTransform)root.transform.Find(name); rect.offsetMin = new Vector2(108, rect.offsetMin.y);
        }
    }
    public static void FacilityIllustrations(GameObject root)
    {
        var icons = FacilityIcons();
        foreach (var symbol in root.GetComponentsInChildren<CountryFacilitySymbol>(true))
        {
            SetArray(symbol, "_icons", icons);
            var data = new SerializedObject(symbol); symbol.SetFacility(data.FindProperty("_facility").stringValue);
            PrefabUtility.RecordPrefabInstancePropertyModifications(symbol);
        }
    }
    public static void TabArtwork(UnityEngine.UI.Button button)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(TabArt).OfType<Sprite>().Single();
        var root = (RectTransform)button.transform;
        var shadowRect = Fill(root, "Shadow", new Vector2(0, 12), new Vector2(0, -12)); shadowRect.anchoredPosition += Vector2.down * 4;
        var shadow = PanelImage(shadowRect, CountryButtonBorderBuilder.Shadow); shadowRect.SetAsFirstSibling();
        var face = PanelImage(Fill(root, "Surface", new Vector2(0, 12), new Vector2(0, -12)), Color.white);
        var focusRect = Fill(root, "Focus", new Vector2(-4, 8), new Vector2(4, -8));
        var focus = PanelImage(focusRect, Theme.Ink); focus.enabled = false; focusRect.SetSiblingIndex(1);
        foreach (var image in new[] { shadow, face, focus })
        { image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 8; }
        face.material = focus.material = CountryButtonBorderBuilder.Material;
        button.targetGraphic = face;
        root.Find("Selected").GetComponent<UnityEngine.UI.Graphic>().enabled = false;
        Theme.SelectTab(button, false); CountryButtonFeedbackBuilder.Configure(button);
    }
    private static UnityEngine.UI.Image PanelImage(RectTransform rect, Color tint)
    {
        if (rect.TryGetComponent<CountryGovernmentPlate>(out var plate)) UnityEngine.Object.DestroyImmediate(plate);
        return Image(rect, tint);
    }
    internal static Sprite[] FacilityIcons()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(CountryFacilityIconBuilder.Art).OfType<Sprite>().ToArray();
        var facilities = new[] { "Spirit", "Blacksmith", "Library", "Plaza", "Tavern", "Observatory", "Arena", "Museum", "Clinic", "Casino", "Guardhouse", "Entertainment" };
        return SchoolIcons().Concat(facilities.Select(name => sprites.Single(sprite => sprite.name == "Facility-" + name))).ToArray();
    }
    private static Sprite[] SchoolIcons()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SchoolArt).OfType<Sprite>().ToArray();
        return new[] { "School-Warrior", "School-Magic", "School-Church" }.Select(name => sprites.Single(sprite => sprite.name == name)).ToArray();
    }

    private static void TreasurySwitch(Transform root)
    {
        var toggle = root.GetComponent<UnityEngine.UI.Toggle>();
        if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(false);
        toggle.graphic = null;
        var box = root.Find("Box");
        foreach (Transform child in box) child.gameObject.SetActive(false);
        var trackRect = (RectTransform)box; trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(1, .5f);
        trackRect.sizeDelta = new Vector2(116, 56); trackRect.anchoredPosition = Vector2.zero;
        var track = box.GetComponent<UnityEngine.UI.Image>(); track.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/Country/UI/Shapes/RoundedPanel.png");
        track.type = UnityEngine.UI.Image.Type.Sliced; track.color = Theme.Recess; toggle.targetGraphic = track;
        var thumb = Child(box, "Thumb"); thumb.gameObject.SetActive(true); thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(.5f, .5f);
        thumb.sizeDelta = new Vector2(44, 44); thumb.anchoredPosition = new Vector2(-29, 0);
        Add<CanvasRenderer>(thumb.gameObject); var circle = Add<CountryCircleGraphic>(thumb.gameObject); circle.color = Theme.Text; circle.raycastTarget = false;
        var stateRect = Child(box, "State"); stateRect.gameObject.SetActive(true); stateRect.anchorMin = stateRect.anchorMax = stateRect.pivot = new Vector2(.5f, .5f);
        stateRect.sizeDelta = new Vector2(60, 48); stateRect.anchoredPosition = new Vector2(24, 0);
        var state = Label(stateRect, "OFF", 22); state.alignment = TextAlignmentOptions.Center;
        var label = root.Find("Label").GetComponent<TextMeshProUGUI>();
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = new Vector2(-132, 0); label.fontSize = 28;
        var data = new SerializedObject(Add<CountryToggleFeedback>(root.gameObject));
        data.FindProperty("_track").objectReferenceValue = track; data.FindProperty("_thumb").objectReferenceValue = thumb; data.FindProperty("_state").objectReferenceValue = state;
        data.ApplyModifiedPropertiesWithoutUndo();
        Theme.Colors(toggle, Color.white);
    }

    internal static void Command(UnityEngine.UI.Button button, Sprite motif)
    {
        var icon = Image(Top(button.transform, "Icon", 20, 28, 56, 56), Color.white);
        icon.sprite = motif; icon.preserveAspect = true; icon.raycastTarget = true;
        var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>(); label.rectTransform.offsetMin = new Vector2(92, 8);
        label.fontSize = 32; label.enableAutoSizing = false;
        foreach (var plate in button.GetComponentsInChildren<CountryGovernmentPlate>(true)) plate.Configure(0, plate.name == "Frame" || plate.name == "Focus" ? 2.5f : 0, 0);
        CountryButtonFeedbackBuilder.Configure(button);
    }
    private static void Readout(Transform parent, string name, float x, float width, Color tint)
    {
        var text = parent.Find(name).GetComponent<TextMeshProUGUI>();
        Top(parent, name, x, 44, width, 64); text.fontSize = 44; text.color = tint;
    }
    private static Sprite MenuIcon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/" + name + ".png");
}
