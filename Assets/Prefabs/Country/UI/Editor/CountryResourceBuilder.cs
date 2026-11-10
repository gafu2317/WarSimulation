using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryResourceBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";

    [MenuItem("WarSim/Country/Set Up Fixed Resource Columns")]
    public static void BuildLayout()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating resource columns.");
        using var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTopBar.prefab");
        CountryTextLayoutBuilder.Apply("CountryTopBar", scope.prefabContentsRoot);
    }

    [MenuItem("WarSim/Country/Set Up Resource Readouts")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Country.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Country scene outside Play mode first.");
        var resources = Object.FindFirstObjectByType<PlayDataManager>();
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTopBar.prefab"))
        {
            var content = scope.prefabContentsRoot.transform.Find("Content");
            SetText(content, "Population", CountryResourceDisplay.Format(resources.Population));
            SetText(content, "TaxIncome", CountryResourceDisplay.Format(resources.Treasury));
            var treasuryLabel = content.Find("TaxIncome/TextBlock/Label").GetComponent<TextMeshProUGUI>();
            treasuryLabel.text = "国庫";
            PrefabUtility.RecordPrefabInstancePropertyModifications(treasuryLabel);
            SetText(content, "Wood", CountryResourceDisplay.Format(resources.Wood));
            SetText(content, "Ore", CountryResourceDisplay.Format(resources.Ore));
            CountryTextLayoutBuilder.Apply("CountryTopBar", scope.prefabContentsRoot);
        }
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot;
            var hud = root.GetComponent<CountryResourceHud>() ?? root.AddComponent<CountryResourceHud>();
            var data = new SerializedObject(hud);
            var content = root.transform.Find("TopBar/Content");
            foreach (var entry in new[] { ("_population", "Population"), ("_taxIncome", "TaxIncome"), ("_wood", "Wood"), ("_ore", "Ore") })
                data.FindProperty(entry.Item1).objectReferenceValue = content.Find(entry.Item2 + "/TextBlock/Value").GetComponent<TextMeshProUGUI>();
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
    }

    private static void SetText(Transform content, string name, string value)
    {
        var label = content.Find(name + "/TextBlock/Value").GetComponent<TextMeshProUGUI>();
        label.text = value;
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
    }
}
