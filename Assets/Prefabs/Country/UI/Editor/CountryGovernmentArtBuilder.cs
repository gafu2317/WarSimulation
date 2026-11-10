using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryGovernmentArtBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string Art = "Assets/Images/UI/Country";

    [MenuItem("WarSim/Country/Set Up Government Materials")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing prefabs.");
        Import(Art + "/Bookcloth-v1.png", false);
        Import(Art + "/PewterRim-v1.png", true);
        Save("CountryWorkboard", root => BuildSurface(root, new Vector2(1800, 600), .82f, true));
        Save("CountrySectionPlaque", root => BuildSurface(root, new Vector2(700, 104), 1, true));
        Save("CountryEffectStrip", root => {
            BuildSurface(root, new Vector2(700, 152), 1.06f, false);
            var fold = Fill(root.transform, "Fold");
            fold.anchorMin = fold.anchorMax = fold.pivot = Vector2.one;
            fold.anchoredPosition = new Vector2(-2, -2); fold.sizeDelta = new Vector2(24, 24);
            var graphic = Add<CountryGovernmentFold>(fold.gameObject);
            graphic.color = new Color32(195, 183, 156, 255); graphic.raycastTarget = false;
            CountryGovernmentControlsBuilder.Note(root);
        });
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryGovernment.prefab")) { Apply(scope.prefabContentsRoot); CountryPaletteBuilder.Apply(scope.prefabContentsRoot); }
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryGovernmentAllocationRow.prefab")) ApplyAllocation(scope.prefabContentsRoot);
    }

    public static void Apply(GameObject root)
    {
        var main = root.transform.Find("Window/Shell/Main");
        var construction = main.Find("Construction/Content");
        var training = main.Find("Training/Content");
        bool legacyTraining = training.GetComponent<CountryTrainingView>() == null;
        var surfaces = legacyTraining ? new[] { construction, training, main.Find("Budget/Facilities") } : new[] { construction, main.Find("Budget/Facilities") };
        foreach (var content in surfaces)
        {
            var board = Instance(content, "Workboard", "CountryWorkboard");
            board.offsetMin = new Vector2(0, -12); board.offsetMax = new Vector2(0, 12);
            board.SetAsFirstSibling();
        }
        construction.Find("Right/Surface").gameObject.SetActive(false);
        var title = construction.Find("Right/Detail/Title");
        var nameplate = Instance(title, "Plaque", "CountrySectionPlaque"); nameplate.SetAsFirstSibling();
        nameplate.anchorMin = nameplate.anchorMax = nameplate.pivot = Vector2.up;
        nameplate.sizeDelta = new Vector2(584, 112); nameplate.anchoredPosition = new Vector2(-16, 0);
        var name = (RectTransform)title.Find("Name");
        name.offsetMin = new Vector2(116, -4); name.offsetMax = new Vector2(-300, -4);
        ((RectTransform)title.Find("Symbol")).anchoredPosition = new Vector2(0, -8);
        ((RectTransform)title.Find("Level")).anchoredPosition = new Vector2(0, -4);
        ApplyStrip(construction.Find("Right/Detail/Effect"));
        if (legacyTraining)
        {
            var schoolPlaque = Instance(training.Find("Right"), "SchoolPlaque", "CountrySectionPlaque");
            schoolPlaque.anchorMin = schoolPlaque.anchorMax = schoolPlaque.pivot = Vector2.up;
            schoolPlaque.anchoredPosition = new Vector2(4, 4); schoolPlaque.sizeDelta = new Vector2(584, 96);
            schoolPlaque.SetAsFirstSibling();
            var school = (RectTransform)training.Find("Right/School");
            school.offsetMin = new Vector2(52, school.offsetMin.y);
        }
        foreach (var content in legacyTraining ? new[] { construction, training } : new[] { construction })
        {
            var divider = Fill(content, "ColumnDivider");
            divider.anchorMin = new Vector2(.25f, 0); divider.anchorMax = new Vector2(.25f, 1);
            divider.offsetMin = new Vector2(-1, 16); divider.offsetMax = new Vector2(1, -16);
            var graphic = Add<CountryGovernmentPlate>(divider.gameObject);
            graphic.Configure(0); graphic.color = CountryGovernmentTheme.Line; graphic.raycastTarget = false;
        }
    }

    public static void ApplyAllocation(GameObject root) => ApplyStrip(root.transform.Find("Effect/Details"));

    private static void ApplyStrip(Transform buttonRoot)
    {
        var strip = Instance(buttonRoot, "Art", "CountryEffectStrip"); strip.SetAsFirstSibling();
        var oldSurface = buttonRoot.Find("Surface"); oldSurface.gameObject.SetActive(false);
        var button = buttonRoot.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = strip.Find("Surface").GetComponent<CountryGovernmentPlate>();
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(.72f, .72f, .72f); colors.selectedColor = Color.white;
        colors.disabledColor = CountryGovernmentTheme.DisabledFace; button.colors = colors;
        CountryButtonFeedbackBuilder.Configure(button);
    }

    private static void BuildSurface(GameObject root, Vector2 size, float brightness, bool metal)
    {
        ((RectTransform)root.transform).sizeDelta = size;
        var shadow = Fill(root.transform, "Shadow"); shadow.anchoredPosition = new Vector2(0, -5);
        Plate(shadow, new Color(0, 0, 0, .38f), 12);
        var face = Plate(Fill(root.transform, "Surface"), new Color(brightness, brightness, brightness), metal ? 14 : 10);
        face.SetTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Bookcloth-v1.png"));
        if (root.name == "CountryWorkboard")
        {
            var light = Plate(Fill(root.transform, "Light"), new Color32(190, 202, 214, 42), 14);
            light.Configure(14, 0, .035f);
            light.transform.SetSiblingIndex(face.transform.GetSiblingIndex() + 1);
        }
        if (metal)
        {
            var edge = Fill(root.transform, "Rim"); edge.offsetMin = Vector2.one * -9; edge.offsetMax = Vector2.one * 9;
            var image = Add<UnityEngine.UI.Image>(edge.gameObject);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/PewterRim-v1.png");
            image.type = UnityEngine.UI.Image.Type.Sliced; image.fillCenter = false;
            image.pixelsPerUnitMultiplier = 5.5f; image.color = Color.white; image.raycastTarget = false;
        }
        else
        {
            var edge = Plate(Fill(root.transform, "Edge"), new Color32(116, 128, 142, 180), 10);
            edge.Configure(10, 1.5f, .08f);
        }
    }

    private static void Import(string path, bool sprite)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.sRGBTexture = true; importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = sprite ? 2048 : 256; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = !sprite; importer.wrapMode = sprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.filterMode = sprite ? FilterMode.Bilinear : FilterMode.Trilinear;
        if (sprite)
        {
            importer.alphaIsTransparency = true; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.spriteBorder = Vector4.one * 144;
        }
        importer.SaveAndReimport();
    }

    private static void Save(string name, Action<GameObject> build)
    {
        string path = Folder + "/" + name + ".prefab";
        bool exists = File.Exists(path);
        var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name, typeof(RectTransform));
        try { root.layer = 5; build(root); CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static RectTransform Instance(Transform parent, string name, string prefab)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + prefab + ".prefab");
            rect = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform;
            rect.name = name;
        }
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var layout = Add<UnityEngine.UI.LayoutElement>(rect.gameObject); layout.ignoreLayout = true;
        return rect;
    }

    private static RectTransform Fill(Transform parent, string name)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            var child = new GameObject(name, typeof(RectTransform)); child.layer = 5; child.transform.SetParent(parent, false);
            rect = (RectTransform)child.transform;
        }
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static CountryGovernmentPlate Plate(RectTransform rect, Color color, float cut)
    {
        var graphic = Add<CountryGovernmentPlate>(rect.gameObject);
        graphic.Configure(cut, 0, .075f); graphic.color = color; graphic.raycastTarget = false;
        return graphic;
    }

    private static T Add<T>(GameObject root) where T : Component => root.TryGetComponent<T>(out var component) ? component : root.AddComponent<T>();
}
