using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountryRosterSelectionBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    public static void Ensure() => SavePrefab("CountryRosterSelection", Build);
    private static void Build(GameObject root)
    {
        Stretch((RectTransform)root.transform);
        Label(TopFill(root.transform, "Capacity", 0, 56, 16, 16), "", 28, Theme.Muted);
        var strip = TopFill(root.transform, "Members", 48, 128, 8, 8);
        var scroll = Add<ScrollRect>(strip.gameObject); scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = ScrollSensitivity;
        var viewport = Fill(strip, "Viewport"); Image(viewport, Color.white).raycastTarget = true; Add<Mask>(viewport.gameObject).showMaskGraphic = false;
        var members = Child(viewport, "Content"); members.anchorMin = members.anchorMax = members.pivot = Vector2.up;
        members.sizeDelta = new Vector2(0, 128); members.anchoredPosition = Vector2.zero;
        Horizontal(members, 12); Add<ContentSizeFitter>(members.gameObject).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = members;
        Label(Fill(strip, "Empty", new Vector2(16, 0), new Vector2(-16, 0)), "", 30, Theme.Muted).gameObject.SetActive(false);
        var body = Fill(root.transform, "Body", new Vector2(8, 0), new Vector2(-8, -192));
        var board = Instance(body, "Workboard", "CountryWorkboard"); board.SetAsFirstSibling();
        board.offsetMin = new Vector2(-12, -8); board.offsetMax = new Vector2(12, 8);
        Instance(body, "Roster", "CountryRosterView");
    }
    public static RectTransform Place(Transform parent)
    {
        var root = Instance(parent, "Selection", "CountryRosterSelection");
        root.offsetMax = new Vector2(0, -112); return root;
    }
    public static void ApplyTraining(GameObject root)
    {
        var selection = Place(root.transform);
        Set(root.GetComponent<CountryTrainingView>(),
            ("_capacity", selection.Find("Capacity").GetComponent<TextMeshProUGUI>()),
            ("_members", (RectTransform)selection.Find("Members/Viewport/Content")),
            ("_empty", selection.Find("Members/Empty").GetComponent<TextMeshProUGUI>()),
            ("_view", selection.Find("Body/Roster").GetComponent<CountryRosterView>()));
        foreach (string name in new[] { "Capacity", "Members", "Body" })
        {
            var old = root.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
    }
    private static RectTransform Instance(Transform parent, string name, string prefab)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) { rect = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"), parent)).transform; rect.name = name; }
        Stretch(rect); return rect;
    }
}
