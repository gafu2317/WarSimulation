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
            Assert.That(selectionCountText.enableWordWrapping, Is.False);
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
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(960f, 720f)));

            pickerContent.GetChild(pickerContent.childCount - 1).GetComponent<Button>().onClick.Invoke();
            IList allyRows = GetPrivateField<IList>(selection, "_allyRows");
            for (int i = 0; i < allyRows.Count; i++)
            {
                Assert.That(
                    GetPrivateField<int>(allyRows[i], "WeaponIndex"),
                    Is.EqualTo(selection.WeaponOptions.Count - 1));
            }

            Assert.That(selection.WeaponOptions.Count, Is.GreaterThan(1));
            GetPrivateField<Button>(allyRows[0], "WeaponButton").onClick.Invoke();
            pickerContent = GetPrivateField<Transform>(selection, "_pickerContent");
            pickerContent.GetChild(0).GetComponent<Button>().onClick.Invoke();
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
            Assert.That(personalityGrid.constraintCount, Is.EqualTo(2));

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

    private static void SetState(CombatBattleFlow flow, CombatBattleState state)
    {
        FieldInfo field = typeof(CombatBattleFlow).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(flow, state);
    }
}
