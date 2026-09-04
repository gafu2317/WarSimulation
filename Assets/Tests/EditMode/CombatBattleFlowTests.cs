using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatBattleFlowTests
{
    [Test]
    public void EndBattle_NotifiesTheFirstValidOutcomeOnly()
    {
        GameObject flowObject = new GameObject("BattleFlow");

        try
        {
            CombatBattleFlow flow = flowObject.AddComponent<CombatBattleFlow>();
            SetState(flow, CombatBattleState.Running);
            int notificationCount = 0;
            CombatBattleState notifiedOutcome = CombatBattleState.WaitingToStart;
            flow.BattleEnded += outcome =>
            {
                notificationCount++;
                notifiedOutcome = outcome;
            };

            flow.EndBattle(CombatBattleState.WaitingToStart);
            flow.EndBattle(CombatBattleState.Victory);
            flow.EndBattle(CombatBattleState.Defeat);

            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(notifiedOutcome, Is.EqualTo(CombatBattleState.Victory));
            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Victory));
        }
        finally
        {
            Object.DestroyImmediate(flowObject);
        }
    }

    [Test]
    public void KuenBattleHud_CyclesSpeedAndPausesUntilResume()
    {
        GameObject hudObject = null;
        GameObject battleFlowObject = new GameObject("BattleFlow");
        GameObject flowObject = new GameObject("CombatFlow");

        try
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI.prefab");
            Assert.That(hudPrefab, Is.Not.Null);
            hudObject = Object.Instantiate(hudPrefab);

            CombatBattleHudView hud = hudObject.GetComponent<CombatBattleHudView>();
            CombatPartyStatusPanel statusPanel = hudObject.GetComponent<CombatPartyStatusPanel>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(statusPanel, Is.Not.Null);
            Assert.That(hudObject.transform.Find("EnemiesColumn"), Is.Null);
            Assert.That(
                GetPrivateField<object>(hudObject.transform.Find("MagicStoneStatusRoot/AllyStonePanel").GetComponent<CombatMagicStoneStatusView>(), "_featureType").ToString(),
                Is.EqualTo("OwnMainStone"));
            Assert.That(
                GetPrivateField<object>(hudObject.transform.Find("MagicStoneStatusRoot/EnemyStonePanel").GetComponent<CombatMagicStoneStatusView>(), "_featureType").ToString(),
                Is.EqualTo("EnemyMainStone"));

            TMP_Text[] labels = hudObject.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            for (int i = 0; i < labels.Length; i++)
            {
                Assert.That(
                    AssetDatabase.GetAssetPath(labels[i].font),
                    Is.EqualTo("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset"));
            }

            CombatBattleFlow battleFlow = battleFlowObject.AddComponent<CombatBattleFlow>();
            SetState(battleFlow, CombatBattleState.Running);
            CombatFlow combatFlow = flowObject.AddComponent<CombatFlow>();
            SetPrivateField(combatFlow, "_battleFlow", battleFlow);
            SetPrivateField(combatFlow, "_battleHudView", hud);
            InvokePrivate(combatFlow, "EnsureBattleControls");

            Button speedButton = hudObject.transform.Find("TemporaryBattleControls/SpeedButton").GetComponent<Button>();
            Button menuButton = hudObject.transform.Find("ControlPanel/Menu/Image").GetComponent<Button>();
            Button pauseButton = hudObject.transform.Find("TemporaryBattleControls/PauseButton").GetComponent<Button>();
            Button returnButton = hudObject.transform.Find("TemporaryBattleControls/ReturnToSelectionButton").GetComponent<Button>();
            TMP_Text speedText = hudObject.transform.Find("ControlPanel/Speed/Text").GetComponent<TMP_Text>();
            TMP_Text pauseText = pauseButton.GetComponentInChildren<TMP_Text>();

            Assert.That(pauseButton, Is.Not.Null);
            Assert.That(returnButton, Is.Not.Null);

            speedButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(2f));
            Assert.That(speedText.text, Is.EqualTo("2x"));

            menuButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(hud.IsMenuVisible, Is.True);

            speedButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(speedText.text, Is.EqualTo("2x"));

            pauseButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(2f));
            Assert.That(hud.IsMenuVisible, Is.False);
            Assert.That(pauseText.text, Is.EqualTo("一時停止"));

            pauseButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(pauseText.text, Is.EqualTo("再開"));

            pauseButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            speedButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(4f));

            speedButton.onClick.Invoke();
            Assert.That(Time.timeScale, Is.EqualTo(6f));
            Assert.That(speedText.text, Is.EqualTo("6x"));

            Button smokeButton = hudObject.transform.Find("UserCommandPanel/Smoke/Image").GetComponent<Button>();
            Button weatherButton = hudObject.transform.Find("UserCommandPanel/WeatherChange/Image/Option1").GetComponent<Button>();
            Assert.That(smokeButton.interactable, Is.False);
            Assert.That(weatherButton.interactable, Is.False);
        }
        finally
        {
            Time.timeScale = 1f;
            if (hudObject != null) Object.DestroyImmediate(hudObject);
            Object.DestroyImmediate(flowObject);
            Object.DestroyImmediate(battleFlowObject);
        }
    }

    [Test]
    public void KuenBattleHud_SwitchingUiModeHidesReparentedProductionMemberCards()
    {
        GameObject hudObject = null;

        try
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI.prefab");
            Assert.That(hudPrefab, Is.Not.Null);
            hudObject = Object.Instantiate(hudPrefab);

            CombatBattleHudView hud = hudObject.GetComponent<CombatBattleHudView>();
            Transform productionColumn = hudObject.transform.Find("AlliesColumn");
            Assert.That(hud, Is.Not.Null);
            Assert.That(productionColumn, Is.Not.Null);

            GameObject viewportObject = new GameObject("AlliesViewport");
            viewportObject.transform.SetParent(hudObject.transform, false);
            productionColumn.SetParent(viewportObject.transform, false);

            hud.SetDebugUiVisible(false);
            Assert.That(productionColumn.gameObject.activeSelf, Is.True);

            hud.SetDebugUiVisible(true);
            Assert.That(productionColumn.gameObject.activeSelf, Is.False);

            hud.SetDebugUiVisible(false);
            Assert.That(productionColumn.gameObject.activeSelf, Is.True);
        }
        finally
        {
            if (hudObject != null) Object.DestroyImmediate(hudObject);
        }
    }

    [Test]
    public void BattleUi_HasLayeredHpBarsForCharacterCardsAndMagicStones()
    {
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI.prefab");
        Assert.That(hudPrefab, Is.Not.Null);

        GameObject hudObject = Object.Instantiate(hudPrefab);
        try
        {
            CombatPartyMemberView[] characterViews = hudObject.GetComponentsInChildren<CombatPartyMemberView>(true);
            CombatMagicStoneStatusView[] magicStoneViews = hudObject.GetComponentsInChildren<CombatMagicStoneStatusView>(true);

            Assert.That(characterViews, Has.Length.EqualTo(6));
            Assert.That(magicStoneViews, Has.Length.EqualTo(2));

            for (int i = 0; i < characterViews.Length; i++)
            {
                AssertLayeredHpBar(GetPrivateField<Image>(characterViews[i], "_hpFillImage"));
            }

            for (int i = 0; i < magicStoneViews.Length; i++)
            {
                AssertLayeredHpBar(GetPrivateField<Image>(magicStoneViews[i], "_hpFillImage"));
            }
        }
        finally
        {
            Object.DestroyImmediate(hudObject);
        }
    }

    [Test]
    public void KuenBattleHud_ShowsTenAlliesInHorizontalScroll()
    {
        GameObject hudObject = null;
        GameObject systemObject = new GameObject("CharacterSystem");
        var characters = new List<GameObject>();

        try
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleUI.prefab");
            Assert.That(hudPrefab, Is.Not.Null);
            hudObject = Object.Instantiate(hudPrefab);
            CombatPartyStatusPanel panel = hudObject.GetComponent<CombatPartyStatusPanel>();
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            system.AllyCharacters.AddRange(CreateCharacters("Ally", CombatTeam.Ally, 10, characters));

            panel.Initialize(system);

            ScrollRect scroll = hudObject.GetComponentInChildren<ScrollRect>(includeInactive: true);
            Assert.That(panel.AllyViewCount, Is.EqualTo(10));
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.vertical, Is.False);
        }
        finally
        {
            if (hudObject != null) Object.DestroyImmediate(hudObject);
            Object.DestroyImmediate(systemObject);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null) Object.DestroyImmediate(characters[i]);
            }
        }
    }

    [Test]
    public void CombatFlow_ReturnsToSelectionWithoutResettingLastUsedFormation()
    {
        GameObject characterSystemObject = new GameObject("CharacterSystem");
        GameObject battleFlowObject = new GameObject("BattleFlow");
        GameObject flowObject = new GameObject("CombatFlow");
        GameObject selectionObject = null;
        var characters = new List<GameObject>();

        try
        {
            CombatCharacterSystem characterSystem = characterSystemObject.AddComponent<CombatCharacterSystem>();
            CombatBattleFlow battleFlow = battleFlowObject.AddComponent<CombatBattleFlow>();
            GameObject selectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab");
            Assert.That(selectionPrefab, Is.Not.Null);

            selectionObject = Object.Instantiate(selectionPrefab);
            CombatCharacterSelection selection = selectionObject.GetComponent<CombatCharacterSelection>();
            Assert.That(selection, Is.Not.Null);

            List<Character> allies = CreateCharacters("Ally", CombatTeam.Ally, 6, characters);
            List<Character> enemies = CreateCharacters("Enemy", CombatTeam.Enemy, 6, characters);
            selection.Initialize(allies, enemies, null);
            Assert.That(selection.IsStonePositionReversed, Is.False);
            Button stonePositionButton = GetPrivateField<Button>(selection, "_stonePositionButton");
            stonePositionButton.onClick.Invoke();
            Assert.That(selection.IsStonePositionReversed, Is.True);

            IList allyRows = GetPrivateField<IList>(selection, "_allyRows");
            IList enemyRows = GetPrivateField<IList>(selection, "_enemyRows");
            object firstAllyRow = allyRows[0];
            object lastAllyRow = allyRows[allyRows.Count - 1];
            object firstEnemyRow = enemyRows[0];
            object lastEnemyRow = enemyRows[enemyRows.Count - 1];
            Assert.That(GetPrivateField<bool>(firstAllyRow, "Selected"), Is.True);
            Assert.That(GetPrivateField<bool>(lastAllyRow, "Selected"), Is.False);
            Assert.That(GetPrivateField<bool>(firstEnemyRow, "Selected"), Is.True);
            Assert.That(GetPrivateField<bool>(lastEnemyRow, "Selected"), Is.False);

            int customWeaponIndex = selection.WeaponOptions.Count - 1;
            List<CombatAiPersonalityProfile> personalityOptions = GetPrivateField<List<CombatAiPersonalityProfile>>(
                selection,
                "_personalityOptions");
            int customPersonalityIndex = personalityOptions.Count - 1;
            SetPrivateField(firstAllyRow, "Selected", false);
            SetPrivateField(lastAllyRow, "Selected", true);
            SetPrivateField(lastAllyRow, "WeaponIndex", customWeaponIndex);
            SetPrivateField(lastAllyRow, "PersonalityIndex", customPersonalityIndex);
            SetPrivateField(firstEnemyRow, "Selected", false);
            SetPrivateField(lastEnemyRow, "Selected", true);
            SetPrivateField(lastEnemyRow, "WeaponIndex", customWeaponIndex);
            SetPrivateField(lastEnemyRow, "PersonalityIndex", customPersonalityIndex);

            CombatFlow combatFlow = flowObject.AddComponent<CombatFlow>();
            SetPrivateField(combatFlow, "_characterSystem", characterSystem);
            SetPrivateField(combatFlow, "_battleFlow", battleFlow);
            SetPrivateField(combatFlow, "_characterSelection", selection);
            GetPrivateField<List<Character>>(combatFlow, "_allyCandidates").AddRange(allies);
            GetPrivateField<List<Character>>(combatFlow, "_enemies").AddRange(enemies);

            MethodInfo showSelection = typeof(CombatFlow).GetMethod(
                "ShowSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showSelection, Is.Not.Null);
            showSelection.Invoke(combatFlow, null);

            Assert.That(GetPrivateField<bool>(firstAllyRow, "Selected"), Is.False);
            Assert.That(GetPrivateField<bool>(lastAllyRow, "Selected"), Is.True);
            Assert.That(GetPrivateField<int>(lastAllyRow, "WeaponIndex"), Is.EqualTo(customWeaponIndex));
            Assert.That(GetPrivateField<int>(lastAllyRow, "PersonalityIndex"), Is.EqualTo(customPersonalityIndex));
            Assert.That(GetPrivateField<bool>(firstEnemyRow, "Selected"), Is.False);
            Assert.That(GetPrivateField<bool>(lastEnemyRow, "Selected"), Is.True);
            Assert.That(GetPrivateField<int>(lastEnemyRow, "WeaponIndex"), Is.EqualTo(customWeaponIndex));
            Assert.That(GetPrivateField<int>(lastEnemyRow, "PersonalityIndex"), Is.EqualTo(customPersonalityIndex));

            selection.SetStonePositionReversedState(false);
            Assert.That(selection.IsStonePositionReversed, Is.False);
        }
        finally
        {
            if (selectionObject != null) Object.DestroyImmediate(selectionObject);
            Object.DestroyImmediate(flowObject);
            Object.DestroyImmediate(battleFlowObject);
            Object.DestroyImmediate(characterSystemObject);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null) Object.DestroyImmediate(characters[i]);
            }
        }
    }

    [Test]
    public void CombatCameraSide_FollowsCurrentAllyStonePosition()
    {
        MethodInfo method = typeof(CombatFlow).GetMethod(
            "IsAllyOnLowSide",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        Assert.That(
            method.Invoke(null, new object[]
            {
                new Vector3(10f, 0f, 5f),
                new Vector3(50f, 0f, 55f),
            }),
            Is.True);
        Assert.That(
            method.Invoke(null, new object[]
            {
                new Vector3(50f, 0f, 55f),
                new Vector3(10f, 0f, 5f),
            }),
            Is.False);
    }

    [Test]
    public void CharacterSelection_ShowsTenCandidatesInTwoColumnsAndKeepsToggleMode()
    {
        GameObject selectionObject = null;
        var characters = new List<GameObject>();

        try
        {
            GameObject selectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab");
            Assert.That(selectionPrefab, Is.Not.Null);

            selectionObject = Object.Instantiate(selectionPrefab);
            CombatCharacterSelection selection = selectionObject.GetComponent<CombatCharacterSelection>();
            Assert.That(selection, Is.Not.Null);

            List<Character> allies = CreateCharacters("Ally", CombatTeam.Ally, 10, characters);
            List<Character> enemies = CreateCharacters("Enemy", CombatTeam.Enemy, 10, characters);
            selection.Initialize(allies, enemies, null);

            GameObject allyColumn = GetPrivateField<GameObject>(selection, "_allyColumnRoot");
            Transform allyRowsGrid = allyColumn.transform.Find("AllyRows");
            Assert.That(allyRowsGrid, Is.Not.Null);
            GridLayoutGroup grid = allyRowsGrid.GetComponent<GridLayoutGroup>();
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.startAxis, Is.EqualTo(GridLayoutGroup.Axis.Vertical));
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedRowCount));
            Assert.That(grid.constraintCount, Is.EqualTo(5));
            Assert.That(allyRowsGrid.childCount, Is.EqualTo(10));

            for (int i = 0; i < allyRowsGrid.childCount; i++)
            {
                Assert.That(allyRowsGrid.GetChild(i).GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(allyRowsGrid.GetChild(i).childCount, Is.EqualTo(3));
            }

            TMP_Text selectionCountText = GetPrivateField<TMP_Text>(selection, "_selectionCountText");
            Assert.That(selectionCountText.text, Is.EqualTo("味方 5/10人 / 敵 5/10人"));
            Assert.That(selectionCountText.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(selectionCountText.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            Assert.That(selectionCountText.rectTransform.sizeDelta.x, Is.EqualTo(420f));

            IList allySelectionRows = GetPrivateField<IList>(selection, "_allyRows");
            for (int i = 0; i < allySelectionRows.Count; i++)
            {
                Assert.That(
                    GetPrivateField<bool>(allySelectionRows[i], "Selected"),
                    Is.EqualTo(i < 5));
            }

            object firstAllyRow = allySelectionRows[0];
            Button firstCharacterButton = GetPrivateField<Button>(firstAllyRow, "CharacterButton");
            Assert.That(firstCharacterButton.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(300f));
            Assert.That(
                GetPrivateField<Button>(firstAllyRow, "WeaponButton").GetComponent<LayoutElement>().preferredWidth,
                Is.EqualTo(240f));
            Assert.That(
                GetPrivateField<Button>(firstAllyRow, "PersonalityButton").GetComponent<LayoutElement>().preferredWidth,
                Is.EqualTo(240f));
            firstCharacterButton.onClick.Invoke();
            Assert.That(selectionCountText.text, Is.EqualTo("味方 4/10人 / 敵 5/10人"));

            Button enemyFormationButton = GetPrivateField<Button>(selection, "_enemyFormationButton");
            enemyFormationButton.onClick.Invoke();

            Assert.That(GetPrivateField<GameObject>(selection, "_allyColumnRoot").activeSelf, Is.False);
            Assert.That(GetPrivateField<GameObject>(selection, "_enemyColumnRoot").activeSelf, Is.True);
            Assert.That(selectionCountText.text, Is.EqualTo("敵 5/10人"));
        }
        finally
        {
            if (selectionObject != null) Object.DestroyImmediate(selectionObject);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null) Object.DestroyImmediate(characters[i]);
            }
        }
    }

    [Test]
    public void CharacterSelection_BulkChangesCurrentTeamAndUsesLargerPicker()
    {
        GameObject selectionObject = null;
        var characters = new List<GameObject>();

        try
        {
            GameObject selectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab");
            Assert.That(selectionPrefab, Is.Not.Null);

            selectionObject = Object.Instantiate(selectionPrefab);
            CombatCharacterSelection selection = selectionObject.GetComponent<CombatCharacterSelection>();
            Assert.That(selection, Is.Not.Null);

            selection.Initialize(
                CreateCharacters("Ally", CombatTeam.Ally, 10, characters),
                CreateCharacters("Enemy", CombatTeam.Enemy, 10, characters),
                null);

            Button bulkWeaponButton = GetPrivateField<Button>(selection, "_bulkWeaponButton");
            bulkWeaponButton.onClick.Invoke();
            Transform pickerContent = GetPrivateField<Transform>(selection, "_pickerContent");
            Assert.That(pickerContent.childCount, Is.EqualTo(selection.WeaponOptions.Count));
            Assert.That(
                pickerContent.GetChild(0).GetComponent<LayoutElement>().preferredHeight,
                Is.EqualTo(64f));

            RectTransform pickerRoot = GetPrivateField<RectTransform>(selection, "_pickerRoot");
            RectTransform panel = pickerRoot.Find("Panel") as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(1440f, 720f)));
            RectTransform body = panel.Find("PickerBody") as RectTransform;
            Assert.That(body, Is.Not.Null);
            LayoutElement optionsLayout = body.Find("Scroll").GetComponent<LayoutElement>();
            LayoutElement detailsLayout = body.Find("SkillDetails").GetComponent<LayoutElement>();
            Assert.That(optionsLayout.flexibleWidth, Is.EqualTo(2f));
            Assert.That(detailsLayout.flexibleWidth, Is.EqualTo(8f));

            Transform detailsContent = body.Find("SkillDetails/Viewport/Content");
            Assert.That(detailsContent, Is.Not.Null);
            Assert.That(detailsContent.childCount, Is.GreaterThan(2));
            Assert.That(detailsContent.GetChild(2).name, Does.StartWith("SkillCard_"));
            Assert.That(detailsContent.GetChild(2).GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            TMP_Text[] detailTexts = detailsContent.GetComponentsInChildren<TMP_Text>();
            string detailText = string.Empty;
            for (int i = 0; i < detailTexts.Length; i++)
            {
                detailText += detailTexts[i].text;
            }
            Assert.That(detailText, Does.Contain("射程"));
            Assert.That(detailText, Does.Contain("CT"));
            Assert.That(detailText, Does.Contain("詠唱"));
            Assert.That(detailText, Does.Not.Contain("魔石"));

            pickerContent.GetChild(pickerContent.childCount - 1).GetComponent<Button>().onClick.Invoke();
            IList allyRows = GetPrivateField<IList>(selection, "_allyRows");
            for (int i = 0; i < allyRows.Count; i++)
            {
                Assert.That(
                    GetPrivateField<int>(allyRows[i], "WeaponIndex"),
                    Is.EqualTo(selection.WeaponOptions.Count - 1));
            }
            panel.Find("ClosePickerButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(
                GetPrivateField<int>(allyRows[0], "WeaponIndex"),
                Is.EqualTo(selection.WeaponOptions.Count - 1));

            Assert.That(selection.WeaponOptions.Count, Is.GreaterThan(1));
            GetPrivateField<Button>(allyRows[0], "WeaponButton").onClick.Invoke();
            pickerContent = GetPrivateField<Transform>(selection, "_pickerContent");
            pickerContent.GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(GetPrivateField<RectTransform>(selection, "_pickerRoot").gameObject.activeSelf, Is.False);
            Assert.That(GetPrivateField<int>(allyRows[0], "WeaponIndex"), Is.EqualTo(0));
            GetPrivateField<RectTransform>(selection, "_pickerRoot")
                .Find("Panel/ClosePickerButton")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(GetPrivateField<int>(allyRows[0], "WeaponIndex"), Is.EqualTo(0));
            Assert.That(
                GetPrivateField<int>(allyRows[1], "WeaponIndex"),
                Is.EqualTo(selection.WeaponOptions.Count - 1));

            GetPrivateField<Button>(selection, "_enemyFormationButton").onClick.Invoke();
            Button bulkPersonalityButton = GetPrivateField<Button>(selection, "_bulkPersonalityButton");
            bulkPersonalityButton.onClick.Invoke();
            pickerContent = GetPrivateField<Transform>(selection, "_pickerContent");
            IList personalityOptions = GetPrivateField<IList>(selection, "_personalityOptions");
            Assert.That(pickerContent.childCount, Is.EqualTo(personalityOptions.Count));
            Assert.That(personalityOptions.Count, Is.GreaterThan(1));
            GridLayoutGroup personalityGrid = pickerContent.GetComponent<GridLayoutGroup>();
            Assert.That(personalityGrid.enabled, Is.True);
            Assert.That(personalityGrid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(personalityGrid.constraintCount, Is.EqualTo(1));

            pickerContent.GetChild(pickerContent.childCount - 1).GetComponent<Button>().onClick.Invoke();
            IList enemyRows = GetPrivateField<IList>(selection, "_enemyRows");
            for (int i = 0; i < enemyRows.Count; i++)
            {
                Assert.That(
                    GetPrivateField<int>(enemyRows[i], "PersonalityIndex"),
                    Is.EqualTo(personalityOptions.Count - 1));
            }

            GetPrivateField<Button>(enemyRows[0], "PersonalityButton").onClick.Invoke();
            pickerContent = GetPrivateField<Transform>(selection, "_pickerContent");
            pickerContent.GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(GetPrivateField<int>(enemyRows[0], "PersonalityIndex"), Is.EqualTo(0));
            Assert.That(
                GetPrivateField<int>(enemyRows[1], "PersonalityIndex"),
                Is.EqualTo(personalityOptions.Count - 1));

            GetPrivateField<Button>(selection, "_enemyPresetNeutralButton").onClick.Invoke();
            WeaponKind[] expectedWeapons =
            {
                WeaponKind.Wand,
                WeaponKind.Wand,
                WeaponKind.Bible,
                WeaponKind.Rosary,
                WeaponKind.Grimoire,
            };
            CombatAiPersonalityKind[] expectedPersonalities =
            {
                CombatAiPersonalityKind.Neutral,
                CombatAiPersonalityKind.Neutral,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.BattleJunkie,
            };
            for (int i = 0; i < enemyRows.Count; i++)
            {
                bool selected = i < expectedWeapons.Length;
                Assert.That(GetPrivateField<bool>(enemyRows[i], "Selected"), Is.EqualTo(selected));
                if (!selected) continue;

                int weaponIndex = GetPrivateField<int>(enemyRows[i], "WeaponIndex");
                Assert.That(selection.WeaponOptions[weaponIndex].Kind, Is.EqualTo(expectedWeapons[i]));

                int personalityIndex = GetPrivateField<int>(enemyRows[i], "PersonalityIndex");
                Assert.That(
                    ((CombatAiPersonalityProfile)personalityOptions[personalityIndex]).Kind,
                    Is.EqualTo(expectedPersonalities[i]));
            }
        }
        finally
        {
            if (selectionObject != null) Object.DestroyImmediate(selectionObject);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null) Object.DestroyImmediate(characters[i]);
            }
        }
    }

    [Test]
    public void CharacterSelection_DestroyingWhileWeaponPickerIsOpen_DoesNotRefreshDestroyedRow()
    {
        GameObject selectionObject = null;
        var characters = new List<GameObject>();

        try
        {
            GameObject selectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab");
            Assert.That(selectionPrefab, Is.Not.Null);

            selectionObject = Object.Instantiate(selectionPrefab);
            CombatCharacterSelection selection = selectionObject.GetComponent<CombatCharacterSelection>();
            Assert.That(selection, Is.Not.Null);

            selection.Initialize(
                CreateCharacters("Ally", CombatTeam.Ally, 1, characters),
                CreateCharacters("Enemy", CombatTeam.Enemy, 1, characters),
                null);

            IList allyRows = GetPrivateField<IList>(selection, "_allyRows");
            Button weaponButton = GetPrivateField<Button>(allyRows[0], "WeaponButton");
            weaponButton.onClick.Invoke();

            Object.DestroyImmediate(selectionObject);
            selectionObject = null;
        }
        finally
        {
            if (selectionObject != null) Object.DestroyImmediate(selectionObject);
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null) Object.DestroyImmediate(characters[i]);
            }
        }
    }

    private static List<Character> CreateCharacters(
        string namePrefix,
        CombatTeam team,
        int count,
        List<GameObject> createdObjects)
    {
        var characters = new List<Character>(count);
        for (int i = 0; i < count; i++)
        {
            GameObject characterObject = new GameObject($"{namePrefix}{i}");
            Character character = characterObject.AddComponent<Character>();
            character.SetTeam(team);
            characters.Add(character);
            createdObjects.Add(characterObject);
        }

        return characters;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method {methodName} was not found on {target.GetType().Name}.");
        method.Invoke(target, null);
    }

    private static void SetState(CombatBattleFlow flow, CombatBattleState state)
    {
        FieldInfo field = typeof(CombatBattleFlow).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(flow, state);
    }

    private static void AssertLayeredHpBar(Image frontImage)
    {
        Assert.That(frontImage, Is.Not.Null);
        Assert.That(frontImage.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(frontImage.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
        Assert.That(frontImage.fillOrigin, Is.EqualTo(0));
        Assert.That(frontImage.fillAmount, Is.EqualTo(1f));
        Assert.That(frontImage.sprite, Is.Not.Null);

        Transform mask = frontImage.transform.parent;
        Assert.That(mask, Is.Not.Null);
        Assert.That(mask.name, Is.EqualTo("Mask"));

        Transform background = mask.Find("HPBarBackground");
        Assert.That(background, Is.Not.Null);
        Assert.That(background.GetSiblingIndex(), Is.LessThan(frontImage.transform.GetSiblingIndex()));

        Image backgroundImage = background.GetComponent<Image>();
        Assert.That(backgroundImage, Is.Not.Null);
        Assert.That(backgroundImage.type, Is.EqualTo(Image.Type.Simple));
        Assert.That(backgroundImage.sprite, Is.Null);
        Assert.That(backgroundImage.color.r, Is.EqualTo(11f / 255f).Within(0.000001f));
        Assert.That(backgroundImage.color.g, Is.EqualTo(61f / 255f).Within(0.000001f));
        Assert.That(backgroundImage.color.b, Is.EqualTo(27f / 255f).Within(0.000001f));
        Assert.That(backgroundImage.color.a, Is.EqualTo(1f));
        Assert.That(backgroundImage.raycastTarget, Is.False);
        Assert.That(backgroundImage.fillAmount, Is.EqualTo(1f));
    }
}
