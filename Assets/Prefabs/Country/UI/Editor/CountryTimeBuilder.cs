using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;

public static class CountryTimeBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const float ButtonWidth = 112, DateWidth = 160;

    [MenuItem("WarSim/Country/Set Up Time Controls")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating time controls.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTopBar.prefab"))
            BuildTopBar(scope.prefabContentsRoot);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot;
            var popup = root.transform.Find("TimeProgressPopup");
            if (popup != null) Object.DestroyImmediate(popup.gameObject);
            var controls = root.GetComponent<CountryTimeControls>();
            var date = root.transform.Find("TopBar/Content/DateControls");
            var mode = date.Find("TimeToggle/ModeButton");
            Set(controls, ("_dateText", date.Find("CurrentDate").GetComponent<TextMeshProUGUI>()),
                ("_modeButton", mode.GetComponent<Button>()),
                ("_stopIcon", mode.Find("Icon/Stop").gameObject),
                ("_playIcon", mode.Find("Icon/Play").gameObject),
                ("_doubleSpeedIcon", mode.Find("Icon/DoubleSpeed").gameObject));
            CountryButtonFeedbackBuilder.Configure(mode.GetComponent<Button>());
        }
        Debug.Log("Three-mode time control updated. The open scene was not saved or reloaded.");
    }

    private static void BuildTopBar(GameObject root)
    {
        var date = root.transform.Find("Content/DateControls");
        var toggle = date.Find("TimeToggle");
        var mode = toggle.Find("ModeButton") ?? toggle.Find("PlayButton");
        mode.name = "ModeButton"; mode.gameObject.SetActive(true);
        foreach (var old in new[] { toggle.Find("PauseButton"), date.Find("MonthEndButton"), mode.Find("Face/StateIcon"), mode.Find("Face/Label") })
            if (old != null) Object.DestroyImmediate(old.gameObject);
        var button = mode.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        var face = button.targetGraphic.rectTransform;
        face.offsetMin = Vector2.one * 4; face.offsetMax = Vector2.one * -4;
        foreach (string name in new[] { "UpperEdge", "LowerEdge" })
            face.Find(name).gameObject.SetActive(false);
        mode.GetComponent<Image>().color = Color.clear;
        mode.Find("InsetFrame").GetComponent<Image>().color = Color.clear;
        button.targetGraphic.color = Color.clear;
        button.transition = Selectable.Transition.None;
        ConfigureLayout(date);

        var icon = Center(mode, "Icon", Vector2.zero, new Vector2(104, 80));
        var pause = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/Controls/pause-v1.png");
        var stop = Shape(icon, "Stop", new Vector2(-21, 0), Vector2.one * 72, pause, CountryGovernmentTheme.TimeStop);
        var triangle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/Controls/play-v1.png");
        var play = Shape(icon, "Play", new Vector2(-23, 0), Vector2.one * 72, triangle, CountryGovernmentTheme.TimePlay);
        var fast = Center(icon, "DoubleSpeed", Vector2.zero, new Vector2(104, 80));
        Shape(fast, "First", new Vector2(-24, 0), Vector2.one * 64, triangle, CountryGovernmentTheme.DoubleSpeed);
        Shape(fast, "Second", new Vector2(10, 0), Vector2.one * 64, triangle, CountryGovernmentTheme.DoubleSpeed);
        stop.gameObject.SetActive(true); play.gameObject.SetActive(false); fast.gameObject.SetActive(false);
        CountryButtonFeedbackBuilder.Configure(button);
        foreach (var component in mode.GetComponentsInChildren<Component>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mode.gameObject);
    }

    internal static void ConfigureLayout(Transform date)
    {
        Width(date, DateWidth + ButtonWidth + 8);
        Width(date.Find("CurrentDate"), DateWidth);
        var label = date.Find("CurrentDate").GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.MidlineRight;
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        var group = date.GetComponent<HorizontalLayoutGroup>(); group.spacing = 8;
        PrefabUtility.RecordPrefabInstancePropertyModifications(group);
        Width(date.Find("TimeToggle"), ButtonWidth);
        Width(date.Find("TimeToggle/ModeButton"), ButtonWidth);
    }

    private static void Width(Transform target, float width)
    {
        var layout = target.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0;
        PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
    }

    private static RectTransform Center(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Image Shape(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        var rect = Center(parent, name, position, size);
        var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }
}
