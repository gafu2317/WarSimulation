using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountryDefenseBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private static readonly Color Wine = new Color32(86, 38, 39, 255), Red = new Color32(143, 57, 47, 255), Copper = new Color32(219, 150, 111, 255);

    [MenuItem("WarSim/Country/Set Up Defense")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating defense prefabs.");
        CountrySettingsBuilder.Build();
        SavePrefab("CountryPersonnel", ApplyPersonnel);
        SavePrefab("CountryDefense", BuildWindow);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var hud = scope.prefabContentsRoot.transform;
            var defense = Instance(hud, "Defense", "CountryDefense").GetComponent<CountryDefensePanel>();
            var government = hud.Find("Government").GetComponent<CountryGovernmentPanel>();
            var personnel = hud.Find("Personnel").GetComponent<CountryPersonnelPanel>();
            var expedition = hud.Find("Expeditions").GetComponent<CountryExpeditionPanel>();
            var settings = hud.Find("Settings").GetComponent<CountrySettingsPanel>();
            Set(defense, ("_government", government), ("_personnel", personnel), ("_expeditions", expedition), ("_settings", settings));
            foreach (var controller in new Component[] { government, personnel, expedition, settings })
            { Set(controller, ("_defense", defense)); PrefabUtility.RecordPrefabInstancePropertyModifications(controller); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(defense);
        }
        var map = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        EditorUtility.SetDirty(map); AssetDatabase.SaveAssetIfDirty(map);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        const string glyphs = "防衛編成登録領内敵部隊侵入確認迎撃せよ襲撃発生時間経過停止兵士武器組合全員重複次回自動参加成功退国戻変更選択中訓練復活待遠征帰還持出一人あたり人０１２３４５６７８９CPONOFF−→‹";
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing defense glyphs: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        Debug.Log("Defense, raid popup and settings connected. The open scene was not saved or reloaded.");
    }

    internal static void ApplyPersonnel(GameObject root)
    {
        var header = root.transform.Find("Window/Shell/Header");
        var tab = Button(Right(header, "Defense", 24, 0, 320, 112), "防衛編成", Theme.Panel); Tab(tab);
        tab.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 30;
        tab.transform.Find("Selected").gameObject.SetActive(false);
        Set(root.GetComponent<CountryPersonnelPanel>(), ("_defenseTab", tab));
    }

    private static void BuildWindow(GameObject root)
    {
        Stretch((RectTransform)root.transform);
        var panel = Add<CountryDefensePanel>(root);
        var window = Fill(root.transform, "Window");
        var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 70;
        Add<GraphicRaycaster>(window.gameObject);
        Image(Fill(window, "Dim"), new Color(.06f, .015f, .02f, .84f)).raycastTarget = true;
        var shell = Fill(window, "Shell"); var group = Add<CanvasGroup>(shell.gameObject);
        var formation = Fill(shell, "Formation"); Image(formation, Theme.Background).raycastTarget = true;
        var header = TopFill(formation, "Header", 0, 112); Image(header, Theme.Panel);
        Label(Top(header, "Title", 40, 0, 420, 112), "防衛編成", 44);
        var personnel = Button(Right(header, "Personnel", 40, 0, 320, 112), "兵士一覧へ", Wine);
        var main = Fill(formation, "Main", new Vector2(40, 144), new Vector2(-40, -128));
        var heading = TopFill(main, "Heading", 0, 112);
        var context = Label(Fill(heading, "Context", new Vector2(16, 0), new Vector2(-440, 0)), "防衛メンバーの登録", 36, Theme.Text);
        var equipment = Button(Right(heading, "Equipment", 0, 0, 400, 112), "武器を変更", Wine);
        var selection = CountryRosterSelectionBuilder.Place(main);
        var count = selection.Find("Capacity").GetComponent<TextMeshProUGUI>(); count.color = Theme.Muted;
        var formationBoard = selection.Find("Body/Workboard");
        TintBoard(formationBoard, new Color(.82f, .82f, .82f)); formationBoard.Find("Rim").GetComponent<Image>().color = Color.white;
        formationBoard.Find("Light").gameObject.SetActive(true);
        var view = selection.Find("Body/Roster").GetComponent<CountryRosterView>();
        var members = (RectTransform)selection.Find("Members/Viewport/Content");
        var footer = BottomFill(formation, "Footer", 128); Image(footer, Theme.Panel);
        var back = Button(Top(footer, "Back", 32, 8, 320, 112), "‹ 国へ戻る", Wine);
        var save = Button(Right(footer, "Save", 32, 8, 360, 112), "登録する", Red, true);
        var summary = Label(Fill(footer, "Summary", new Vector2(392, 8), new Vector2(-432, -8)), "", 28, Theme.Muted); summary.textWrappingMode = TextWrappingModes.Normal;

        var alert = Fill(shell, "Alert");
        var card = Child(alert, "Card"); card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
        card.sizeDelta = new Vector2(1440, 760); card.anchoredPosition = Vector2.zero;
        Image(card, new Color32(49, 27, 31, 255)).raycastTarget = true;
        var board = Instance(card, "Workboard", "CountryWorkboard"); board.SetAsFirstSibling(); TintBoard(board, new Color32(168, 103, 99, 255));
        var ribbon = TopFill(card, "Ribbon", 0, 160); Image(ribbon, Wine);
        var icon = Image(Top(ribbon, "Castle", 48, 24, 112, 112), new Color32(249, 215, 182, 255)); icon.preserveAspect = true;
        var data = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset"); icon.sprite = data.Castle;
        Label(Top(ribbon, "Caption", 192, 12, 1160, 48), "襲撃発生", 30, Copper);
        var title = Label(Top(ribbon, "Title", 192, 64, 1160, 76), "領地に襲撃", 48);
        var body = Label(TopFill(card, "Body", 204, 144, 64, 64), "", 38); body.textWrappingMode = TextWrappingModes.Normal;
        var info = Instance(card, "Info", "CountrySectionPlaque");
        info.anchorMin = Vector2.up; info.anchorMax = Vector2.one; info.pivot = Vector2.up;
        info.sizeDelta = new Vector2(-128, 128); info.anchoredPosition = new Vector2(64, -392); TintBoard(info, new Color32(182, 131, 117, 255));
        var raidSummary = Label(Fill(info, "Text", new Vector2(24, 8), new Vector2(-24, -8)), "", 32, Theme.Text); raidSummary.textWrappingMode = TextWrappingModes.Normal;
        var actions = BottomFill(card, "Actions", 160);
        var edit = Button(Top(actions, "Edit", 408, 8, 400, 112), "防衛編成を変更", Wine);
        var fight = Button(Right(actions, "Fight", 48, 8, 480, 112), "防衛する", Red, true);
        fight.transform.Find("Frame").GetComponent<Graphic>().color = Copper;
        var confirm = CountryExpeditionBuilder.Confirmation(window, "Confirm", group);
        var confirmCard = confirm.transform.Find("Window/Card");
        confirmCard.Find("Surface").GetComponent<Graphic>().color = Wine;
        confirmCard.Find("Frame").GetComponent<Graphic>().color = Copper;
        Set(panel, ("_data", data), ("_window", window.gameObject), ("_formation", formation.gameObject), ("_alert", alert.gameObject), ("_shell", group),
            ("_view", view), ("_members", members), ("_context", context), ("_count", count), ("_summary", summary),
            ("_back", back), ("_save", save), ("_saveLabel", save.GetComponentInChildren<TextMeshProUGUI>()), ("_saveFrame", save.transform.Find("Frame").GetComponent<Graphic>()),
            ("_equipment", equipment), ("_personnelBack", personnel), ("_edit", edit), ("_fight", fight), ("_confirmation", confirm),
            ("_raidTitle", title), ("_raidBody", body), ("_raidSummary", raidSummary),
            ("_tokenPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountrySoldierToken.prefab").GetComponent<CountryRosterRow>()),
            ("_emptySlotPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryTrainingEmptySlot.prefab")));
        CountryRaidDispatchBuilder.Apply(root);
        formation.gameObject.SetActive(false); alert.gameObject.SetActive(false); window.gameObject.SetActive(false);
    }

    private static void TintBoard(Transform board, Color color)
    {
        board.Find("Surface").GetComponent<Graphic>().color = color;
        board.Find("Rim").GetComponent<Image>().color = Copper;
        var light = board.Find("Light");
        if (light != null) { light.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(light.gameObject); }
    }
    private static RectTransform Instance(Transform parent, string name, string prefab)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) { rect = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"), parent)).transform; rect.name = name; }
        Stretch(rect); return rect;
    }
}
