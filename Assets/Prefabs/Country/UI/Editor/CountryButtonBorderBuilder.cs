using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CountryButtonBorderBuilder
{
    internal const string MaterialPath = "Assets/Prefabs/Country/UI/Materials/CountryButtonBorder.mat";
    internal static Material Material => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
    internal static Color Shadow => new Color(.28f, .34f, .42f, .3f);

    [MenuItem("WarSim/Country/Lighten Button Borders")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || Material == null) throw new InvalidOperationException("Use Edit mode with the shared button material.");
        const string folder = "Assets/Prefabs/Country/UI/";
        var visited = new HashSet<string>();
        var changed = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder.TrimEnd('/') })) Update(AssetDatabase.GUIDToAssetPath(guid));
        Debug.Log($"Button borders updated in {changed.Count} shared prefabs. The open scene was not saved or reloaded.");

        void Update(string path)
        {
            if (!path.StartsWith(folder) || !path.EndsWith(".prefab") || !visited.Add(path)) return;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false)) Update(dependency);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (Apply(root) == 0) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static int Apply(GameObject root)
    {
        int changed = 0;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.GetComponentInParent<Button>(true) == null) continue;
            bool shape = image.sprite.texture.name == "GameTab-v1" || image.sprite.name is "Roster-RowFace" or "Roster-RowFrame";
            if (!shape) continue;
            if (image.name == "Shadow")
            {
                if (image.color == Shadow) continue;
                image.color = Shadow;
            }
            else
            {
                if (image.material == Material) continue;
                image.material = Material;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            changed++;
        }
        return changed;
    }
}
