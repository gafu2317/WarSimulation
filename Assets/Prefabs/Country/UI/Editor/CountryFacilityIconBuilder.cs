using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static CountryGovernmentBuilder;

public static class CountryFacilityIconBuilder
{
    internal const string Art = "Assets/Images/UI/Country/Controls/FacilityEmblems-v1.png";
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string Records = "Artifacts/CountryUI/FacilityIcons-v1";
    private static readonly string[] Names = { "戦士学校", "魔法学校", "教会", "精霊使役学校", "鍛冶場", "図書館", "広場", "酒場", "天文台", "闘技場", "美術館", "診療所", "カジノ", "衛兵所", "歓楽街" };

    [MenuItem("WarSim/Country/Set Up Facility Icons")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating facility prefabs.");
        AssetDatabase.ImportAsset(Art);
        CountryButtonSpriteImporter.Import(Art, new[] {
            Region("Spirit", 28, 715, 319, 339), Region("Blacksmith", 384, 721, 344, 289),
            Region("Library", 759, 719, 310, 299), Region("Plaza", 1100, 717, 327, 331),
            Region("Tavern", 51, 371, 303, 301), Region("Observatory", 400, 366, 305, 324),
            Region("Arena", 742, 366, 329, 319), Region("Museum", 1113, 366, 311, 304),
            Region("Clinic", 21, 40, 340, 294), Region("Casino", 405, 38, 314, 254),
            Region("Guardhouse", 755, 33, 305, 316), Region("Entertainment", 1167, 19, 206, 344)
        });
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art); importer.isReadable = false; importer.SaveAndReimport();
        foreach (string name in new[] { "CountryFacilityBanner", "CountryFacilityPopup", "CountryGovernmentAllocationRow", "CountryGovernmentChoice", "CountryGovernment" })
        {
            string path = Folder + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (name == "CountryFacilityBanner") ApplyBanner(root);
                if (name == "CountryFacilityPopup") ApplyPopup(root);
                CountryGovernmentControlsBuilder.FacilityIllustrations(root);
                CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("15 shared facility icons updated. The open scene was not saved or reloaded.");
    }

    private static SpriteRect Region(string name, int x, int y, int width, int height) => new() { name = "Facility-" + name, rect = new Rect(x, y, width, height) };

