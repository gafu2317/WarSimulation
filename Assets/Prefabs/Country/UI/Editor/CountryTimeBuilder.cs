using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;
using Theme = CountryGovernmentTheme;

public static class CountryTimeBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const float ButtonWidth = 112, Gap = 8, Padding = 12;
    private static readonly (string Name, string Label)[] Modes =
        { ("Stop", "停止"), ("Normal", "1倍速"), ("DoubleSpeed", "2倍速") };

    [MenuItem("WarSim/Country/Set Up Time Controls")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating time controls.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTopBar.prefab"))
            BuildTopBar(scope.prefabContentsRoot);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot;
            var controls = root.GetComponent<CountryTimeControls>();
            var date = root.transform.Find("TopBar/Content/DateControls");
            var buttons = new Button[Modes.Length];
            var frames = new GameObject[Modes.Length];
            for (int i = 0; i < Modes.Length; i++)
            {
                var mode = date.Find(Modes[i].Name);
                buttons[i] = mode.GetComponent<Button>();
                frames[i] = mode.Find("ActiveFrame").gameObject;
            }
            Set(controls, ("_dateText", date.Find("CurrentDate").GetComponent<TextMeshProUGUI>()));
            SetArray(controls, "_modeButtons", buttons);
            SetArray(controls, "_selectionFrames", frames);
        }
        Debug.Log("Three-mode time control updated. The open scene was not saved or reloaded.");
    }

    private static void BuildTopBar(GameObject root)
    {
        var date = (RectTransform)root.transform.Find("Content/DateControls");
        foreach (string name in new[] { "TimeToggle", "MonthEndButton" })
            if (date.Find(name) is Transform old) Object.DestroyImmediate(old.gameObject);
        if (date.TryGetComponent<HorizontalLayoutGroup>(out var group)) Object.DestroyImmediate(group);
        ConfigureLayout(date);
        date.SetAsLastSibling();
        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Shapes/RoundedPanel.png");
        var background = Picture(Fill(date, "Background"), rounded);
        background.color = Theme.Panel;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();
        var label = Label(TopFill(date, "CurrentDate", 6, 54), "1月1日", 36, Theme.Text);
        label.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < Modes.Length; i++)
        {
            var rect = Top(date, Modes[i].Name, Padding + i * (ButtonWidth + Gap), 62, ButtonWidth, 92);
            var button = PageButton(rect, Modes[i].Label);
            button.onClick = new Button.ButtonClickedEvent();
            Label((RectTransform)rect.Find("Label"), Modes[i].Label, 29, Theme.Ink).alignment = TextAlignmentOptions.Center;
            var selected = Picture(Fill(rect, "ActiveFrame", new Vector2(0, 8), new Vector2(0, -8)), rounded);
            selected.color = Theme.Accent;
            selected.raycastTarget = false;
            selected.transform.SetSiblingIndex(1);
            selected.gameObject.SetActive(i == 0);
            Theme.Colors(button, i == 0 ? Theme.Paper : Theme.Face);
        }
    }

    internal static void ConfigureLayout(Transform date)
    {
        var rect = (RectTransform)date;
        Add<LayoutElement>(date.gameObject).ignoreLayout = true;
        rect.anchorMin = rect.anchorMax = Vector2.right;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24, -8);
        rect.sizeDelta = new Vector2(ButtonWidth * Modes.Length + Gap * (Modes.Length - 1) + Padding * 2, 156);
    }
}
