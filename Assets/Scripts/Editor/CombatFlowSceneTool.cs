#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public static class CombatFlowSceneTool
{
    private const string SelectionPanelPath = "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab";
    private const string ResultPanelPath = "Assets/Prefabs/Combat/BattleFlow/CombatResultPanel.prefab";

    [MenuItem("Tools/War Simulation/Setup Combat Flow UI")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Canvas canvas = FindSceneComponent<Canvas>(scene, "Canvas");
        if (canvas == null)
        {
            throw new System.InvalidOperationException("Canvas named 'Canvas' was not found in the active scene.");
        }

        CombatCharacterSystem characterSystem = FindSceneComponent<CombatCharacterSystem>(scene);
        CombatBattleFlow battleFlow = FindSceneComponent<CombatBattleFlow>(scene);
        GameObject magicStoneStatus = FindDirectChild(canvas.transform, "MagicStoneStatusRoot");
        GameObject alliesColumn = FindDirectChild(canvas.transform, "AlliesColumn");
        GameObject enemiesColumn = FindDirectChild(canvas.transform, "EnemiesColumn");
        GameObject selectionPrefab = LoadPrefab(SelectionPanelPath);
        GameObject resultPrefab = LoadPrefab(ResultPanelPath);
        List<WeaponConfig> weapons = LoadWeaponOptions();
        List<CombatAiPersonalityProfile> personalities = LoadAssets<CombatAiPersonalityProfile>("Assets/Data/Combat/AI");

        if (characterSystem == null)
        {
            throw new System.InvalidOperationException("CombatCharacterSystem was not found in the active scene.");
        }

        if (battleFlow == null)
        {
            throw new System.InvalidOperationException("CombatBattleFlow was not found in the active scene.");
        }

        if (magicStoneStatus == null || alliesColumn == null || enemiesColumn == null)
        {
            throw new System.InvalidOperationException(
                "MagicStoneStatusRoot, AlliesColumn, and EnemiesColumn must remain direct children of Canvas.");
        }

        if (weapons.Count == 0 || personalities.Count == 0)
        {
            throw new System.InvalidOperationException(
                "At least one WeaponConfig and CombatAiPersonalityProfile asset are required.");
        }


        if (selectionPrefab.GetComponent<CombatCharacterSelection>() == null)
        {
            throw new System.InvalidOperationException("CombatCharacterSelection was not found on the selection prefab.");
        }

        GetRequiredComponent(resultPrefab.transform, "ResultTitle", GetTmpTextType());
        GetRequiredComponent<Button>(resultPrefab.transform, "BackToSelectionButton");

        GameObject previous = FindDirectChild(canvas.transform, "CombatFlowUI");
        if (previous != null)
        {
            Undo.DestroyObjectImmediate(previous);
        }

        GameObject flowUi = new GameObject("CombatFlowUI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(flowUi, "Setup Combat Flow UI");
        flowUi.transform.SetParent(canvas.transform, false);
        Stretch(flowUi.GetComponent<RectTransform>());

        GameObject selectionPanel = InstantiatePrefab(selectionPrefab, flowUi.transform);
        GameObject resultPanel = InstantiatePrefab(resultPrefab, flowUi.transform);
        Stretch(selectionPanel.GetComponent<RectTransform>());
        Stretch(resultPanel.GetComponent<RectTransform>());

        CombatCharacterSelection selection = selectionPanel.GetComponent<CombatCharacterSelection>();
        if (selection == null)
        {
            throw new System.InvalidOperationException("CombatCharacterSelection was not found on the selection prefab.");
        }

        ConfigureSelectionData(selection, weapons, personalities);
        Component resultTitle = GetRequiredComponent(resultPanel.transform, "ResultTitle", GetTmpTextType());
        Button backButton = GetRequiredComponent<Button>(resultPanel.transform, "BackToSelectionButton");
        CombatFlow flow = Undo.AddComponent<CombatFlow>(flowUi);
        SerializedObject serialized = new SerializedObject(flow);
        serialized.FindProperty("_characterSystem").objectReferenceValue = characterSystem;
        serialized.FindProperty("_battleFlow").objectReferenceValue = battleFlow;
        serialized.FindProperty("_characterSelection").objectReferenceValue = selection;
        serialized.FindProperty("_characterSelectionPanel").objectReferenceValue = selectionPanel;
        serialized.FindProperty("_resultPanel").objectReferenceValue = resultPanel;
        serialized.FindProperty("_resultTitle").objectReferenceValue = resultTitle;
        serialized.FindProperty("_backToSelectionButton").objectReferenceValue = backButton;

        SerializedProperty battleUiObjects = serialized.FindProperty("_battleUiObjects");
        battleUiObjects.arraySize = 3;
        battleUiObjects.GetArrayElementAtIndex(0).objectReferenceValue = magicStoneStatus;
        battleUiObjects.GetArrayElementAtIndex(1).objectReferenceValue = alliesColumn;
        battleUiObjects.GetArrayElementAtIndex(2).objectReferenceValue = enemiesColumn;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ValidateOriginalBattleUi(canvas.transform, magicStoneStatus, alliesColumn, enemiesColumn);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = flowUi;
        Debug.Log("CombatFlowUI was added under Canvas without modifying the existing battle UI hierarchy.");
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new System.InvalidOperationException($"Prefab was not found: {path}");
        }

        return prefab;
    }

    private static void ConfigureSelectionData(
        CombatCharacterSelection selection,
        List<WeaponConfig> weapons,
        List<CombatAiPersonalityProfile> personalities)
    {
        SerializedObject serialized = new SerializedObject(selection);
        SetObjectReferences(serialized.FindProperty("_weaponOptions"), weapons);
        SetObjectReferences(serialized.FindProperty("_personalityOptions"), personalities);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<WeaponConfig> LoadWeaponOptions()
    {
        List<WeaponConfig> assets = LoadAssets<WeaponConfig>("Assets/Data/Map/Weapon");
        var byKind = new Dictionary<WeaponKind, WeaponConfig>();
        for (int i = 0; i < assets.Count; i++)
        {
            WeaponConfig asset = assets[i];
            string expectedName = $"{asset.Kind}WeaponConfig";
            if (!byKind.TryGetValue(asset.Kind, out WeaponConfig current) || asset.name == expectedName)
            {
                byKind[asset.Kind] = asset;
            }
        }

        var options = new List<WeaponConfig>(byKind.Values);
        options.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return options;
    }

    private static List<T> LoadAssets<T>(string folder) where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        var assets = new List<T>();
        for (int i = 0; i < guids.Length; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        assets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return assets;
    }

    private static void SetObjectReferences<T>(SerializedProperty property, List<T> values)
        where T : UnityEngine.Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Setup Combat Flow UI");
        return instance;
    }

    private static T FindSceneComponent<T>(Scene scene, string objectName = null) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject.scene != scene) continue;
            if (objectName == null || component.gameObject.name == objectName) return component;
        }

        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static T GetRequiredComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent.Find(childName);
        T component = child != null ? child.GetComponent<T>() : null;
        if (component == null)
        {
            throw new System.InvalidOperationException($"{childName} with {typeof(T).Name} was not found.");
        }

        return component;
    }

    private static Component GetRequiredComponent(Transform parent, string childName, System.Type type)
    {
        Transform child = parent.Find(childName);
        Component component = child != null && type != null ? child.GetComponent(type) : null;
        if (component == null)
        {
            throw new System.InvalidOperationException($"{childName} with TextMeshProUGUI was not found.");
        }

        return component;
    }

    private static System.Type GetTmpTextType()
    {
        return System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
    }

    private static void ValidateOriginalBattleUi(
        Transform canvas,
        GameObject magicStoneStatus,
        GameObject alliesColumn,
        GameObject enemiesColumn)
    {
        if (magicStoneStatus.transform.parent != canvas ||
            alliesColumn.transform.parent != canvas ||
            enemiesColumn.transform.parent != canvas)
        {
            throw new System.InvalidOperationException("The existing battle UI hierarchy was unexpectedly modified.");
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
#endif
