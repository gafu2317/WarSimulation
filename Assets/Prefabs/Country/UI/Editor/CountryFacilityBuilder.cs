using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryFacilityBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string FontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";
    private const string BannerPath = Folder + "/CountryFacilityBanner.prefab";
    private const string PopupPath = Folder + "/CountryFacilityPopup.prefab";
    private const string LayerPath = Folder + "/CountryFacilityLayer.prefab";
    private const string Personality = "キャラクターの性格に関わる施設。";
    private static readonly (string model, string name, string category, string description)[] Definitions =
    {
        ("WarriorAcademy_Lv5", "戦士学校", "職業施設", "戦士のための学校。"),
        ("ArcaneAcademy_Lv5", "魔法学校", "職業施設", "魔法を学ぶ学校。"),
        ("Church", "教会", "職業施設", "職業に関わる宗教施設。"),
        ("Library", "図書館", "性格施設", "蔵書の内容によって、CP0から復帰するときの性格の決まり方を調整する。"),
        ("Plaza", "広場", "性格施設", "飾り付けによって、CP0から復帰するときの性格の決まり方を調整する。"),
        ("Tavern", "酒場", "性格施設", Personality),
        ("Observatory", "天文台", "性格施設", Personality),
        ("Arena", "闘技場", "性格施設", Personality),
        ("Museum", "美術館", "性格施設", Personality),
        ("Clinic", "診療所", "性格施設", Personality),
        ("Casino", "カジノ", "性格施設", Personality),
        ("Guardhouse", "衛兵所", "性格施設", Personality),
        ("Entertainment quarter", "歓楽街", "性格施設", Personality),
        ("SpiritAcademy_Lv5", "精霊使役学校", "精霊施設", "ステータスを上昇させる施設。"),
        ("Blacksmith", "鍛冶場", "武器製作施設", "施設レベルを上げると、より良い武器を製作できる。")
    };

    [MenuItem("WarSim/Country/Set Up Facility Banners")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Country.unity")
            throw new InvalidOperationException("Open the Country scene outside Play mode first.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        string characters = string.Concat(Definitions.Select(d => d.name + d.category + d.description)) + "施設閉じる×";
        if (!font.HasCharacters(characters) && !font.TryAddCharacters(characters, out string missing))
            throw new InvalidOperationException("Missing facility glyphs: " + missing);
        BuildBanner(); BuildPopup(); BuildLayer();
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot.transform;
            var layer = root.Find("FacilityLayer") ?? Instance(AssetDatabase.LoadAssetAtPath<GameObject>(LayerPath), root, "FacilityLayer");
            layer.SetAsFirstSibling();
            var data = new SerializedObject(layer.GetComponent<CountryFacilityLayer>());
            var obstacles = data.FindProperty("_obstacles");
            string[] names = { "TopBar", "Minimap", "BottomLeftButtons", "BottomRightButton" };
            obstacles.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) obstacles.GetArrayElementAtIndex(i).objectReferenceValue = root.Find(names[i]);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(layer.GetComponent<CountryFacilityLayer>());
        }
        AssetDatabase.SaveAssets();
        var country = Object.FindAnyObjectByType<CountryComparisonView>();
        var controller = Object.FindAnyObjectByType<CountryFacilityLayer>(FindObjectsInactive.Include);
        if (country != null && controller != null && controller.Targets.Count == 0) BindTargets(country);
    }

    public static void BindTargets(CountryComparisonView country)
    {
        var controller = Object.FindFirstObjectByType<CountryFacilityLayer>(FindObjectsInactive.Include);
        if (controller == null) return;
        var targets = new List<CountryFacilityTarget>();
        foreach (var city in country.cities)
        {
            var transforms = city.GetComponentsInChildren<Transform>(true);
            foreach (var definition in Definitions)
            {
                var building = definition.model == "Entertainment quarter"
                    ? city.transform.Find("Buildings and courtyards/Entertainment quarter")
                    : transforms.Single(t => t.name == definition.model && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
                if (building == null) throw new InvalidOperationException("Missing facility: " + city.name + "/" + definition.model);
                var renderers = building.GetComponentsInChildren<MeshRenderer>(true);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                targets.Add(new CountryFacilityTarget {
                    building = building, localAnchor = building.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y + 1, bounds.center.z)),
                    name = definition.name, category = definition.category, description = definition.description, accent = Accent(definition.category)
                });
            }
        }
        Undo.RecordObject(controller, "Connect Country facility banners");
        controller.Bind(country.viewCamera, country, targets.ToArray());
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorSceneManager.MarkSceneDirty(country.gameObject.scene);
        Directory.CreateDirectory(".unity/test-results");
        File.WriteAllText(".unity/test-results/country-facility-targets.json", JsonUtility.ToJson(new TargetReport {
            cities = country.cities.Length, facilitiesPerCity = Definitions.Length,
            entries = targets.Select(t => new TargetEntry { name = t.name, category = t.category, building = t.building.name,
                country = country.cities.First(c => t.building.IsChildOf(c.transform)).name,
                anchor = t.building.TransformPoint(t.localAnchor) }).ToArray()
        }, true));
    }

    [Serializable] private sealed class TargetReport { public int cities, facilitiesPerCity; public TargetEntry[] entries; }
    [Serializable] private sealed class TargetEntry { public string name, category, building, country; public Vector3 anchor; }

    private static void BuildBanner()
    {
        bool exists = File.Exists(BannerPath);
        var root = exists ? PrefabUtility.LoadPrefabContents(BannerPath)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryButton.prefab"));
        try
        {
            root.name = "CountryFacilityBanner";
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(240, 112);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .001f);
            var face = (RectTransform)root.transform.Find("Face");
            face.anchorMin = Vector2.zero; face.anchorMax = Vector2.right; face.pivot = new Vector2(.5f, 0);
            face.anchoredPosition = new Vector2(0, 12); face.sizeDelta = new Vector2(-4, 64);
            var image = face.GetComponent<UnityEngine.UI.Image>(); image.color = new Color32(100, 112, 127, 250);
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.78f, .78f, .78f); button.colors = colors;
            var label = Text(Fill(face, "Label", new Vector2(18, 0), new Vector2(-12, 0)), "施設", 36);
            label.alignment = TextAlignmentOptions.Midline;
            var accentRect = Child(face, "CategoryAccent");
            accentRect.anchorMin = Vector2.zero; accentRect.anchorMax = Vector2.up;
            accentRect.pivot = new Vector2(0, .5f); accentRect.anchoredPosition = new Vector2(3, 0); accentRect.sizeDelta = new Vector2(6, -8);
            var accent = Image(accentRect, Accent("職業施設"));
            var pointer = Child(root.transform, "Pointer");
            pointer.anchorMin = pointer.anchorMax = new Vector2(.5f, 0); pointer.pivot = new Vector2(.5f, .5f);
            pointer.anchoredPosition = new Vector2(0, 8); pointer.sizeDelta = new Vector2(18, 18); pointer.localRotation = Quaternion.Euler(0, 0, -90);
            var tip = Image(pointer, new Color32(100, 112, 127, 250));
            tip.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/Controls/play-v1.png"); tip.type = UnityEngine.UI.Image.Type.Simple;
            var connector = Child(root.transform, "Connector"); connector.anchorMin = connector.anchorMax = new Vector2(.5f, 0);
            connector.pivot = new Vector2(.5f, 1); connector.anchoredPosition = Vector2.zero; connector.sizeDelta = new Vector2(2, 0);
            Image(connector, new Color32(193, 207, 217, 220));
            var banner = root.GetComponent<CountryFacilityBanner>() ?? root.AddComponent<CountryFacilityBanner>();
            Set(banner, ("_label", label), ("_button", button), ("_accent", accent), ("_connector", connector));
            CountryFacilityIconBuilder.ApplyBanner(root);
            CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, BannerPath);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static void BuildPopup()
    {
        bool exists = File.Exists(PopupPath);
        var root = exists ? PrefabUtility.LoadPrefabContents(PopupPath) : new GameObject("CountryFacilityPopup", typeof(RectTransform));
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            var popup = root.GetComponent<CountryFacilityPopup>() ?? root.AddComponent<CountryFacilityPopup>();
            var modal = Fill(root.transform, "Modal", Vector2.zero, Vector2.zero);
            modal.gameObject.SetActive(false);
            if (!modal.TryGetComponent<Canvas>(out var canvas)) canvas = modal.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 30;
            if (!modal.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out _)) modal.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var dim = Fill(modal, "Dim", Vector2.zero, Vector2.zero);
            var dimImage = Image(dim, new Color(0.04f, .055f, .08f, .62f)); dimImage.sprite = null; dimImage.raycastTarget = true;
            var dismiss = dim.GetComponent<UnityEngine.UI.Button>() ?? dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dismiss.targetGraphic = dimImage; dismiss.transition = UnityEngine.UI.Selectable.Transition.None;
            dismiss.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var panel = Child(modal, "Panel"); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(900, 620);
            Image(panel, new Color32(84, 94, 107, 255)).raycastTarget = true;
            Image(Fill(panel, "Face", Vector2.one * 3, Vector2.one * -3), new Color32(112, 123, 137, 255)).raycastTarget = true;
            var title = Text(Top(panel, "Title", 36, 28, 700, 76), "施設", 48);
            var category = Text(Top(panel, "Category", 40, 114, 760, 56), "職業施設", 36);
            category.color = new Color32(197, 208, 222, 255);
            var accent = Image(Top(panel, "Accent", 36, 188, 828, 4), Accent("職業施設"));
            var description = Text(Top(panel, "Description", 40, 220, 820, 222), "", 36);
            description.alignment = TextAlignmentOptions.TopLeft;
            description.textWrappingMode = TextWrappingModes.Normal;
            var close = Button(panel, "CloseButton", "閉じる", new Vector2(240, 112));
            close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = new Vector2(.5f, 0);
            close.GetComponent<RectTransform>().pivot = new Vector2(.5f, 0); close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 24);
            var cross = Button(panel, "CloseIcon", "×", new Vector2(112, 112));
            var crossRect = cross.GetComponent<RectTransform>(); crossRect.anchorMin = crossRect.anchorMax = crossRect.pivot = Vector2.one;
            crossRect.anchoredPosition = new Vector2(-20, -20);
            foreach (var button in new[] { close, cross, dismiss })
            {
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(button.onClick, popup.Close);
            }
            Set(popup, ("_modal", modal.gameObject), ("_title", title), ("_category", category),
                ("_description", description), ("_accent", accent), ("_closeButton", close));
            CountryFacilityIconBuilder.ApplyPopup(root);
            modal.gameObject.SetActive(false);
            CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static void BuildLayer()
    {
        bool exists = File.Exists(LayerPath);
        var root = exists ? PrefabUtility.LoadPrefabContents(LayerPath) : new GameObject("CountryFacilityLayer", typeof(RectTransform));
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            var layer = root.GetComponent<CountryFacilityLayer>() ?? root.AddComponent<CountryFacilityLayer>();
            var banners = Fill(root.transform, "Banners", Vector2.zero, Vector2.zero);
            var popup = root.transform.Find("Popup") ?? Instance(AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath), root.transform, "Popup");
            Set(layer, ("_bannerArea", banners), ("_bannerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BannerPath).GetComponent<CountryFacilityBanner>()),
                ("_popup", popup.GetComponent<CountryFacilityPopup>()));
            CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, LayerPath);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static Color32 Accent(string category) => category switch
    {
        "職業施設" => new Color32(150, 181, 211, 255),
        "性格施設" => new Color32(164, 191, 159, 255),
        "精霊施設" => new Color32(178, 165, 207, 255),
        _ => new Color32(210, 174, 137, 255)
    };

    private static void Set(Object target, params (string name, Object value)[] fields)
    {
        var data = new SerializedObject(target);
        foreach (var field in fields) data.FindProperty(field.name).objectReferenceValue = field.value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Instance(GameObject prefab, Transform parent, string name)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); instance.name = name;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        return instance.transform;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string name, string text, Vector2 size)
    {
        var rect = parent.Find(name) as RectTransform ?? (RectTransform)Instance(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryTopBarButton.prefab"), parent, name);
        rect.sizeDelta = size;
        var label = rect.Find("Face/Label").GetComponent<TextMeshProUGUI>(); label.text = text;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        return rect.GetComponent<UnityEngine.UI.Button>();
    }

    private static RectTransform Child(Transform parent, string name)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        var child = new GameObject(name, typeof(RectTransform)); child.layer = LayerMask.NameToLayer("UI");
        child.transform.SetParent(parent, false); return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static RectTransform Fill(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = Child(parent, name); Stretch(rect); rect.offsetMin = min; rect.offsetMax = max; return rect;
    }

    private static RectTransform Top(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = Child(parent, name); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }

    private static UnityEngine.UI.Image Image(RectTransform rect, Color tint)
    {
        var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Shapes/RoundedPanel.png");
        image.type = UnityEngine.UI.Image.Type.Sliced; image.color = tint; image.raycastTarget = false; return image;
    }

    private static TextMeshProUGUI Text(RectTransform rect, string text, float size)
    {
        var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/NotoSansJP-Regular SDF - Country HUD.mat");
        label.text = text; label.fontSize = size; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.color = new Color32(236, 240, 244, 255); label.raycastTarget = false;
        return label;
    }
}
