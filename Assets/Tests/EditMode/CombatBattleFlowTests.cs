using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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
