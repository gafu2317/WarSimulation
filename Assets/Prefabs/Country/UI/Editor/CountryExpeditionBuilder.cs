using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;
using Theme = CountryGovernmentTheme;

public static class CountryExpeditionBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string DataPath = "Assets/Data/Country/CountryWorldMap.asset";
    private const string Art = "Assets/Images/UI/Country/Expedition/";

    [MenuItem("WarSim/Country/Set Up Expeditions")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating expedition prefabs.");
        ImportArt();
        var data = BuildData();
        CountryRosterSelectionBuilder.Ensure();
        SavePrefab("CountryTraining", CountryRosterSelectionBuilder.ApplyTraining);
        SavePrefab("CountryExpeditionRow", root => BuildRow(root, data));
        SavePrefab("CountryExpedition", root => BuildWindow(root, data));
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryGovernment.prefab")) ApplyReport(scope.prefabContentsRoot, data);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var hud = scope.prefabContentsRoot.transform;
            var root = Instance(hud, "Expeditions", "CountryExpedition");
            var expedition = root.GetComponent<CountryExpeditionPanel>();
            var government = hud.Find("Government").GetComponent<CountryGovernmentPanel>();
            var personnel = hud.Find("Personnel").GetComponent<CountryPersonnelPanel>();
            var settings = hud.Find("Settings").GetComponent<CountrySettingsPanel>();
            Set(expedition, ("_open", hud.Find("BottomRightButton").GetComponent<Button>()), ("_government", government), ("_personnel", personnel), ("_settings", settings));
            foreach (var controller in new Component[] { government, personnel, settings })
            { Set(controller, ("_expeditions", expedition)); PrefabUtility.RecordPrefabInstancePropertyModifications(controller); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(expedition);
            CountryUsabilityBuilder.ApplyHud(scope.prefabContentsRoot);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        string glyphs = "遠征地図編成領地所属国自国王都首都隣接境界選択往路帰路帰還到着出発獲得報告隊人武器変更持出中明るいへできるとする片道日程進行の状況全員同じ複数重ねて間合わない名前金川谷北南東西冠白峰森丘琥珀翠野口端グランツベルク公エルム連邦アルトリア予約訓練開始復活待機期間固定戻る閉じるキャンセル0123456789＋→／—›‹";
        glyphs += string.Concat(data.Nations.Select(item => item.Name)) + string.Concat(data.Regions.Select(item => item.Name));
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing expedition glyphs: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        Debug.Log("Expedition prefabs and monthly report connected. The open scene was not saved or reloaded.");
    }

    private static void ImportArt()
    {
        var crown = (TextureImporter)AssetImporter.GetAtPath(Art + "Capital-Crown-v1.png");
        crown.textureType = TextureImporterType.Sprite; crown.spriteImportMode = SpriteImportMode.Single;
        crown.alphaIsTransparency = true; crown.mipmapEnabled = false; crown.npotScale = TextureImporterNPOTScale.None;
        crown.textureCompression = TextureImporterCompression.Uncompressed; crown.SaveAndReimport();
        CountryButtonSpriteImporter.Import(Art + "MapSymbols-v1.png", new[] {
            new SpriteRect { name = "Map-Castle", rect = new Rect(144, 111, 830, 613) },
            new SpriteRect { name = "Map-Flag", rect = new Rect(1249, 106, 632, 611) }
        });
    }

    private static CountryWorldMapData BuildData()
    {
        var data = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CountryWorldMapData>();
            data.Nations = new[] {
                new CountryWorldNation { Name = "アルトリア王国", Color = new Color32(150,194,206,255) },
                new CountryWorldNation { Name = "グランツ王国", Color = new Color32(212,170,154,255) },
                new CountryWorldNation { Name = "ベルク公国", Color = new Color32(185,177,207,255) },
                new CountryWorldNation { Name = "エルム連邦", Color = new Color32(201,196,158,255) }
            };
            CountryExpeditionMapBuilder.Apply(data);
            AssetDatabase.CreateAsset(data, DataPath);
        }
        data.HomeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Capital-Crown-v1.png");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + "MapSymbols-v1.png").OfType<Sprite>().ToArray();
        data.Castle = sprites.Single(sprite => sprite.name == "Map-Castle"); data.Flag = sprites.Single(sprite => sprite.name == "Map-Flag");
        data.Validate(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); return data;
    }

    private static void BuildRow(GameObject root, CountryWorldMapData data)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1200, 144); Layout(rect, 144);
        var button = Button(rect, "", Theme.Panel); root.transform.Find("Label").gameObject.SetActive(false);
        root.transform.Find("Outline").gameObject.SetActive(false);
        Picture(Top(rect, "Castle", 20, 28, 88, 88), data.Castle);
        var name = Label(TopFill(rect, "Name", 8, 64, 140, 320), "", 36);
        var detail = Label(TopFill(rect, "Detail", 74, 48, 140, 320), "", 28, Theme.Muted);
        var status = Label(Right(rect, "Status", 24, 24, 280, 88), "", 30, Theme.Accent); status.alignment = TextAlignmentOptions.MidlineRight;
        Image(BottomFill(rect, "Divider", 1), Theme.Line);
        Set(Add<CountryExpeditionRow>(root), ("_button", button), ("_name", name), ("_detail", detail), ("_status", status));
        CountryCommandMotifsBuilder.ApplyExpeditionRow(root);
    }

    private static void BuildWindow(GameObject root, CountryWorldMapData data)
    {
        Stretch((RectTransform)root.transform);
        var panel = Add<CountryExpeditionPanel>(root);
        var window = Fill(root.transform, "Window"); var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 50;
        Add<GraphicRaycaster>(window.gameObject); Image(window, Theme.Background).raycastTarget = true;
        var shell = Fill(window, "Shell"); var group = Add<CanvasGroup>(shell.gameObject);
        var header = TopFill(shell, "Header", 0, 112); Image(header, Theme.Panel);
        var date = Label(Top(header, "Date", 40, 0, 216, 112), "", 34, Theme.Accent);
        var log = Button(Right(header, "Active", 40, 0, 320, 112), "遠征中 0隊", Theme.Face);
        var main = Fill(shell, "Main", new Vector2(40, 144), new Vector2(-40, -200));
        var pageContent = main.Find("Viewport/Content") ?? main;
        var mapPage = (RectTransform)(shell.Find("Map") ?? pageContent.Find("Map")) ?? Fill(shell, "Map");
        var map = BuildMap(mapPage, data);
        var formation = Fill(pageContent, "Formation");
        var equipmentRect = (RectTransform)formation.Find("Header/Equipment");
        if (equipmentRect != null) equipmentRect.SetParent(formation, false);
        var equipment = Button(Right(formation, "Equipment", 0, 0, 400, 112), "武器を変更", Theme.Face);
        var selection = CountryRosterSelectionBuilder.Place(formation);
        var count = selection.Find("Capacity").GetComponent<TextMeshProUGUI>();
        var members = (RectTransform)selection.Find("Members/Viewport/Content");
        var view = selection.Find("Body/Roster").GetComponent<CountryRosterView>();
        foreach (string oldName in new[] { "Count", "Members", "Body" })
        { var old = formation.Find(oldName); if (old != null) Object.DestroyImmediate(old.gameObject); }
        var oldCount = shell.Find("MemberCount"); if (oldCount != null) Object.DestroyImmediate(oldCount.gameObject);
        var confirm = Confirmation(window, "Confirm", group);
        var logWindow = Fill(window, "Log"); var logCanvas = Add<Canvas>(logWindow.gameObject); logCanvas.overrideSorting = true; logCanvas.sortingOrder = 70;
        Add<GraphicRaycaster>(logWindow.gameObject); Image(logWindow, new Color(.02f, .04f, .08f, .85f)).raycastTarget = true;
        var logCard = Child(logWindow, "Card"); logCard.anchorMin = logCard.anchorMax = logCard.pivot = Vector2.one * .5f; logCard.sizeDelta = new Vector2(1480, 880); Board(logCard);
        var logGroup = Add<CanvasGroup>(logCard.gameObject);
        Label(TopFill(logCard, "Title", 24, 80, 40, 40), "遠征中の部隊", 44);
        var logScroll = Scroll(Fill(logCard, "List", new Vector2(40, 144), new Vector2(-40, -128)));
        var logEmpty = Label(TopFill(logCard, "Empty", 144, 96, 64, 64), "遠征中の部隊はない", 32, Theme.Muted);
        var closeLog = Button(Right(logCard, "Close", 40, 744, 280, 112), "閉じる", Theme.Face);
        var logDetails = Confirmation(logWindow, "Details", logGroup);
        Set(panel, ("_data", data), ("_window", window.gameObject), ("_shell", group),
            ("_map", map), ("_view", view), ("_dateText", date),
            ("_emptySlotPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryTrainingEmptySlot.prefab")), ("_members", members), ("_count", count), ("_equipment", equipment), ("_confirmation", confirm),
            ("_log", log), ("_logLabel", log.GetComponentInChildren<TextMeshProUGUI>()), ("_logEmpty", logEmpty), ("_logWindow", logWindow.gameObject), ("_logContent", logScroll.content), ("_logClose", closeLog), ("_logDetails", logDetails),
            ("_rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryExpeditionRow.prefab").GetComponent<CountryExpeditionRow>()),
            ("_tokenPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountrySoldierToken.prefab").GetComponent<CountryRosterRow>()));
        CountryExpeditionPageBuilder.Apply(root);
        logWindow.gameObject.SetActive(false); window.gameObject.SetActive(false);
    }

    private static CountryWorldMapView BuildMap(RectTransform page, CountryWorldMapData data)
    {
        var area = Fill(page, "MapArea");
        var oldBoard = area.Find("Workboard"); if (oldBoard != null) oldBoard.gameObject.SetActive(false);
        Image(area, new Color32(43, 70, 87, 255)); Plate(Fill(area, "MapFrame"), Theme.Border, 0, 2.5f);
        var plane = Fill(area, "Plane"); var aspect = Add<AspectRatioFitter>(plane.gameObject); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1.6f;
        var oldTerrain = plane.Find("Terrain"); if (oldTerrain != null) Object.DestroyImmediate(oldTerrain.gameObject);
        Image(Fill(plane, "Sea"), new Color32(43, 70, 87, 255)).transform.SetAsFirstSibling();
        var continent = Add<CountryWorldRegionGraphic>(Fill(plane, "Continent").gameObject); continent.raycastTarget = false; continent.transform.SetSiblingIndex(1);
        var view = Add<CountryWorldMapView>(plane.gameObject);
        var regions = new CountryWorldRegionGraphic[data.Regions.Length]; var markers = new Button[data.Nations.Length]; var flags = new Image[data.Nations.Length];
        var fills = Fill(plane, "Regions"); var nodes = Fill(plane, "Markers");
        var routeRect = Fill(plane, "Route"); Add<CanvasRenderer>(routeRect.gameObject); var route = Add<CountryWorldRouteGraphic>(routeRect.gameObject); route.color = Theme.Accent; route.raycastTarget = false;
        route.transform.SetSiblingIndex(nodes.GetSiblingIndex());
        for (int i = 0; i < regions.Length; i++)
        {
            var region = Fill(fills, $"Region{i:00}"); Add<CanvasRenderer>(region.gameObject); regions[i] = Add<CountryWorldRegionGraphic>(region.gameObject); regions[i].raycastTarget = true;
        }
        foreach (Transform old in nodes.Cast<Transform>().Where(child => child.name.StartsWith("Region", StringComparison.Ordinal)).ToArray()) Object.DestroyImmediate(old.gameObject);
        for (int i = 0; i < markers.Length; i++)
        {
            var marker = Child(nodes, $"Capital{i}"); marker.anchorMin = marker.anchorMax = data.Regions[data.Capital(i)].Center;
            marker.pivot = Vector2.one * .5f; marker.anchoredPosition = Vector2.zero; marker.sizeDelta = new Vector2(112, 112);
            Image(marker, Color.clear).raycastTarget = true;
            var castle = Picture(Fill(marker, "Castle", new Vector2(12, 12), new Vector2(-12, -20)), i == 0 ? data.HomeIcon : data.Castle);
            markers[i] = Add<Button>(marker.gameObject); markers[i].targetGraphic = castle; Theme.Colors(markers[i], Color.white);
            flags[i] = Picture(Top(marker, "Flag", 72, -4, 44, 44), data.Flag);
            flags[i].gameObject.SetActive(i != 0);
            Focus(marker); marker.Find("Focus").GetComponent<Graphic>().color = Theme.Accent;
        }
        var nationMaterial = NationMaterial();
        var nationLabels = new TextMeshProUGUI[data.Nations.Length];
        for (int i = 0; i < nationLabels.Length; i++)
        {
            var label = Label(Child(plane, $"Nation{i}"), data.Nations[i].Name, 32); label.rectTransform.sizeDelta = new Vector2(312, 56);
            label.rectTransform.pivot = new Vector2(.5f, 0); label.rectTransform.anchoredPosition = new Vector2(0, 72); label.alignment = TextAlignmentOptions.Center;
            var backing = label.transform.Find("Backing"); if (backing != null) Object.DestroyImmediate(backing.gameObject);
            var text = Label(Fill(label.transform, "Text"), data.Nations[i].Name, 32, new Color32(255, 252, 239, 255));
            text.fontSharedMaterial = nationMaterial; text.fontStyle = FontStyles.Bold; text.UpdateMeshPadding();
            text.alignment = TextAlignmentOptions.Center;
            label.text = ""; nationLabels[i] = label;
        }
        SetArray(view, "_regions", regions); SetArray(view, "_markers", markers); SetArray(view, "_flags", flags); SetArray(view, "_nations", nationLabels); Set(view, ("_route", route), ("_continent", continent));
        return view;
    }

    private static Material NationMaterial()
    {
        const string path = Folder + "Materials/NotoSansJP-Regular SDF - Country Map.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Materials/NotoSansJP-Regular SDF - Country HUD.mat");
            material = new Material(source) { name = "NotoSansJP-Regular SDF - Country Map" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_OutlineColor", new Color32(19, 29, 38, 255));
        material.SetFloat("_OutlineWidth", .45f); material.SetFloat("_OutlineSoftness", 0);
        material.SetFloat("_FaceDilate", .025f); material.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    internal static CountryRosterConfirm Confirmation(Transform parent, string name, CanvasGroup blocked)
    {
        var root = Instance(parent, name, "CountryRosterConfirm");
        var controller = root.GetComponent<CountryRosterConfirm>();
        var card = (RectTransform)root.Find("Window/Card"); card.sizeDelta = new Vector2(1280, 880);
        card.Find("Body").gameObject.SetActive(false);
        var scroll = Scroll(Fill(card, "BodyScroll", new Vector2(48, 160), new Vector2(-48, -136)));
        var body = Label(Child(scroll.content, "ContentBody"), "", 32); body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.TopLeft;
        Set(controller, ("_blocked", blocked), ("_bodyScroll", scroll), ("_body", body)); PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        return controller;
    }

    internal static void ApplyReport(GameObject root, CountryWorldMapData data)
    {
        var shell = root.transform.Find("Window/Shell"); var page = shell.Find("Main/Income");
        shell.Find("Header/Navigation/Income/TabLabel").GetComponent<TextMeshProUGUI>().text = "報告";
        TopFill(page, "Title", 16, 72, 48, 48).GetComponent<TextMeshProUGUI>().fontSize = 44;
        TopFill(page, "State", 88, 48, 48, 48);
        var resources = TopFill(page, "Resources", 160, 224, 48, 48);
        foreach (Transform item in resources)
        {
            Top(item, "Icon", 24, 28, 80, 80);
            TopFill(item, "Caption", 8, 56, 128, 20).GetComponent<TextMeshProUGUI>().fontSize = 30;
            TopFill(item, "Gain", 64, 96, 128, 20).GetComponent<TextMeshProUGUI>().fontSize = 56;
            var stock = BottomFill(item, "Stock", 56); stock.offsetMin = new Vector2(24, 12); stock.offsetMax = new Vector2(-24, 68);
            stock.GetComponent<TextMeshProUGUI>().fontSize = 26;
            item.Find("Divider").gameObject.SetActive(false);
        }
        Label(TopFill(page, "ReturnTitle", 408, 56, 48, 48), "遠征の帰還", 32, Theme.Accent);
        var list = Scroll(Fill(page, "Returns", new Vector2(48, 16), new Vector2(-48, -480)));
        var empty = Label(TopFill(page, "NoReturns", 496, 80, 72, 72), "帰還した部隊はない", 30, Theme.Muted);
        var details = Confirmation(root.transform.Find("Window"), "ExpeditionDetails", shell.GetComponent<CanvasGroup>());
        var report = Add<CountryExpeditionReportView>(page.gameObject);
        Set(report, ("_data", data), ("_rosterData", AssetDatabase.LoadAssetAtPath<CountryRosterData>("Assets/Data/Country/CountryRosterExample.asset")), ("_content", list.content), ("_empty", empty), ("_details", details),
            ("_rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryExpeditionRow.prefab").GetComponent<CountryExpeditionRow>()));
        Set(root.GetComponent<CountryGovernmentPanel>(), ("_expeditionReport", report));
        CountryMonthlyReportBuilder.Apply(root);
        foreach (var button in root.GetComponentsInChildren<Button>(true)) CountryButtonFeedbackBuilder.Configure(button);
    }

    private static RectTransform Instance(Transform parent, string name, string prefab)
    {
        var root = parent.Find(name) as RectTransform;
        if (root == null) { root = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"), parent)).transform; root.name = name; }
        Stretch(root); return root;
    }
    private static void Board(Transform parent) { var board = Instance(parent, "Workboard", "CountryWorkboard"); board.SetAsFirstSibling(); }
    private static Image Picture(RectTransform rect, Sprite sprite) { var image = Image(rect, Color.white); image.sprite = sprite; image.preserveAspect = true; return image; }
}
