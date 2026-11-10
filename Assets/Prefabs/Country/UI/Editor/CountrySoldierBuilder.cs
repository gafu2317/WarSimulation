using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;
using Theme = CountryGovernmentTheme;

public static class CountrySoldierBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string DataPath = "Assets/Data/Country/CountryRosterExample.asset";

    [MenuItem("WarSim/Country/Set Up Soldier UI")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing prefabs.");
        EnsureAssets();
        RemoveDeveloperNotes();
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryGovernment.prefab"))
        {
            var root = scope.prefabContentsRoot;
            foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
                scroll.scrollSensitivity = ScrollSensitivity;
            Set(root.GetComponent<CountryGovernmentPanel>(),
                ("_budgetTitle", root.transform.Find("Window/Shell/Main/Budget/Guide/Title").GetComponent<TextMeshProUGUI>()),
                ("_constructionTitle", root.transform.Find("Window/Shell/Main/Construction/Guide/Title").GetComponent<TextMeshProUGUI>()));
            root.transform.Find("Window/Shell/Main/Budget/Guide/Instruction").GetComponent<TextMeshProUGUI>().text = "金額を調整し、適用月を確認して決定";
            ApplyTraining(root.transform.Find("Window/Shell/Main/Training"), root.GetComponent<CountryGovernmentPanel>());
            root.transform.Find("Window/Shell/Footer/Confirm").GetComponentInChildren<TextMeshProUGUI>().fontSize = 36;
        }
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryGovernmentAllocationRow.prefab"))
            scope.prefabContentsRoot.transform.Find("Funding/Caption").GetComponent<TextMeshProUGUI>().text = "配分額";
        SavePrefab("CountryPersonnel", BuildPersonnel);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryHud.prefab"))
        {
            var hud = scope.prefabContentsRoot.transform;
            var root = Instance(hud, "Personnel", "CountryPersonnel");
            var personnel = root.GetComponent<CountryPersonnelPanel>();
            Set(personnel, ("_government", hud.Find("Government").GetComponent<CountryGovernmentPanel>()));
            PrefabUtility.RecordPrefabInstancePropertyModifications(personnel);
            Wire(hud.Find("BottomLeftButtons/Buttons/Button4").GetComponent<UnityEngine.UI.Button>(), personnel.OpenSoldiers);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        string glyphs = "合計件点来月適用予約当見込着工解除技持替使用可能装備中ステータス最大基礎内訳兵士武器一覧装備変更所持未設定待機中復活予定訓練対象加選択外開始前完了定員人期間途中不可国戻付替年月日能力値初期成長補正習得済使用可能現在剣盾杖魔導書聖ロザリオ個別名前配置No.CP0123456789HPSTRINTFAIAGI●・／＋→〈〉（）準備確認基本学戦魔法教会学校税予算調整研磨見習練";
        var data = BuildData();
        foreach (var weapon in data.Weapons)
        {
            if (weapon.Name.StartsWith("試作", StringComparison.Ordinal)) weapon.Name = weapon.Name.Substring(2);
            if (weapon.Image == WeaponIcon(weapon.Kind)) weapon.Image = null;
        }
        EditorUtility.SetDirty(data);
        glyphs += string.Concat(data.Skills.Definitions.Where(skill => skill != null).Select(skill => data.SkillName(skill.SkillId)));
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing soldier glyphs: " + missing);
            EditorUtility.SetDirty(font);
        }
        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<CountryGovernmentData>("Assets/Data/Country/CountryGovernmentExample.asset"));
        AssetDatabase.SaveAssets();
        Debug.Log("Soldier UI prefabs updated. The open scene was not saved or reloaded.");
    }

    private static void EnsureAssets()
    {
        BuildData();
        SavePrefab("CountryRosterRow", root => BuildRow(root, false));
        SavePrefab("CountrySoldierToken", root => BuildRow(root, true));
        SavePrefab("CountryTrainingEmptySlot", BuildEmptySlot);
        SavePrefab("CountryRosterConfirm", BuildConfirm);
        SavePrefab("CountryRosterView", BuildView);
        CountryRosterSelectionBuilder.Ensure();
        SavePrefab("CountryTraining", BuildTraining);
    }

    private static CountryRosterData BuildData()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CountryRosterData>(DataPath);
        if (existing != null) return existing;
        var data = ScriptableObject.CreateInstance<CountryRosterData>();
        data.Skills = AssetDatabase.LoadAssetAtPath<CombatSkillCatalog>("Assets/Data/Combat/Skills/CombatSkillCatalog.asset");
        data.Soldiers = new CountrySoldierDefinition[26]; data.Weapons = new CountryWeaponDefinition[26];
        for (int i = 0; i < 26; i++)
        {
            var kind = (WeaponKind)(i % 6 + 1);
            string id = $"weapon-{i + 1:00}";
            data.Weapons[i] = new CountryWeaponDefinition {
                Id = id, Name = $"{CountryRoster.WeaponName(kind)}{i / 6 + 1:00}", Kind = kind,
                Bonus = new CountrySoldierStats { HP = i % 4 * 2, STR = i % 5 + 1, INT = (i + 2) % 5, FAI = (i + 3) % 4, AGI = i % 3 }
            };
            data.Soldiers[i] = new CountrySoldierDefinition {
                Id = $"soldier-{i + 1:00}", Name = $"兵士{i + 1:00}", InitialWeaponId = id, MaxCP = 100,
                InitialStats = new CountrySoldierStats { HP = 100 + i * 3, STR = 10 + i % 7, INT = 9 + i % 5, FAI = 8 + i % 6, AGI = 10 + i % 4 }
            };
        }
        data.Schools = new[] {
            new CountrySchoolDefinition { Name = "戦士学校", Facility = FacilityType.WarriorSchool, FirstWeapon = WeaponKind.Sword, SecondWeapon = WeaponKind.Shield,
                FirstSkills = new[] { SkillId.Sword_QuickSlash, SkillId.Sword_StrongSlash, SkillId.None, SkillId.None, SkillId.None },
                SecondSkills = new[] { SkillId.Shield_ShoulderGuard, SkillId.Shield_IronWall, SkillId.Shield_Taunt, SkillId.None, SkillId.None } },
            new CountrySchoolDefinition { Name = "魔法学校", Facility = FacilityType.MagicSchool, FirstWeapon = WeaponKind.Wand, SecondWeapon = WeaponKind.Grimoire,
                FirstSkills = new[] { SkillId.Wand_ArcaneBlast, SkillId.Wand_AreaBlast, SkillId.Wand_GodsHand, SkillId.None, SkillId.None },
                SecondSkills = new[] { SkillId.Grimoire_StrDebuff, SkillId.StatDebuff_AGI, SkillId.Grimoire_Bind, SkillId.Grimoire_Poison, SkillId.Grimoire_Stealth } },
            new CountrySchoolDefinition { Name = "教会", Facility = FacilityType.Church, FirstWeapon = WeaponKind.Bible, SecondWeapon = WeaponKind.Rosary,
                FirstSkills = new[] { SkillId.Bible_StrBuff, SkillId.Bible_FaiBuff, SkillId.Bible_IntBuff, SkillId.Bible_Invulnerable, SkillId.Bible_Gotsume },
                SecondSkills = new[] { SkillId.Rosary_DistantHeal, SkillId.Rosary_CloseHeal, SkillId.Rosary_Regeneration, SkillId.Rosary_HealingArea, SkillId.Rosary_SacrificeThunder } }
        };
        AssetDatabase.CreateAsset(data, DataPath);
        return data;
    }

    private static Sprite WeaponIcon(WeaponKind kind)
    {
        string name = kind switch { WeaponKind.Sword => "sword", WeaponKind.Shield => "shield", WeaponKind.Wand => "staff", WeaponKind.Grimoire => "grimoire", WeaponKind.Bible => "holybook", _ => "rosary" };
        return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Images/Icons/{name}.png");
    }

    private static void BuildRow(GameObject root, bool token)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(token ? 264 : 500, 128);
        Layout(rect, 128); if (token) Column(rect, 264, 0);
        var button = Button(rect, "", Theme.Panel);
        root.transform.Find("Label").gameObject.SetActive(false);
        root.transform.Find("Outline").gameObject.SetActive(false);
        var selected = Fill(rect, "Selected"); selected.anchorMax = new Vector2(0, 1); selected.sizeDelta = new Vector2(4, -16); selected.anchoredPosition = Vector2.zero;
        Image(selected, Theme.Accent);
        Image(BottomFill(rect, "Divider", 1), Theme.Line);
        var portrait = Top(rect, "Portrait", 12, 20, 88, 88);
        var oldIcon = rect.Find("Icon");
        if (oldIcon != null) oldIcon.SetParent(Fill(portrait, "Clip"), false);
        var icon = RoundIcon(portrait);
        var silhouette = Silhouette(Fill(icon.transform, "Placeholder"));
        var name = Label(TopFill(rect, "Name", 4, 64, 120, 12), "兵士01", token ? 30 : 36);
        var state = Label(TopFill(rect, "State", 64, 48, 120, 12), "待機中", 28, Theme.Muted);
        Set(Add<CountryRosterRow>(root), ("_button", button), ("_icon", icon), ("_placeholder", silhouette), ("_name", name), ("_state", state), ("_selected", selected.gameObject));
        if (CountryRosterArtBuilder.Available)
        {
            if (token) CountryRosterArtBuilder.Token(root); else CountryRosterArtBuilder.Row(root);
        }
    }

    private static void BuildEmptySlot(GameObject root)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(264, 128);
        Layout(rect, 128); Column(rect, 264, 0);
        Circle(Top(rect, "Empty", 12, 20, 88, 88), Theme.Border, 3.5f, true);
        if (CountryRosterArtBuilder.Available) CountryRosterArtBuilder.Empty(root);
    }

    private static void BuildView(GameObject root)
    {
        Stretch((RectTransform)root.transform);
        var view = Add<CountryRosterView>(root);
        var left = Fill(root.transform, "Left"); left.anchorMax = new Vector2(.29f, 1); left.offsetMax = new Vector2(-16, 0);
        var caption = Label(TopFill(left, "Caption", 0, 64, 16, 16), "一覧", 30, Theme.Muted);
        var list = Scroll(Fill(left, "List", Vector2.zero, new Vector2(0, -64)));
        var right = Fill(root.transform, "Right"); right.anchorMin = new Vector2(.29f, 0); right.offsetMin = new Vector2(24, 0);
        var name = Label(TopFill(right, "Name", 0, 64, 16, 16), "兵士01", 44);
        var subtitle = Label(TopFill(right, "Subtitle", 64, 56, 16, 16), "剣　待機中", 28, Theme.Accent);
        var body = Fill(right, "Body", Vector2.zero, new Vector2(0, -128));
        var portraitArea = Fill(body, "Portrait"); portraitArea.anchorMax = new Vector2(.3f, 1); portraitArea.offsetMax = new Vector2(-16, 0);
        var frame = Fill(portraitArea, "Frame");
        foreach (string childName in new[] { "Surface", "Art", "Placeholder", "Caption" })
        {
            var old = portraitArea.Find(childName);
            if (old != null) old.SetParent(frame, false);
        }
        var aspect = Add<UnityEngine.UI.AspectRatioFitter>(frame.gameObject);
        // None mode replaces the ratio with the current rectangle's ratio in Edit Mode.
        aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = .5f;
        Plate(Fill(frame, "Surface"), new Color32(83, 97, 112, 255), 0);
        var image = Image(Fill(frame, "Art"), Color.white); image.preserveAspect = true;
        var placeholder = Silhouette(Fill(frame, "Placeholder", Vector2.one * 16, Vector2.one * -16));
        var portraitLabel = Label(BottomFill(frame, "Caption", 112), "", 24, Theme.Muted);
        portraitLabel.gameObject.SetActive(false);
        portraitLabel.alignment = TextAlignmentOptions.Center; portraitLabel.textWrappingMode = TextWrappingModes.Normal;
        var detailsRoot = Fill(body, "Details"); detailsRoot.anchorMin = new Vector2(.3f, 0); detailsRoot.offsetMin = new Vector2(16, 128);
        var details = Scroll(detailsRoot); var content = details.content;
        var statsTitle = Label(Child(content, "StatsTitle"), "ステータス", 30, Theme.Muted); Layout(statsTitle.rectTransform, 48);
        var stats = Child(content, "Stats"); Vertical(stats, 8); stats.SetSiblingIndex(1);
        string[] captions = { "CP", "HP", "STR", "INT", "FAI", "AGI" };
        var values = new TextMeshProUGUI[5]; TextMeshProUGUI cpValue = null; GameObject cp = null;
        for (int i = 0; i < captions.Length; i++)
        {
            var row = Child(stats, $"Row{i / 2 + 1}"); Layout(row, 56); Horizontal(row, 32, true);
            var previous = content.Find(captions[i]);
            if (previous != null) previous.SetParent(row, false);
            var cell = Child(row, captions[i]); Layout(cell, 56); Column(cell, 0, 1);
            var breakdown = cell.Find("Breakdown");
            if (breakdown != null) Object.DestroyImmediate(breakdown.gameObject);
            Label(Top(cell, "Caption", 0, 0, 128, 56), i == 1 ? "最大HP" : captions[i], 30, Theme.Muted);
            var value = Label(Fill(cell, "Value", new Vector2(136, 0), new Vector2(-8, 0)), "100", 36);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Image(BottomFill(cell, "Divider", 1), Theme.Line);
            if (i == 0) { cp = cell.gameObject; cpValue = value; }
            else values[i - 1] = value;
        }
        var skillsTitle = Label(Child(content, "SkillsTitle"), "装備", 30, Theme.Accent); Layout(skillsTitle.rectTransform, 64);
        var skills = Label(Child(content, "Skills"), "", 30); skills.alignment = TextAlignmentOptions.TopLeft; skills.textWrappingMode = TextWrappingModes.Normal;
        var learned = Child(content, "LearnedSkills"); Vertical(learned, 16);
        var learnedTitle = Label(Child(learned, "Title"), "習得済みスキル", 32, Theme.Accent); Layout(learnedTitle.rectTransform, 64);
        var groups = new GameObject[6]; var groupNames = new TextMeshProUGUI[6];
        var categoryIcons = new Sprite[6];
        var groupStates = new TextMeshProUGUI[6]; var groupSkills = new TextMeshProUGUI[6];
        for (int i = 0; i < groups.Length; i++)
        {
            var kind = (WeaponKind)(i + 1);
            var group = Child(learned, kind.ToString()); Vertical(group, 8); groups[i] = group.gameObject;
            var header = Child(group, "Header"); Layout(header, 72);
            categoryIcons[i] = WeaponIcon(kind);
            var categoryIcon = RoundIcon(Top(header, "WeaponIcon", 0, 8, 56, 56)); categoryIcon.sprite = categoryIcons[i];
            groupNames[i] = Label(Top(header, "Name", 76, 0, 300, 72), CountryRoster.WeaponName(kind), 32);
            groupStates[i] = Label(Fill(header, "State", new Vector2(392, 0), Vector2.zero), "", 26, Theme.Muted);
            groupStates[i].alignment = TextAlignmentOptions.MidlineRight;
            Image(BottomFill(header, "Divider", 1), Theme.Line);
            groupSkills[i] = Label(Child(group, "Skills"), "", 30);
            groupSkills[i].margin = new Vector4(76, 0, 0, 8);
            groupSkills[i].alignment = TextAlignmentOptions.TopLeft; groupSkills[i].textWrappingMode = TextWrappingModes.Normal;
        }
        var footer = BottomFill(right, "Actions", 120); footer.anchorMin = new Vector2(.3f, 0); footer.offsetMin = new Vector2(16, 0);
        var action = Button(Right(footer, "Action", 0, 4, 400, 112), "装備を変更", Theme.Face, true);
        var actionLabel = action.GetComponentInChildren<TextMeshProUGUI>(); actionLabel.fontSize = 32;
        var hint = Label(Fill(footer, "Hint", new Vector2(16, 0), new Vector2(-424, 0)), "", 26, Theme.Muted); hint.textWrappingMode = TextWrappingModes.Normal;
        Set(view, ("_data", BuildData()), ("_rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryRosterRow.prefab").GetComponent<CountryRosterRow>()),
            ("_listCaption", caption), ("_list", list), ("_details", details), ("_portrait", image), ("_portraitFrame", aspect), ("_cpRow", cp), ("_cpValue", cpValue), ("_placeholder", placeholder), ("_name", name), ("_subtitle", subtitle), ("_portraitLabel", portraitLabel),
            ("_statsTitle", statsTitle), ("_skillsTitle", skillsTitle), ("_skills", skills), ("_learnedSection", learned.gameObject), ("_hint", hint), ("_actionLabel", actionLabel), ("_action", action));
        SetArray(view, "_stats", values);
        Set(view, ("_roundedPanel", AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Shapes/RoundedPanel.png")));
        SetArray(view, "_weaponCategoryIcons", categoryIcons);
        SetArray(view, "_skillGroups", groups); SetArray(view, "_skillGroupNames", groupNames);
        SetArray(view, "_skillGroupStates", groupStates); SetArray(view, "_skillGroupSkills", groupSkills);
        CountryGovernmentControlsBuilder.Roster(root);
        if (CountryRosterArtBuilder.Available) CountryRosterArtBuilder.View(root);
    }

    private static void BuildTraining(GameObject root)
    {
        Stretch((RectTransform)root.transform);
        var training = Add<CountryTrainingView>(root);
        var schoolBar = TopFill(root.transform, "Schools", 0, 112, 0, 0); var buttons = new UnityEngine.UI.Button[3];
        var data = BuildData();
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i] = Button(Top(schoolBar, $"School{i + 1}", i * 264, 0, 248, 112), data.Schools[i].Name, Theme.Panel); Tab(buttons[i]);
        }
        var schoolName = Label(Fill(schoolBar, "SchoolName", new Vector2(824, 0), new Vector2(-16, 0)), "", 30, Theme.Accent);
        CountryRosterSelectionBuilder.ApplyTraining(root);
        Set(training, ("_schoolName", schoolName),
            ("_emptySlotPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryTrainingEmptySlot.prefab")),
            ("_tokenPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountrySoldierToken.prefab").GetComponent<CountryRosterRow>()));
        SetArray(training, "_schools", buttons);
        CountryGovernmentControlsBuilder.Training(root);
        CountryUsabilityBuilder.ApplyTraining(root);
    }

    public static void ApplyTraining(Transform page, CountryGovernmentPanel panel)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CountryTraining.prefab") == null) EnsureAssets();
        var legacy = page.Find("Content");
        if (legacy != null && legacy.GetComponent<CountryTrainingView>() == null) Object.DestroyImmediate(legacy.gameObject);
        var guide = page.Find("Guide"); if (guide != null) Object.DestroyImmediate(guide.gameObject);
        var training = Instance(page, "Content", "CountryTraining").GetComponent<CountryTrainingView>();
        var rect = (RectTransform)training.transform; rect.offsetMin = new Vector2(40, 24); rect.offsetMax = new Vector2(-40, 0);
        var confirm = Instance(panel.transform.Find("Window"), "TrainingConfirm", "CountryRosterConfirm").GetComponent<CountryRosterConfirm>();
        Set(confirm, ("_blocked", panel.transform.Find("Window/Shell").GetComponent<CanvasGroup>()));
        Set(training, ("_confirmation", confirm)); Set(panel, ("_training", training));
        PrefabUtility.RecordPrefabInstancePropertyModifications(training); PrefabUtility.RecordPrefabInstancePropertyModifications(confirm);
    }

    private static void BuildPersonnel(GameObject root)
    {
        Stretch((RectTransform)root.transform); var panel = Add<CountryPersonnelPanel>(root);
        var window = Fill(root.transform, "Window"); var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 50;
        Add<UnityEngine.UI.GraphicRaycaster>(window.gameObject); Image(window, Theme.Background).raycastTarget = true;
        var shell = Fill(window, "Shell"); var group = Add<CanvasGroup>(shell.gameObject);
        var background = TopFill(window, "HeaderBackground", 0, 112);
        Image(background, Theme.Panel).raycastTarget = false; background.SetAsFirstSibling();
        var header = TopFill(shell, "Header", 0, 112); Image(header, Theme.Panel).enabled = false;
        var soldiers = Button(Right(header, "Soldiers", 360, 0, 320, 112), "兵士一覧へ", Theme.Panel); Tab(soldiers);
        var weapons = Button(Right(header, "Weapons", 360, 0, 320, 112), "武器一覧へ", Theme.Panel); Tab(weapons);
        foreach (var button in new[] { soldiers, weapons })
        {
            button.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 30;
            button.transform.Find("Selected").gameObject.SetActive(false);
        }
        var provisional = Label(Fill(header, "DataState", new Vector2(624, 0), new Vector2(-32, 0)), "", 28, Theme.Muted); provisional.gameObject.SetActive(false);
        var oldContext = shell.Find("Context"); if (oldContext != null) oldContext.SetParent(header, false);
        var context = Label(Top(header, "Context", 40, 0, 400, 112), "兵士一覧", 32, Theme.Accent);
        var main = Fill(shell, "Main", new Vector2(40, 24), new Vector2(-40, -180)); Board(main);
        var view = Instance(main, "Roster", "CountryRosterView").GetComponent<CountryRosterView>();
        ConfigurePersonnelView(view);
        var footer = BottomFill(shell, "Footer", 128); Image(footer, Theme.Panel).enabled = false;
        var oldBack = header.Find("Back"); if (oldBack != null) oldBack.SetParent(footer, false);
        var backRect = Child(footer, "Back"); backRect.anchorMin = backRect.anchorMax = backRect.pivot = Vector2.zero;
        backRect.anchoredPosition = new Vector2(72, 40); backRect.sizeDelta = new Vector2(224, 112);
        var back = Button(backRect, "‹ 戻る", Theme.Face);
        back.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 36;
        var help = footer.Find("Help"); if (help != null) help.gameObject.SetActive(false);
        var confirmation = Instance(window, "Confirm", "CountryRosterConfirm").GetComponent<CountryRosterConfirm>(); Set(confirmation, ("_blocked", group));
        Set(panel, ("_window", window.gameObject), ("_view", view), ("_confirmation", confirmation), ("_soldiersTab", soldiers), ("_weaponsTab", weapons),
            ("_shell", group), ("_headerBackground", background), ("_back", back), ("_context", context), ("_dataState", provisional), ("_backLabel", back.GetComponentInChildren<TextMeshProUGUI>(true)));
        CountryDefenseBuilder.ApplyPersonnel(root);
        window.gameObject.SetActive(false);
    }

    private static void ConfigurePersonnelView(CountryRosterView view)
    {
        var right = view.transform.Find("Right");
        var body = (RectTransform)right.Find("Body"); body.anchoredPosition = new Vector2(body.anchoredPosition.x, 0); body.sizeDelta = new Vector2(body.sizeDelta.x, 0);
        var portrait = (RectTransform)body.Find("Portrait"); portrait.anchorMax = new Vector2(0, portrait.anchorMax.y); portrait.pivot = new Vector2(0, portrait.pivot.y);
        portrait.anchoredPosition = new Vector2(0, portrait.anchoredPosition.y); portrait.sizeDelta = new Vector2(320, portrait.sizeDelta.y);
        var name = (RectTransform)right.Find("Name"); name.anchoredPosition = new Vector2(336, -24); name.sizeDelta = new Vector2(-836, name.sizeDelta.y);
        var details = (RectTransform)body.Find("Details"); details.anchorMin = new Vector2(0, details.anchorMin.y);
        details.anchoredPosition = new Vector2(160, -56); details.sizeDelta = new Vector2(-352, details.sizeDelta.y);
        foreach (var rect in new[] { body, portrait, name, details }) PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
    }

    private static void BuildConfirm(GameObject root)
    {
        Stretch((RectTransform)root.transform); var controller = Add<CountryRosterConfirm>(root);
        var window = Fill(root.transform, "Window"); var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 80;
        Add<UnityEngine.UI.GraphicRaycaster>(window.gameObject);
        Image(Fill(window, "Dim"), new Color(.02f, .04f, .08f, .85f)).raycastTarget = true;
        var card = Child(window, "Card"); card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f; card.sizeDelta = new Vector2(1240, 660);
        Plate(Fill(card, "Surface"), Theme.Panel, 8); Plate(Fill(card, "Frame"), Theme.Border, 8, 2);
        var title = Label(TopFill(card, "Title", 24, 88, 48, 48), "訓練を開始", 44);
        var bodyRect = card.Find("BodyScroll/Viewport/Content/Body") as RectTransform ?? Fill(card, "Body", new Vector2(48, 160), new Vector2(-48, -136));
        var body = Label(bodyRect, "", 34); body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.TopLeft;
        var footer = BottomFill(card, "Footer", 128);
        var cancel = Button(Top(footer, "Cancel", 48, 0, 248, 112), "戻る", Theme.Face);
        var confirm = Button(Right(footer, "Confirm", 48, 0, 336, 112), "訓練を開始", Theme.Selected, true);
        Set(controller, ("_window", window.gameObject), ("_title", title), ("_body", body), ("_confirm", confirm), ("_cancel", cancel), ("_confirmLabel", confirm.GetComponentInChildren<TextMeshProUGUI>()));
        CountryUsabilityBuilder.ApplyConfirmation(root);
        window.gameObject.SetActive(false);
    }

    private static CountrySoldierSilhouette Silhouette(RectTransform rect)
    { Add<CanvasRenderer>(rect.gameObject); var graphic = Add<CountrySoldierSilhouette>(rect.gameObject); graphic.color = new Color32(133, 147, 163, 255); graphic.raycastTarget = false; return graphic; }
    private static UnityEngine.UI.Image RoundIcon(RectTransform rect)
    {
        Circle(rect, Theme.Recess);
        var clip = Fill(rect, "Clip", Vector2.one * 3, Vector2.one * -3);
        Circle(clip, Color.white); Add<UnityEngine.UI.Mask>(clip.gameObject).showMaskGraphic = false;
        var icon = Image(Fill(clip, "Icon"), Color.white); icon.preserveAspect = true;
        Circle(Fill(rect, "Edge"), Theme.Border, 2.5f);
        return icon;
    }
    private static void Vertical(RectTransform rect, float spacing)
    {
        var layout = Add<UnityEngine.UI.VerticalLayoutGroup>(rect.gameObject); layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
    }
    private static void RemoveDeveloperNotes()
    {
        foreach (string name in new[] { "CountryGovernment", "CountryGovernmentAllocationRow" })
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/" + name + ".prefab");
            foreach (var label in scope.prefabContentsRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.text.Contains("〈表示例〉")) label.text = label.text.Replace("〈表示例〉", "");
        }
    }
    private static void Circle(RectTransform rect, Color color, float stroke = 0, bool dashed = false)
    {
        var graphic = Add<CountryCircleGraphic>(rect.gameObject);
        graphic.color = color; graphic.raycastTarget = false; graphic.Configure(stroke, dashed);
    }
    private static RectTransform Instance(Transform parent, string name, string prefab)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) { rect = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + prefab + ".prefab"), parent)).transform; rect.name = name; }
        Stretch(rect); return rect;
    }
    private static void Board(Transform parent)
    {
        var board = Instance(parent, "Workboard", "CountryWorkboard"); board.SetAsFirstSibling();
        board.offsetMin = new Vector2(-12, -8); board.offsetMax = new Vector2(12, 8);
    }
}
