using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Theme = CountryGovernmentTheme;

public static class CountryPaletteBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";

    [MenuItem("WarSim/Country/Apply Planned Palette")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the palette.");
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Folder.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        var visited = new HashSet<string>();
        foreach (string path in paths) UpdatePrefab(path);
        var scene = SceneManager.GetActiveScene();
        if (scene.path == "Assets/Scenes/Country.unity")
            foreach (var root in scene.GetRootGameObjects())
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "CountryHud") { Apply(rect.gameObject); break; }
        Debug.Log($"Country palette applied to {visited.Count} prefabs and the existing HUD. The scene was not saved or reloaded.");

        void UpdatePrefab(string path)
        {
            if (!path.StartsWith(Folder) || !path.EndsWith(".prefab") || !visited.Add(path)) return;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false)) UpdatePrefab(dependency);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            Apply(scope.prefabContentsRoot);
        }
    }

    internal static void Apply(GameObject root)
    {
        foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            graphic.color = Remap(graphic.color, graphic is TMP_Text);
            if (graphic is TMP_Text text)
                text.text = text.text.Replace("#E0C894", "#" + ColorUtility.ToHtmlStringRGB(Theme.Text))
                    .Replace("#EFA597", "#" + ColorUtility.ToHtmlStringRGB(Theme.Error));
            Record(graphic);
        }
        foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) Style(button);
        foreach (var toggle in root.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
        { Theme.Colors(toggle, Color.white); Record(toggle); }
        foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            input.targetGraphic.color = Theme.Recess; Theme.Colors(input, Color.white);
            input.textComponent.color = Theme.Text;
            input.customCaretColor = true; input.caretColor = Theme.Text;
            if (input.placeholder != null) { input.placeholder.color = Theme.Muted; Record(input.placeholder); }
            Record(input.targetGraphic); Record(input.textComponent); Record(input);
        }
        foreach (var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
        {
            var fill = slider.fillRect?.GetComponent<UnityEngine.UI.Graphic>();
            if (fill != null) { fill.color = Theme.Face; Record(fill); }
        }
        foreach (var panel in root.GetComponentsInChildren<CountryGovernmentPanel>(true))
        {
            var paper = panel.transform.Find("Window/Shell/Main/Income");
            if (paper != null)
                foreach (var text in paper.GetComponentsInChildren<TMP_Text>(true)) { text.color = Theme.Ink; Record(text); }
            foreach (string name in new[] { "_taxGain", "_woodGain", "_oreGain" }) FieldColor(panel, name, Theme.PositiveInk);
            FieldColor(panel, "_incomeState", Theme.PaperMuted);
        }
        foreach (var defense in root.GetComponentsInChildren<CountryDefensePanel>(true))
        {
            var formation = defense.transform.Find("Window/Shell/Formation");
            formation.GetComponent<UnityEngine.UI.Graphic>().color = Theme.Background;
            foreach (string name in new[] { "Header", "Footer" })
            {
                var graphic = formation.Find(name).GetComponent<UnityEngine.UI.Graphic>();
                graphic.color = Theme.Panel; Record(graphic);
            }
            foreach (var text in formation.GetComponentsInChildren<TMP_Text>(true))
                if (((Color32)text.color).Equals(new Color32(219, 150, 111, 255))) { text.color = Theme.Muted; Record(text); }
            var board = formation.Find("Main/Selection/Body/Workboard");
            var surface = board.Find("Surface").GetComponent<UnityEngine.UI.Graphic>();
            var rim = board.Find("Rim").GetComponent<UnityEngine.UI.Graphic>();
            surface.color = new Color(.82f, .82f, .82f); rim.color = Color.white;
            var light = board.Find("Light");
            if (light != null) { light.gameObject.SetActive(true); Record(light.gameObject); }
            Record(surface); Record(rim); Record(formation.GetComponent<UnityEngine.UI.Graphic>());
        }
    }

    private static void Style(UnityEngine.UI.Button button)
    {
        var face = button.targetGraphic;
        if (face == null || face.color.a < .01f) return;
        if (face is CountryWorldRegionGraphic) return;
        if (button.name is "Dim" or "Dismiss" or "Backdrop")
        { face.color = new Color(.03f, .04f, .06f, .72f); Record(face); return; }
        var image = face as UnityEngine.UI.Image;
        string art = image != null && image.sprite != null ? image.sprite.texture.name : "";
        if (art == "RoundedPanel" && button.name is ("Back" or "ReturnToCountry")) return;
        bool row = button.GetComponent<CountryRosterRow>() != null || button.GetComponent<CountryGovernmentChoice>() != null;
        if (button.GetComponent<CountryGovernmentChoice>() != null)
        {
            var marker = (RectTransform)button.transform.Find("Selected");
            marker.anchoredPosition = new Vector2(4, marker.anchoredPosition.y); Record(marker);
        }
        bool menu = button.transform.parent != null && button.transform.parent.name == "Buttons";
        bool tab = art == "GameTab-v1";
        bool paper = art == "CommandMotifParts-v1" || art == "TrainingPennantParts-v1";
        bool material = paper || art == "ConstructionSignParts-v1" || art == "ExpeditionParts-v1" || art == "MapSymbols-v1" || art == "Capital-Crown-v1";
        bool information = button.name == "Details" || face.transform.parent != null && face.transform.parent.name == "Art";
        Color normal = Theme.Face, label = Theme.Ink;
        if (row || information) { normal = Color.white; label = Theme.Text; }
        if (row) { face.color = Color.white; normal = Theme.Panel; }
        else if (information) face.color = Theme.Panel;
        else face.color = Color.white;
        if (material) { normal = Color.white; label = paper ? Theme.Ink : Theme.Text; }
        if (menu) label = Theme.Text;
        if (button.name is "Back" or "Close" or "Cancel") { normal = Theme.Panel; label = Theme.Text; }
        if (button.name == "PlayButton") { normal = Theme.Play; label = Theme.Text; }
        if (button.name is "PauseButton" or "Stop" or "StopButton") { normal = Theme.Stop; label = Theme.Text; }
        if (button.name is "Attack" or "Defend" or "Fight") { normal = Theme.Stop; label = Theme.Text; }
        Theme.Colors(button, normal);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) { text.color = label; Record(text); }
        if (button.name is "Settings" or "SettingsButton")
        {
            foreach (var icon in button.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (icon.sprite != null && icon.sprite.texture.name == "settings-v1") { icon.color = Theme.Ink; Record(icon); }
        }
        if (button.name is "PlayButton" or "PauseButton" or "MonthEndButton")
        {
            var inset = button.transform.Find("InsetFrame")?.GetComponent<UnityEngine.UI.Graphic>();
            if (inset != null) { inset.color = Color.clear; Record(inset); }
        }
        var focus = button.transform.Find("Focus")?.GetComponent<UnityEngine.UI.Graphic>();
        if (focus != null) { focus.color = label == Theme.Ink ? Theme.Ink : Theme.Text; Record(focus); }
        var frame = button.transform.Find("Frame")?.GetComponent<UnityEngine.UI.Graphic>();
        if (frame != null) { frame.color = Theme.Border; Record(frame); }
        if (button.TryGetComponent<CanvasGroup>(out var group) && Mathf.Approximately(group.alpha, .45f))
        { group.alpha = 1; Record(group); }
        if (tab) Theme.SelectTab(button, false);
        Record(face); Record(button);
    }

    private static Color Remap(Color value, bool text)
    {
        if (value.a < .01f) return value;
        var old = (Color32)value;
        Color replacement = value;
        switch ((old.r << 16) | (old.g << 8) | old.b)
        {
            case 0x444F5D: replacement = Theme.Background; break;
            case 0x53606F: replacement = Theme.Panel; break;
            case 0x627080: replacement = Theme.Face; break;
            case 0x6E7E8F: replacement = Theme.Selected; break;
            case 0xF1EEE5: replacement = Theme.Text; break;
            case 0xAEB9C7: replacement = Theme.Muted; break;
            case 0xD6DEE7: if (text) replacement = Theme.Muted; break;
            case 0xBEC8D7: if (text) replacement = Theme.Muted; break;
            case 0xE8EDF4:
            case 0xDEE7EB: if (text) replacement = Theme.Text; break;
            case 0xE0C894:
            case 0xE2BD68: replacement = text ? Theme.Text : Theme.Accent; break;
            case 0x718296: replacement = Theme.Border; break;
            case 0x384351: replacement = text ? Theme.Ink : Theme.Recess; break;
            case 0xA1C9AD: replacement = Theme.Positive; break;
            case 0xEFA597: replacement = Theme.Error; break;
            case 0x2E3033: replacement = Theme.DisabledFace; break;
            case 0x707B89: replacement = Theme.Panel; break;
            case 0x798593: replacement = Theme.Face; break;
            case 0x282E30: if (text) replacement = Theme.Ink; break;
            case 0x5B605C: if (text) replacement = Theme.PaperMuted; break;
            case 0x795333: if (text) replacement = Theme.Ink; break;
        }
        replacement.a = value.a;
        return replacement;
    }

    private static void FieldColor(Component owner, string name, Color color)
    {
        var graphic = new SerializedObject(owner).FindProperty(name)?.objectReferenceValue as UnityEngine.UI.Graphic;
        if (graphic == null) return;
        graphic.color = color; Record(graphic);
    }

    private static void Record(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
}