    internal static void ApplyBanner(GameObject root)
    {
        var face = root.transform.Find("Face");
        var rect = Child(face, "Symbol"); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = new Vector2(14, 0); rect.sizeDelta = new Vector2(56, 56);
        var symbol = Configure(rect);
        var label = face.Find("Label").GetComponent<TextMeshProUGUI>();
        label.rectTransform.offsetMin = new Vector2(80, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        Set(root.GetComponent<CountryFacilityBanner>(), ("_symbol", symbol));
        CountryTextLayoutBuilder.Banner(root);
    }

    internal static void ApplyPopup(GameObject root)
    {
        var panel = root.transform.Find("Modal/Panel");
        var symbol = Configure(Top(panel, "Symbol", 36, 36, 96, 96));
        Top(panel, "Title", 148, 28, 596, 76);
        Top(panel, "Category", 148, 114, 600, 56);
        Set(root.GetComponent<CountryFacilityPopup>(), ("_symbol", symbol));
    }

    private static CountryFacilitySymbol Configure(RectTransform rect)
    {
        var symbol = Add<CountryFacilitySymbol>(rect.gameObject); symbol.raycastTarget = false;
        SetArray(symbol, "_icons", CountryGovernmentControlsBuilder.FacilityIcons());
        symbol.SetFacility("戦士学校");
        return symbol;
    }

    [MenuItem("WarSim/Country/Facility Icons/Show Catalog")]
    public static void ShowCatalog()
    {
        RequirePlay(); HideCatalog();
        var root = Fill(GameObject.Find("CountryHud").transform, "FacilityIconCatalog");
        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 50;
        Image(root, CountryGovernmentTheme.Background);
        Label(Top(root, "Title", 40, 24, 1600, 64), "施設アイコン", 42);
        for (int i = 0; i < Names.Length; i++)
        {
            var cell = Top(root, "Facility" + i, 40 + i % 5 * 376, 128 + i / 5 * 270, 344, 240);
            Configure(Top(cell, "Symbol", 108, 0, 128, 128)).SetFacility(Names[i]);
            Label(Top(cell, "Name", 0, 160, 344, 64), Names[i], 32).alignment = TextAlignmentOptions.Center;
        }
    }

    [MenuItem("WarSim/Country/Facility Icons/Hide Catalog")]
    public static void HideCatalog()
    {
        var root = GameObject.Find("CountryHud/FacilityIconCatalog");
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
    }

    [MenuItem("WarSim/Country/Facility Icons/Verify Bindings")]
    public static void VerifyBindings()
    {
        RequirePlay();
        var layer = GameObject.Find("CountryHud/FacilityLayer").GetComponent<CountryFacilityLayer>();
        var popup = layer.GetComponentInChildren<CountryFacilityPopup>(true);
        var banners = layer.Banners.Select(banner => {
            var symbol = banner.GetComponentInChildren<CountryFacilitySymbol>(true);
            if (!symbol.HasImage || symbol.raycastTarget) throw new InvalidOperationException("Missing or interactive icon: " + banner.Target.name);
            return new IconReport { Facility = banner.Target.name, Sprite = symbol.Icon.name, Path = AnimationUtility.CalculateTransformPath(banner.transform, GameObject.Find("CountryHud").transform) };
        }).ToArray();
        if (banners.Length != 45 || banners.Select(banner => banner.Facility).Distinct().Count() != 15)
            throw new InvalidOperationException("Expected 15 facilities in each of the 3 cities.");
        var targets = layer.Targets.Where(target => target.building.gameObject.activeInHierarchy).ToArray();
        foreach (var target in targets)
        {
            popup.Open(target);
            var symbol = popup.GetComponentInChildren<CountryFacilitySymbol>(true);
            if (!popup.IsOpen || !symbol.HasImage || symbol.Icon.name != banners.First(banner => banner.Facility == target.name).Sprite)
                throw new InvalidOperationException("Popup icon mismatch: " + target.name);
            popup.Close();
        }
        if (targets.Length != 15 || banners.Select(banner => banner.Sprite).Distinct().Count() != 15)
            throw new InvalidOperationException("Expected 15 distinct facility icons and 15 active-city popup targets.");
        Directory.CreateDirectory(Records); File.WriteAllText(Records + "/bindings.json", JsonUtility.ToJson(new BindingReport { Banners = banners, PopupsOpened = targets.Length }, true));
        Debug.Log("Facility icons: 45 banners, 15 distinct images and 15 popups verified.");
    }

    [MenuItem("WarSim/Country/Facility Icons/Capture")]
    public static void Capture()
    {
        RequirePlay(); Canvas.ForceUpdateCanvases();
        var hud = GameObject.Find("CountryHud");
        var report = new CaptureReport {
            Icons = hud.GetComponentsInChildren<CountryFacilitySymbol>().Where(symbol => symbol.HasImage).Select(symbol => new IconReport {
                Sprite = symbol.Icon.name, Path = AnimationUtility.CalculateTransformPath(symbol.transform, hud.transform)
            }).ToArray(),
            TextOverflows = hud.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing).Select(text => text.text).ToArray()
        };
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        Directory.CreateDirectory(Records); string path = Records + $"/UI-{width}x{height}";
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(path + ".png"));
    }

    private static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use the existing Editor in Play mode first."); }
    [Serializable] private sealed class IconReport { public string Facility, Sprite, Path; }
    [Serializable] private sealed class BindingReport { public IconReport[] Banners; public int PopupsOpened; }
    [Serializable] private sealed class CaptureReport { public IconReport[] Icons; public string[] TextOverflows; }
}
