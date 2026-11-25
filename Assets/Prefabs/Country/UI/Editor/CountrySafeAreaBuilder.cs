using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CountrySafeAreaBuilder
{
    [MenuItem("WarSim/Country/Update Safe Area Target List")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the HUD prefab.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryHud.prefab"))
        {
            var root = scope.prefabContentsRoot;
            var layout = root.GetComponent<CountrySafeArea>() ?? root.AddComponent<CountrySafeArea>();
            var targets = new List<CountrySafeArea.Target>();
            void Add(string path, bool horizontal = true, bool vertical = true)
            {
                var rect = root.transform.Find(path) as RectTransform;
                if (rect == null) throw new InvalidOperationException("Missing safe-area target: " + path);
                targets.Add(new CountrySafeArea.Target {
                    Rect = rect, Min = rect.anchorMin, Max = rect.anchorMax,
                    Horizontal = horizontal, Vertical = vertical });
            }
            Add("FacilityLayer/Banners");
            Add("TopBar", false);
            Add("BottomLeftButtons"); Add("BottomRightButton"); Add("Minimap");
            foreach (var screen in new[] { "Government", "Personnel", "Expeditions", "Defense", "Settings" }) Add(screen, false, false);
            Add("DepartureNotice"); Add("TopBar/Content", true, false);
            foreach (var screen in new[] { "Government", "Personnel", "Expeditions" }) Add(screen + "/Window/Shell");
            foreach (var path in new[] {
                "Government/Window/Details/Card", "Government/Window/TrainingConfirm/Window/Card", "Government/Window/ExpeditionDetails/Window/Card",
                "Personnel/Window/Confirm/Window/Card", "Expeditions/Window/Confirm/Window/Card", "Expeditions/Window/Log/Card", "Expeditions/Window/Log/Details/Window/Card",
                "Defense/Window/Shell/Formation/Header", "Defense/Window/Shell/Formation/Main", "Defense/Window/Shell/Formation/Footer",
                "Defense/Window/Shell/Alert/Card", "Defense/Window/Confirm/Window/Card", "Settings/Window/Panel",
                "FacilityLayer/Popup/Modal/Panel" }) Add(path);
            layout.Configure(targets.ToArray());
            EditorUtility.SetDirty(layout);
        }
        Debug.Log("Country HUD safe-area targets updated in the prefab.");
    }

    [MenuItem("WarSim/Country/Verify Phone Safe Area")]
    public static void Verify()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit mode for the isolated HUD checks.");
        var results = new List<object>();
        var failures = new List<Exception>();
        var canvasScales = Resources.FindObjectsOfTypeAll<Canvas>()
            .Where(c => c.isRootCanvas && c.gameObject.scene.isLoaded && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(c.gameObject.scene))
            .Select(c => (canvas: c, scale: c.scaleFactor)).ToArray();
        try
        {
            foreach (string name in new[] { "CountrySafeAreaTests", "CountryTimeControlsTests" })
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).First(t => t != null);
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var setup = methods.First(m => m.GetCustomAttributes(false).Any(a => a.GetType().FullName == "NUnit.Framework.SetUpAttribute"));
                var teardown = methods.First(m => m.GetCustomAttributes(false).Any(a => a.GetType().FullName == "NUnit.Framework.TearDownAttribute"));
                foreach (var test in methods.Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().FullName == "NUnit.Framework.TestAttribute")))
                {
                    var instance = Activator.CreateInstance(type);
                    Exception failure = null;
                    try { setup.Invoke(instance, null); test.Invoke(instance, null); }
                    catch (Exception error) { failure = error.InnerException ?? error; }
                    finally
                    {
                        try { teardown.Invoke(instance, null); }
                        catch (Exception error) { failure = error.InnerException ?? error; }
                    }
                    results.Add(new { suite = name, test = test.Name, passed = failure == null, error = failure?.ToString() });
                    if (failure != null) failures.Add(failure);
                }
            }
        }
        finally
        {
            foreach (var (canvas, scale) in canvasScales)
                if (canvas != null) canvas.scaleFactor = scale;
        }
        const string folder = ".unity/test-results";
        Directory.CreateDirectory(folder);
        File.WriteAllText(folder + "/country-phone-ui.json", Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
        if (failures.Count > 0) throw new AggregateException(failures);
        Debug.Log($"Phone safe-area checks: {results.Count} passed without saving or reloading the open scene.");
    }
}
