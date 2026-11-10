using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;

public static class CountryBrightnessBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string Records = "Artifacts/CountryUI/Brightness-v2";

    [MenuItem("WarSim/Country/Brightness Preview/Audit Palette")]
    public static void Audit() => UpdatePrefabs(false);
    [MenuItem("WarSim/Country/Brightness Preview/Apply Palette")]
    public static void Apply() => UpdatePrefabs(true);
    [MenuItem("WarSim/Country/Brightness Preview/Restore Defense Red")]
    public static void RestoreDefenseRed()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating prefabs.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryDefense.prefab"))
        {
            var root = scope.prefabContentsRoot;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                var color = (Color32)graphic.color;
                if (color.r == 108 && color.g == 55 && color.b == 54 && color.a == 255) graphic.color = new Color32(86, 38, 39, 255);
                else if (color.r == 56 && color.g == 42 && color.b == 45 && color.a == 255) graphic.color = new Color32(38, 28, 32, 255);
            }
            foreach (var control in root.GetComponentsInChildren<Selectable>(true))
            {
                var colors = control.colors; var normal = (Color32)colors.normalColor;
                if (normal.r != 108 || normal.g != 55 || normal.b != 54 || normal.a != 255) continue;
                var red = (Color)new Color32(86, 38, 39, 255);
                colors.normalColor = colors.selectedColor = red;
                colors.highlightedColor = Color.Lerp(red, Color.white, .1f);
                colors.pressedColor = Color.Lerp(red, Color.black, .22f);
                control.colors = colors;
            }
            foreach (string path in new[] { "Window/Shell/Formation/Main/Selection/Body/Workboard/Light", "Window/Shell/Alert/Card/Workboard/Light" })
            {
                var light = root.transform.Find(path);
                if (light == null) continue;
                light.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.gameObject);
            }
        }
    }

    private static void UpdatePrefabs(bool save)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating prefabs.");
        var log = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }).OrderBy(id => AssetDatabase.GUIDToAssetPath(id)))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("Country", StringComparison.Ordinal)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int graphics = 0, controls = 0;
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    bool bannerPart = Path.GetFileName(path) == "CountryFacilityBanner.prefab" && (graphic.name == "Face" || graphic.name == "Pointer");
                    if ((!bannerPart && PrefabUtility.IsPartOfPrefabInstance(graphic)) || graphic is TMP_Text) continue;
                    if (graphic is UnityEngine.UI.Image image && image.sprite != null &&
                        !bannerPart && !AssetDatabase.GetAssetPath(image.sprite).EndsWith("/Shapes/RoundedPanel.png", StringComparison.Ordinal)) continue;
                    if (graphic is not UnityEngine.UI.Image && graphic is not CountryGovernmentPlate) continue;
                    if (!TryBrighten(graphic.color, out Color color)) continue;
                    log.AppendLine($"  {Path.GetFileName(path)} / {AnimationUtility.CalculateTransformPath(graphic.transform, root.transform)}: {(Color32)graphic.color} -> {(Color32)color}");
                    graphic.color = color;
                    if (bannerPart) PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
                    graphics++;
                }
                foreach (var control in root.GetComponentsInChildren<Selectable>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(control)) continue;
                    var colors = control.colors;
                    if (!TryBrighten(colors.normalColor, out Color normal)) continue;
                    log.AppendLine($"  {Path.GetFileName(path)} / {AnimationUtility.CalculateTransformPath(control.transform, root.transform)} [control]: {(Color32)colors.normalColor} -> {(Color32)normal}");
                    var delta = normal - colors.normalColor;
                    colors.normalColor = normal;
                    colors.highlightedColor = Add(colors.highlightedColor, delta);
                    colors.selectedColor = Add(colors.selectedColor, delta);
                    colors.pressedColor = Add(colors.pressedColor, delta);
                    control.colors = colors; controls++;
                }
                bool board = Path.GetFileName(path) == "CountryWorkboard.prefab";
                if (board)
                {
                    var surface = root.transform.Find("Surface");
                    var light = Plate(Fill(root.transform, "Light"), new Color32(190, 202, 214, 42), 14);
                    light.transform.SetSiblingIndex(surface.GetSiblingIndex() + 1);
                }
                if (graphics == 0 && controls == 0 && !board) continue;
                log.AppendLine($"{path}: {graphics} backgrounds, {controls} controls" + (board ? ", fabric overlay" : ""));
                if (!save) continue;
                string backup = Path.Combine(Records, "before", Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Directory.CreateDirectory(Records);
        File.WriteAllText(Path.Combine(Records, save ? "changes.txt" : "audit.txt"), log.ToString());
        Debug.Log($"Country brightness {(save ? "updated" : "audited")} without saving or reopening the scene.\n{log}");
    }

    private static Color Add(Color color, Color delta) => new Color(
        Mathf.Clamp01(color.r + delta.r), Mathf.Clamp01(color.g + delta.g), Mathf.Clamp01(color.b + delta.b), color.a);

    private static bool TryBrighten(Color source, out Color result)
    {
        var value = (Color32)source;
        result = source;
        if (value.a < 225 && !(value.r == 109 && value.g == 125 && value.b == 145 && value.a == 110)) return false;
        var rgb = (value.r, value.g, value.b);
        var brighter = rgb switch {
            (49, 58, 70) => (68, 79, 93), (64, 76, 91) => (83, 96, 111),
            (80, 94, 110) => (98, 112, 128), (96, 111, 128) => (110, 126, 143),
            (39, 48, 61) => (56, 67, 81), (109, 125, 145) => (126, 142, 161),
            (65, 73, 85) => (84, 94, 107), (95, 104, 117) => (112, 123, 137),
            (105, 115, 128) => (121, 133, 147), (75, 84, 97) => (93, 104, 118),
            (65, 78, 93) => (83, 97, 112), (44, 58, 74) => (62, 75, 91),
            (83, 97, 111) => (100, 114, 129), (41, 51, 63) => (58, 69, 82),
            (83, 95, 110) => (100, 112, 127), (64, 78, 94) => (82, 96, 111), _ => (-1, -1, -1)
        };
        if (brighter.Item1 < 0) return false;
        result = new Color32((byte)brighter.Item1, (byte)brighter.Item2, (byte)brighter.Item3, value.a);
        return true;
    }
}
