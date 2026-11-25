using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Theme = CountryGovernmentTheme;

public static class CountryGovernmentBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string DataPath = "Assets/Data/Country/CountryGovernmentExample.asset";
    private const string FontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";
    private const float Outline = 2.5f;
    internal const float ScrollSensitivity = 18;
    private static readonly Color Background = Theme.Background, Panel = Theme.Panel, Face = Theme.Face;
    private static readonly Color TextColor = Theme.Text, Muted = Theme.Muted, Gold = Theme.Accent;
    private static readonly Color Border = Theme.Border, Recess = Theme.Recess;

    [MenuItem("WarSim/Country/Set Up Government UI")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Country.unity")
            throw new InvalidOperationException("Open the Country scene outside Play mode first.");
        var data = BuildData();
        SavePrefab("CountryGovernmentChoice", BuildChoice);
        SavePrefab("CountryGovernmentAllocationRow", BuildAllocation);
        SavePrefab("CountryGovernment", root => BuildWindow(root, data));
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot.transform;
            var instance = root.Find("Government");
            if (instance == null)
            {
                instance = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryGovernment.prefab"), root)).transform;
                instance.name = "Government";
            }
            var panel = instance.GetComponent<CountryGovernmentPanel>();
            Wire(root.Find("BottomLeftButtons/Buttons/Button1").GetComponent<UnityEngine.UI.Button>(), panel.OpenBudget);
            Wire(root.Find("BottomLeftButtons/Buttons/Button2").GetComponent<UnityEngine.UI.Button>(), panel.OpenTraining);
            Wire(root.Find("BottomLeftButtons/Buttons/Button3").GetComponent<UnityEngine.UI.Button>(), panel.OpenConstruction);
            var constructionLabel = root.Find("BottomLeftButtons/Buttons/Button3").GetComponentInChildren<TextMeshProUGUI>();
            constructionLabel.text = "建設";
            PrefabUtility.RecordPrefabInstancePropertyModifications(constructionLabel);
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) CountryButtonFeedbackBuilder.Configure(button);
        }
        AssetDatabase.SaveAssets();
        var government = Object.FindAnyObjectByType<CountryGovernmentPanel>(FindObjectsInactive.Include);
        var comparison = Object.FindAnyObjectByType<CountryComparisonView>();
        if (government != null && comparison != null)
        {
            var sceneData = new SerializedObject(government);
            foreach (var (field, value) in new (string, Object)[] {
                ("_cameraControls", comparison.viewCamera.GetComponent<CountryCameraController>()),
                ("_comparison", comparison),
                ("_facilityPopup", Object.FindAnyObjectByType<CountryFacilityPopup>(FindObjectsInactive.Include)) })
            {
                var property = sceneData.FindProperty(field);
                if (property.objectReferenceValue == null) property.objectReferenceValue = value;
            }
            sceneData.ApplyModifiedProperties();
        }
        foreach (var oldUI in Object.FindObjectsByType<CountryOperationTestUI>(FindObjectsInactive.Include))
        {
            if (!oldUI.enabled) continue;
            Undo.RecordObject(oldUI, "Disable old Country UI");
            oldUI.enabled = false;
        }
    }

    private static CountryGovernmentData BuildData()
    {
        var data = AssetDatabase.LoadAssetAtPath<CountryGovernmentData>(DataPath);
        if (data != null) return data;
        if (!AssetDatabase.IsValidFolder("Assets/Data/Country")) AssetDatabase.CreateFolder("Assets/Data", "Country");
        data = ScriptableObject.CreateInstance<CountryGovernmentData>();
        data.Facilities = new[] {
            Facility("戦士学校", 1, true, 100, 130, "訓練効果", Cost(350,120,60,2,"この学校でメンバーを訓練できる"), Cost(510,260,150,1,"訓練効果 100% → 120%")),
            Facility("魔法学校", 1, true, 80, 105, "訓練効果", Cost(400,110,80,2,"この学校でメンバーを訓練できる"), Cost(580,170,230,1,"訓練効果 100% → 120%")),
            Facility("教会", 1, true, 60, 75, "訓練効果", Cost(300,100,50,2,"この学校でメンバーを訓練できる"), Cost(340,160,100,1,"訓練効果 100% → 120%")),
            Facility("精霊使役学校", 1, false, 90, 105, "施設効果", Cost(450,130,90,2,"ステータスを上昇させる"), Cost(630,250,255,1,"精霊施設の機能を強化する")),
            Facility("鍛冶場", 1, false, 70, 85, "武器製作の品質", Cost(380,100,80,2,"武器を製作できる"), Cost(500,210,220,1,"製作できる武器の品質が上がる")),
            Facility("図書館", 0, false, 80, 0, "施設効果", Cost(350,320,60,2,"施設の機能を利用できる"), Cost(620,120,60,1,"施設の機能を強化する")),
            Facility("天文台", 0, false, 80, 0, "施設効果", Cost(470,170,175,2,"施設の機能を利用できる"), Cost(680,100,100,1,"施設の機能を強化する"))
        };
        AssetDatabase.CreateAsset(data, DataPath);
        return data;
    }

    private static CountryGovernmentFacility Facility(string name, int level, bool school, int minimum, int funding, string effect, CountryGovernmentCost construction, CountryGovernmentCost upgrade)
        => new() { Name = name, InitialLevel = level, TrainingSchool = school, Minimum = minimum, Funding = funding, EffectName = effect, Construction = construction, Upgrade = upgrade };
    private static CountryGovernmentCost Cost(int money, int wood, int ore, int months, string effect)
        => new() { Money = money, Wood = wood, Ore = ore, Months = months, Effect = effect };

    private static void BuildChoice(GameObject root)
    {
        var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(420, 128);
        Layout(rect, 128);
        var button = Add<UnityEngine.UI.Button>(root);
        var frame = Image(rect, Color.clear); frame.raycastTarget = true;
        button.targetGraphic = Image(Fill(rect, "Surface"), Color.white);
        ButtonColors(button, Panel);
        button.targetGraphic.transform.SetAsFirstSibling();
        var symbol = Symbol(Top(rect, "Symbol", 16, 36, 56, 56), "戦士学校");
        var name = Label(TopFill(rect, "Name", 16, 56, 88, 12), "戦士学校", 36);
        var value = Label(TopFill(rect, "Value", 72, 40, 88, 12), "Lv.1", 26, Gold);
        var marker = Fill(rect, "Selected", Vector2.zero, new Vector2(0, 0));
        marker.anchorMax = new Vector2(0, 1); marker.sizeDelta = new Vector2(4, -28); marker.anchoredPosition = new Vector2(4, 0);
        Image(marker, Gold);
        Image(BottomFill(rect, "Divider", 1), Theme.Line);
        Focus(rect);
        Set(Add<CountryGovernmentChoice>(root), ("_button", button), ("_frame", frame), ("_name", name), ("_value", value), ("_selected", marker.gameObject), ("_symbol", symbol));
        CountryGovernmentControlsBuilder.FacilityChoice(root);
    }

    private static void BuildAllocation(GameObject root)
    {
        var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1840, 224); Layout(rect, 224);
        var layout = Add<UnityEngine.UI.HorizontalLayoutGroup>(root);
        layout.padding = new RectOffset(8, 12, 16, 16); layout.spacing = 40;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
        var identity = Child(rect, "Facility"); Column(identity, 396, 0);
        var symbol = Symbol(Top(identity, "Symbol", 0, 34, 72, 72), "戦士学校");
        var name = Label(TopFill(identity, "Name", 28, 72, 92), "戦士学校", 36);
        var minimum = Label(TopFill(identity, "Minimum", 102, 48, 92), "最低必要額 100", 28, Muted);
        var funding = Child(rect, "Funding"); Column(funding, 500, 1);
        Label(TopFill(funding, "Caption", 0, 56, 0, 160), "配分額", 28, Muted);
        var amount = Label(Right(funding, "Amount", 0, 0, 160, 72), "200", 48, Gold); amount.alignment = TextAlignmentOptions.MidlineRight;
        var slider = Slider(TopFill(funding, "Slider", 48, 112));
        var maximum = Label(TopFill(funding, "Maximum", 146, 44), "配分可能額 350", 26, Muted); maximum.alignment = TextAlignmentOptions.MidlineRight;
        var effect = Child(rect, "Effect"); Column(effect, 520, 1);
        var effectButton = Button(MiddleFill(effect, "Details", 144), "", Face);
        InformationButton(effectButton);
        Label(TopFill(effectButton.transform, "Caption", 8, 40, 24, 136), "施設の役割", 26, Muted);
        var effectText = Label(TopFill(effectButton.transform, "EffectText", 48, 80, 24, 136), "剣・盾の技を学ぶ訓練", 32);
        effectText.textWrappingMode = TextWrappingModes.Normal;
        Hide(effectButton.transform, "DetailLink");
        var more = Label(Right(effectButton.transform, "More", 20, 46, 64, 52), "詳細 ›", 24, Gold); more.alignment = TextAlignmentOptions.Center; more.transform.SetAsLastSibling();
        var divider = BottomFill(rect, "Divider", 1); Image(divider, Theme.Line); Add<UnityEngine.UI.LayoutElement>(divider.gameObject).ignoreLayout = true;
        Set(Add<CountryGovernmentAllocationRow>(root), ("_name", name), ("_minimum", minimum), ("_amount", amount), ("_maximum", maximum), ("_effect", effectText), ("_slider", slider), ("_details", effectButton), ("_symbol", symbol));
        CountryGovernmentArtBuilder.ApplyAllocation(root);
        CountryGovernmentControlsBuilder.Allocation(root);
    }

    private static void BuildWindow(GameObject root, CountryGovernmentData data)
    {
        Stretch(root.GetComponent<RectTransform>());
        var controller = Add<CountryGovernmentPanel>(root);
        var window = Fill(root.transform, "Window");
        var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 50;
        Add<UnityEngine.UI.GraphicRaycaster>(window.gameObject);
        Image(window, Background).raycastTarget = true;
        var shell = Fill(window, "Shell"); var group = Add<CanvasGroup>(shell.gameObject);
        var header = TopFill(shell, "Header", 0, 112); Image(header, Panel);
        var headerLayout = Add<UnityEngine.UI.HorizontalLayoutGroup>(header.gameObject);
        headerLayout.padding = new RectOffset(32, 32, 0, 0); headerLayout.spacing = 16;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true; headerLayout.childForceExpandWidth = false; headerLayout.childForceExpandHeight = true;
        var date = Label(Child(header, "Date"), "1月1日", 34, Gold); Column(date.rectTransform, 192, 0);
        var navigation = Child(header, "Navigation"); Column(navigation, 900, 1); Horizontal(navigation, 8);
        var navLayout = navigation.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(); navLayout.padding = new RectOffset();
        string[] names = { "Income", "Budget", "Construction", "Training" }, labels = { "収入", "配分", "建設", "育成" }, icons = { "budget-v1", "budget-v1", "construction-v1", "training-v1" };
        var tabs = new UnityEngine.UI.Button[4];
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = Child(navigation, names[i]); Column(rect, 208, 0);
            tabs[i] = Button(rect, "", Panel);
            Tab(tabs[i]);
            Icon(Top(rect, "Icon", 24, 32, 48, 48), icons[i]);
            Label(Fill(rect, "TabLabel", new Vector2(88, 0), new Vector2(-16, 0)), labels[i], 34);
        }
        var wallet = Child(header, "Treasury"); Column(wallet, 224, 0);
        Icon(Top(wallet, "Icon", 0, 30, 52, 52), "budget-v1");
        var treasuryCaption = Label(Top(wallet, "Caption", 64, 6, 160, 44), "計画後国庫", 26, Muted);
        Set(controller, ("_treasuryCaption", treasuryCaption));
        var treasury = Label(Top(wallet, "Value", 64, 44, 160, 68), "0", 42);
        var main = Fill(shell, "Main", new Vector2(0, 128), new Vector2(0, -112));
        var pages = names.Select(name => Fill(main, name).gameObject).ToArray();
        BuildIncome(pages[0].transform, controller);
        BuildBudget(pages[1].transform, controller);
        BuildConstruction(pages[2].transform, controller);
        BuildTraining(pages[3].transform, controller);
        var footer = BottomFill(shell, "Footer", 128); Image(footer, Panel);
        Image(TopFill(footer, "Edge", 0, 1), Theme.Line);
        var back = PageButton(Child(footer, "Back"), Theme.CountryReturnText);
        Theme.PlacePageItem((RectTransform)back.transform, 0, Theme.PageBarInset, Theme.PageBarBackWidth);
        var confirm = Button(Right(footer, "Confirm", 32, 8, 344, 112), "配分を確定", Theme.Selected, true);
        var next = footer.Find("Next");
        if (next != null) Object.DestroyImmediate(next.gameObject);
        var status = Label(Fill(footer, "Status", new Vector2(292, 8), new Vector2(-408, -8)), "", 28, Muted); status.textWrappingMode = TextWrappingModes.Normal;
        BuildDetails(window, controller);
        CountryGovernmentArtBuilder.Apply(root);
        Set(controller, ("_data", data), ("_window", window.gameObject), ("_shell", group), ("_dateText", date), ("_treasuryText", treasury), ("_status", status),
            ("_backButton", back), ("_confirmButton", confirm), ("_confirmFrame", confirm.transform.Find("Frame").GetComponent<CountryGovernmentPlate>()), ("_confirmLabel", confirm.GetComponentInChildren<TextMeshProUGUI>()),
            ("_allocationPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryGovernmentAllocationRow.prefab").GetComponent<CountryGovernmentAllocationRow>()),
            ("_choicePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryGovernmentChoice.prefab").GetComponent<CountryGovernmentChoice>()));
        SetArray(controller, "_tabs", tabs); SetArray(controller, "_pages", pages);
        CountryGovernmentControlsBuilder.Government(root);
        string glyphs = string.Concat(root.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text))
            + string.Concat(data.Facilities.Select(f => f.Name + f.EffectName + f.Construction.Effect + f.Upgrade.Effect))
            + "受取済みまだはない月分使用中税収内戻確定計画案保存不足資金必要人追加種類今回変化閉じる未建設強化訓練停止効果の配分政務終える0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ%＋→／・（）〈〉か月✓›‹";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (!font.HasCharacters(glyphs) && !font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing government glyphs: " + missing);
        EditorUtility.SetDirty(font);
        foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) CountryButtonFeedbackBuilder.Configure(button);
        window.gameObject.SetActive(false);
    }

    private static void BuildIncome(Transform root, CountryGovernmentPanel panel)
    {
        Image((RectTransform)root, Background);
        var heading = Label(TopFill(root, "Title", 72, 80, 72, 72), "1月の収入", 48);
        var state = Label(TopFill(root, "State", 152, 56, 72, 72), "まだ受取はない", 30, Muted);
        var row = Fill(root, "Resources", new Vector2(72, 140), new Vector2(-72, -264)); Horizontal(row, 32, true);
        var gains = new TextMeshProUGUI[3]; var stocks = new TextMeshProUGUI[3];
        string[] names = { "Tax", "Wood", "Ore" }, labels = { "今月の税収", "木材", "鉱石" }, icons = { "budget-v1", "wood-v1", "ore-v1" };
        for (int i = 0; i < 3; i++)
        {
            var item = Child(row, names[i]); Column(item, 420, 1);
            Plate(Fill(item, "Surface"), Panel, 6).transform.SetAsFirstSibling();
            var edge = TopFill(item, "Accent", 0, 3, 32, 32); Image(edge, i == 0 ? Gold : Theme.Border);
            Icon(Top(item, "Icon", 32, 64, 120, 120), icons[i]);
            Label(TopFill(item, "Caption", 56, 56, 180, 24), labels[i], 34);
            gains[i] = Label(TopFill(item, "Gain", 100, 132, 180, 24), "＋0", 68, i == 0 ? Gold : Theme.Positive);
            var divider = BottomFill(item, "Divider", 1); divider.anchoredPosition = new Vector2(32, 136); divider.sizeDelta = new Vector2(-64, 1); Image(divider, Theme.Line);
            var stock = BottomFill(item, "Stock", 104); stock.anchoredPosition = new Vector2(32, 20); stock.sizeDelta = new Vector2(-64, 104);
            stocks[i] = Label(stock, i == 0 ? "国庫 0" : "0", 28, Muted);
        }
        Set(panel, ("_incomeTitle", heading), ("_incomeState", state), ("_taxGain", gains[0]), ("_woodGain", gains[1]), ("_oreGain", gains[2]), ("_taxStock", stocks[0]), ("_woodStock", stocks[1]), ("_oreStock", stocks[2]));
        var world = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        if (world != null) CountryExpeditionBuilder.ApplyReport(panel.gameObject, world);
    }

    private static void BuildBudget(Transform root, CountryGovernmentPanel panel)
    {
        PageGuide(root, "予算配分", "金額を調整し、適用月を確認して決定");
        root.Find("Guide").gameObject.SetActive(false);
        var bar = TopFill(root, "Summary", 0, 112, 40, 40); Image(bar, Panel);
        Icon(Top(bar, "TaxIcon", 24, 30, 52, 52), "budget-v1");
        var tax = Label(Top(bar, "Tax", 96, 0, 344, 112), "今月の税収 0", 34);
        var total = Label(Top(bar, "Total", 480, 0, 280, 112), "配分 0", 34);
        var flow = Label(Top(bar, "Remainder", 800, 0, 360, 112), "国庫へ 0", 34, Gold);
        var toggleRect = Right(bar, "UseTreasury", 32, 0, 360, 112);
        var toggleHit = Image(toggleRect, new Color(1, 1, 1, .001f)); toggleHit.sprite = null; toggleHit.raycastTarget = true;
        var toggle = Add<UnityEngine.UI.Toggle>(toggleRect.gameObject);
        var box = Top(toggleRect, "Box", 0, 34, 44, 44); var boxImage = Image(box, Border); boxImage.raycastTarget = true; toggle.targetGraphic = boxImage;
        Image(Fill(box, "Inset", Vector2.one * Outline, Vector2.one * -Outline), Recess).transform.SetAsFirstSibling();
        toggle.graphic = CheckMark(box);
        var toggleLabel = Label(Fill(toggleRect, "Label", new Vector2(64, 0), Vector2.zero), "国庫も使う OFF", 30);
        var scroll = Scroll(Fill(root, "Facilities", new Vector2(40, 24), new Vector2(-40, -136)));
        Set(panel, ("_fundingBasis", root.Find("Guide/Instruction").GetComponent<TextMeshProUGUI>()), ("_budgetTitle", root.Find("Guide/Title").GetComponent<TextMeshProUGUI>()), ("_taxBudget", tax), ("_fundingTotal", total), ("_cashFlow", flow), ("_useTreasury", toggle), ("_treasuryToggleLabel", toggleLabel), ("_budgetContent", scroll.content));
    }

    private static void BuildConstruction(Transform root, CountryGovernmentPanel panel)
    {
        PageGuide(root, "建設", "施設を選び、費用・工期を確認して決定");
        var modes = TopFill(root, "Modes", 96, 112, 40, 40); Image(modes, Color.clear);
        Label(Top(modes, "Caption", 0, 0, 172, 112), "建設の種類", 28, Muted);
        Hide(modes, "SelectorFrame");
        var upgrade = Button(Top(modes, "Upgrade", 184, 0, 220, 112), "強化", Panel); Tab(upgrade, true);
        var build = Button(Top(modes, "New", 412, 0, 220, 112), "新築", Panel); Tab(build, true);
        var count = Label(Right(modes, "Count", 0, 0, 300, 112), "建設計画 0件", 28, Muted); count.alignment = TextAlignmentOptions.MidlineRight;
        var split = Fill(root, "Content", new Vector2(40, 24), new Vector2(-40, -232));
        var left = LeftColumn(split);
        Label(TopFill(left, "Caption", 0, 56, 0, 20), "施設を選ぶ", 28, Muted);
        var scroll = Scroll(Fill(left, "FacilityList", Vector2.zero, new Vector2(-20, -72)));
        var detail = RightColumn(split);
        Plate(Fill(detail, "Surface"), Panel, 0).transform.SetAsFirstSibling();
        var content = Fill(detail, "Detail", new Vector2(32, 24), new Vector2(-32, -24));
        var layout = Add<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject); layout.spacing = 24; layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var titleRow = Child(content, "Title"); Layout(titleRow, 104);
        var symbol = Symbol(Top(titleRow, "Symbol", 0, 4, 88, 88), "戦士学校");
        var title = Label(Fill(titleRow, "Name", new Vector2(116, 0), new Vector2(-300, 0)), "戦士学校", 44);
        var level = Label(Right(titleRow, "Level", 0, 0, 300, 104), "Lv.1 → Lv.2", 32, Gold); level.alignment = TextAlignmentOptions.MidlineRight;
        var costs = Child(content, "Costs"); Layout(costs, 152); Horizontal(costs, 20, true);
        var values = new TextMeshProUGUI[4]; string[] captions = { "費用", "木材", "鉱石", "工期" }, icons = { "budget-v1", "wood-v1", "ore-v1" };
        for (int i = 0; i < 4; i++)
        {
            var item = Child(costs, "Cost" + i); Column(item, 220, 1);
            if (i < 3) Icon(Top(item, "Icon", 0, 60, 56, 56), icons[i]);
            else Symbol(Top(item, "Icon", 0, 60, 56, 56), "工期");
            Label(TopFill(item, "Caption", 0, 48), captions[i], 28, Muted);
            values[i] = Label(TopFill(item, "Value", 52, 80, 76), i == 3 ? "1か月" : "0", 44, i == 3 ? Gold : TextColor);
            Image(BottomFill(item, "Divider", 1), Theme.Line);
        }
        var effect = Button(Child(content, "Effect"), "", Face); Layout((RectTransform)effect.transform, 152);
        InformationButton(effect); Hide(effect.transform, "DetailLink");
        Label(TopFill(effect.transform, "Caption", 10, 48, 24, 112), "完成後の効果", 28, Muted);
        var effectLabel = Label(TopFill(effect.transform, "Description", 62, 76, 24, 112), "施設の機能を強化する", 34); effectLabel.textWrappingMode = TextWrappingModes.Normal;
        var more = Label(Right(effect.transform, "More", 20, 50, 80, 52), "詳細 ›", 26, Gold); more.alignment = TextAlignmentOptions.Center; more.transform.SetAsLastSibling();
        var empty = Label(Fill(detail, "Empty"), "強化できる施設はない", 40, Muted);
        Set(panel, ("_constructionTitle", root.Find("Guide/Title").GetComponent<TextMeshProUGUI>()), ("_upgradeButton", upgrade), ("_newButton", build), ("_constructionContent", scroll.content), ("_constructionScroll", scroll),
            ("_constructionName", title), ("_constructionSymbol", symbol), ("_level", level), ("_moneyCost", values[0]), ("_woodCost", values[1]), ("_oreCost", values[2]), ("_months", values[3]),
            ("_constructionEffect", effectLabel), ("_orderCount", count), ("_constructionDetailsButton", effect), ("_constructionDetail", content.gameObject), ("_constructionEmpty", empty));
    }

    private static void BuildTraining(Transform root, CountryGovernmentPanel panel) => CountrySoldierBuilder.ApplyTraining(root, panel);

    private static void PageGuide(Transform root, string title, string instruction)
    {
        var guide = TopFill(root, "Guide", 0, 96, 40, 40);
        Label(Top(guide, "Title", 0, 0, 360, 96), title, 36);
        var hint = Label(Fill(guide, "Instruction", new Vector2(384, 0), Vector2.zero), instruction, 28, Muted);
        hint.alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void BuildDetails(Transform root, CountryGovernmentPanel panel)
    {
        var modal = Fill(root, "Details");
        var dimRect = Fill(modal, "Dim");
        var dim = Image(dimRect, new Color(.04f, .08f, .12f, .82f)); dim.raycastTarget = true;
        var dismiss = Add<UnityEngine.UI.Button>(dimRect.gameObject); dismiss.targetGraphic = dim; dismiss.transition = UnityEngine.UI.Selectable.Transition.None;
        Wire(dismiss, panel.CloseDetails);
        var card = Child(modal, "Card"); card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(1320, 720);
        Image(card, Border).raycastTarget = true;
        var surface = Fill(card, "Surface", Vector2.one * Outline, Vector2.one * -Outline);
        Plate(surface, Panel, 0).transform.SetAsFirstSibling();
        Image(TopFill(card, "Accent", 0, 3, 48, 48), Gold);
        var title = Label(TopFill(card, "Title", 32, 88, 48, 48), "配分の効果", 48);
        var body = Label(Fill(card, "Body", new Vector2(48, 160), new Vector2(-48, -144)), "", 36);
        body.alignment = TextAlignmentOptions.TopLeft; body.textWrappingMode = TextWrappingModes.Normal;
        var close = Button(Right(card, "Close", 48, 576, 200, 112), "閉じる", Face);
        Set(panel, ("_detailModal", modal.gameObject), ("_detailTitle", title), ("_detailBody", body), ("_detailClose", close));
        modal.gameObject.SetActive(false);
    }

    internal static UnityEngine.UI.ScrollRect Scroll(RectTransform root, bool verticalLayout = true)
    {
        var scroll = Add<UnityEngine.UI.ScrollRect>(root.gameObject); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = ScrollSensitivity;
        var viewport = Fill(root, "Viewport", Vector2.zero, new Vector2(-28, 0));
        var maskImage = Image(viewport, Color.white); maskImage.sprite = null; maskImage.raycastTarget = true;
        Add<UnityEngine.UI.Mask>(viewport.gameObject).showMaskGraphic = false;
        var content = Child(viewport, "Content"); content.anchorMin = Vector2.up; content.anchorMax = Vector2.one; content.pivot = Vector2.up;
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var fitter = Add<UnityEngine.UI.ContentSizeFitter>(content.gameObject); fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        if (verticalLayout)
        {
            var layout = Add<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject); layout.spacing = 12; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        }
        var barRect = Fill(root, "Scrollbar"); barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = Vector2.one; barRect.pivot = Vector2.one;
        barRect.sizeDelta = new Vector2(20, 0); barRect.anchoredPosition = Vector2.zero;
        Image(barRect, new Color(1, 1, 1, .001f)).raycastTarget = true;
        var handle = Fill(barRect, "Handle", new Vector2(7, 3), new Vector2(-7, -3)); var handleImage = Image(handle, Border); handleImage.raycastTarget = true;
        var bar = Add<UnityEngine.UI.Scrollbar>(barRect.gameObject); bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = handleImage;
        scroll.viewport = viewport; scroll.content = content; scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private static UnityEngine.UI.Slider Slider(RectTransform root)
    {
        var hit = Image(root, new Color(1, 1, 1, .001f)); hit.sprite = null; hit.raycastTarget = true;
        RoundedImage(MiddleFill(root, "Track", 6), Theme.Line);
        var fillArea = MiddleFill(root, "FillArea", 6);
        var fill = RoundedImage(Fill(fillArea, "Fill"), Gold);
        var handleArea = MiddleFill(root, "HandleArea", 0); handleArea.sizeDelta = new Vector2(-28, 0);
        var handle = Child(handleArea, "Handle"); handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0, .5f); handle.sizeDelta = new Vector2(28, 44);
        Plate(handle, Gold, 6).raycastTarget = true;
        var handleImage = Plate(Fill(handle, "Surface", Vector2.one * 1.5f, Vector2.one * -1.5f), Color.white, 5);
        Hide(handle, "Grip");
        var slider = Add<UnityEngine.UI.Slider>(root.gameObject); slider.fillRect = fill.rectTransform; slider.handleRect = handle;
        slider.targetGraphic = handleImage; slider.wholeNumbers = true; slider.minValue = 0; slider.maxValue = 100;
        Theme.Colors(slider, Gold);
        CountryGovernmentControlsBuilder.Slider(root);
        return slider;
    }

    internal static void SavePrefab(string name, Action<GameObject> build)
    {
        string path = Folder + "/" + name + ".prefab";
        bool exists = File.Exists(path); var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name, typeof(RectTransform));
        try {
            root.layer = 5; build(root); CountryTextLayoutBuilder.Apply(name, root); CountryPaletteBuilder.Apply(root);
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) CountryButtonFeedbackBuilder.Configure(button);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }
    internal static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction callback)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == (callback.Target as Object) && button.onClick.GetPersistentMethodName(i) == callback.Method.Name) return;
        UnityEventTools.AddPersistentListener(button.onClick, callback);
    }
    internal static T Add<T>(GameObject root) where T : Component => root.TryGetComponent<T>(out var component) ? component : root.AddComponent<T>();
    internal static RectTransform Child(Transform parent, string name)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; root.transform.SetParent(parent, false); return (RectTransform)root.transform;
    }
    internal static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
    internal static RectTransform Fill(Transform parent, string name, Vector2 min = default, Vector2 max = default) { var rect = Child(parent, name); Stretch(rect); rect.offsetMin = min; rect.offsetMax = max; return rect; }
    internal static RectTransform Top(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = Child(parent, name); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up; rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    internal static RectTransform Right(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = Child(parent, name); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    internal static RectTransform TopFill(Transform parent, string name, float y, float height, float left = 0, float right = 0)
    {
        var rect = Child(parent, name); rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = Vector2.up; rect.sizeDelta = new Vector2(-left - right, height); rect.anchoredPosition = new Vector2(left, -y); return rect;
    }
    internal static RectTransform BottomFill(Transform parent, string name, float height)
    {
        var rect = Child(parent, name); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = Vector2.zero; rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = Vector2.zero; return rect;
    }
    private static RectTransform MiddleFill(Transform parent, string name, float height)
    {
        var rect = Child(parent, name); rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = Vector2.zero; return rect;
    }
    internal static RectTransform LeftColumn(Transform parent) { var rect = Fill(parent, "Left"); rect.anchorMax = new Vector2(.25f, 1); rect.offsetMax = new Vector2(-24, 0); return rect; }
    internal static RectTransform RightColumn(Transform parent) { var rect = Fill(parent, "Right"); rect.anchorMin = new Vector2(.25f, 0); rect.offsetMin = new Vector2(24, 0); return rect; }
    internal static void Layout(RectTransform rect, float height) { var layout = Add<UnityEngine.UI.LayoutElement>(rect.gameObject); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
    internal static void Column(RectTransform rect, float width, float flexible) { var layout = Add<UnityEngine.UI.LayoutElement>(rect.gameObject); layout.minWidth = width; layout.preferredWidth = width; layout.flexibleWidth = flexible; }
    internal static void Horizontal(RectTransform rect, float spacing, bool expand = false)
    {
        var layout = Add<UnityEngine.UI.HorizontalLayoutGroup>(rect.gameObject); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = expand; layout.childForceExpandHeight = true;
    }
    internal static UnityEngine.UI.Image Image(RectTransform rect, Color tint)
    {
        var image = Add<UnityEngine.UI.Image>(rect.gameObject); image.sprite = null;
        image.type = UnityEngine.UI.Image.Type.Simple; image.color = tint; image.raycastTarget = false; return image;
    }
    private static UnityEngine.UI.Image RoundedImage(RectTransform rect, Color tint)
    {
        var image = Image(rect, tint); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Shapes/RoundedPanel.png");
        image.type = UnityEngine.UI.Image.Type.Sliced; return image;
    }
    internal static CountryGovernmentPlate Plate(RectTransform rect, Color tint, float cut = 6, float stroke = 0)
    {
        if (rect.TryGetComponent<UnityEngine.UI.Image>(out var image)) Object.DestroyImmediate(image);
        Add<CanvasRenderer>(rect.gameObject);
        var plate = Add<CountryGovernmentPlate>(rect.gameObject);
        plate.color = tint; plate.raycastTarget = false; plate.Configure(cut, stroke);
        return plate;
    }
    private static CountryFacilitySymbol Symbol(RectTransform rect, string facility)
    {
        Add<CanvasRenderer>(rect.gameObject);
        var symbol = Add<CountryFacilitySymbol>(rect.gameObject); symbol.color = Gold; symbol.raycastTarget = false; symbol.SetFacility(facility);
        return symbol;
    }
    private static void Hide(Transform root, string name) { var child = root.Find(name); if (child != null) child.gameObject.SetActive(false); }
    internal static void Focus(RectTransform rect, float cut = 0)
    {
        var focus = Plate(Fill(rect, "Focus"), new Color32(169, 188, 210, 255), cut, Outline);
        focus.enabled = false; focus.transform.SetAsLastSibling();
    }
    private static void Icon(RectTransform rect, string name)
    {
        var image = Image(rect, Color.white); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/" + name + ".png"); image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true;
    }
    internal static TextMeshProUGUI Label(RectTransform rect, string text, float size, Color? tint = null)
    {
        var label = Add<TextMeshProUGUI>(rect.gameObject); label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/NotoSansJP-Regular SDF - Country HUD.mat");
        label.text = text; label.fontSize = size; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.color = tint ?? TextColor; label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }
    internal static UnityEngine.UI.Button Button(RectTransform rect, string text, Color color, bool primary = false)
    {
        var image = Image(rect, Color.clear); image.raycastTarget = true;
        var face = Fill(rect, "Surface", new Vector2(Outline, 8 + Outline), new Vector2(-Outline, -8 - Outline));
        UnityEngine.UI.Graphic graphic;
        if (primary)
        {
            var shadow = Fill(rect, "Shadow", new Vector2(0, 5), new Vector2(0, -11));
            Plate(shadow, Recess, 0);
            var frame = Fill(rect, "Frame", new Vector2(0, 8), new Vector2(0, -8)); Plate(frame, Gold, 0);
            graphic = Plate(face, Color.white, 0);
            shadow.SetAsFirstSibling(); frame.SetSiblingIndex(1); face.SetSiblingIndex(2);
        }
        else
        {
            graphic = Image(face, Color.white); face.SetAsFirstSibling();
            Plate(Fill(rect, "Outline", new Vector2(0, 8), new Vector2(0, -8)), Theme.Line, 0, Outline);
        }
        var button = Add<UnityEngine.UI.Button>(rect.gameObject); button.targetGraphic = graphic; ButtonColors(button, color);
        var label = Label(Fill(rect, "Label", new Vector2(18, 8), new Vector2(-18, -8)), text, 36); label.alignment = TextAlignmentOptions.Center;
        Focus(rect);
        Fill(rect, "Focus", new Vector2(0, 8), new Vector2(0, -8));
        return button;
    }
    internal static UnityEngine.UI.Button PageButton(RectTransform rect, string text)
    {
        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Shapes/RoundedPanel.png");
        foreach (string name in new[] { "Shadow", "Outline", "Icon" })
            if (rect.Find(name) is Transform part) Object.DestroyImmediate(part.gameObject);
        Image(rect, Color.clear).raycastTarget = true;
        var frame = Picture(Fill(rect, "Frame", new Vector2(0, 8), new Vector2(0, -8)), rounded);
        frame.color = Theme.Border; frame.gameObject.SetActive(true); frame.transform.SetAsFirstSibling();
        var focus = Picture(Fill(rect, "Focus", new Vector2(0, 8), new Vector2(0, -8)), rounded);
        focus.color = Theme.Accent; focus.enabled = false; focus.transform.SetSiblingIndex(1);
        var face = Picture(Fill(rect, "Surface", new Vector2(3, 11), new Vector2(-3, -11)), rounded);
        face.transform.SetSiblingIndex(2);
        var label = Label(Fill(rect, "Label", new Vector2(8, 8), new Vector2(-8, -8)), text, 32, Theme.Ink);
        label.alignment = TextAlignmentOptions.Center; label.transform.SetAsLastSibling();
        var button = Add<UnityEngine.UI.Button>(rect.gameObject);
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        button.targetGraphic = face; Theme.Colors(button, Theme.Face);
        CountryButtonFeedbackBuilder.Configure(button);
        return button;
    }

    internal static UnityEngine.UI.Image Picture(RectTransform rect, Sprite sprite)
    {
        foreach (var graphic in rect.GetComponents<UnityEngine.UI.Graphic>().Where(g => g is not UnityEngine.UI.Image).ToArray())
            Object.DestroyImmediate(graphic);
        var image = Image(rect, Color.white); image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4; image.raycastTarget = false;
        return image;
    }

    internal static void Tab(UnityEngine.UI.Button button, bool inset = false)
    {
        var rect = (RectTransform)button.transform;
        button.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        Hide(rect, "Outline");
        Fill(rect, "Surface", new Vector2(0, 12), new Vector2(0, -12));
        var selected = BottomFill(rect, "Selected", 3); Image(selected, Gold); selected.anchoredPosition = new Vector2(0, 12);
        Hide(rect, "Separator");
        Focus(rect);
        Fill(rect, "Focus", new Vector2(0, 12), new Vector2(0, -12));
        CountryGovernmentControlsBuilder.TabArtwork(button);
    }
    private static void InformationButton(UnityEngine.UI.Button button)
    {
        button.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        Hide(button.transform, "Outline");
        var face = Fill(button.transform, "Surface"); face.SetAsFirstSibling();
        Hide(button.transform, "InformationEdge");
    }
    private static UnityEngine.UI.Graphic CheckMark(Transform parent)
    {
        var rect = Fill(parent, "Checked", Vector2.one * 4, Vector2.one * -4);
        if (rect.TryGetComponent<UnityEngine.UI.Image>(out var old)) Object.DestroyImmediate(old);
        if (rect.TryGetComponent<TextMeshProUGUI>(out var text)) Object.DestroyImmediate(text);
        return Symbol(rect, "選択");
    }
    private static void ButtonColors(UnityEngine.UI.Button button, Color normal)
    {
        Theme.Colors(button, normal);
    }
    internal static void Set(Object target, params (string field, Object value)[] fields)
    {
        var data = new SerializedObject(target); foreach (var field in fields) data.FindProperty(field.field).objectReferenceValue = field.value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    internal static void SetArray(Object target, string field, Object[] values)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
