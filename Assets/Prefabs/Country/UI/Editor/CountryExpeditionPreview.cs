using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryExpeditionPreview
{
    private const string Records = "Artifacts/CountryUI/Expeditions-v4";
    private static CountryExpeditionPanel Panel => Object.FindAnyObjectByType<CountryExpeditionPanel>();
    private static CountryExpeditionState State => PlayDataManager.Instance.Expeditions;

    [MenuItem("WarSim/Country/Expedition Preview/Verify Rules")]
    public static void VerifyRules()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Verify rules outside Play mode.");
        var results = new List<TestResult>();
        foreach (string suiteName in new[] { "CountryWorldMapTests", "CountryExpeditionTests", "CountryRosterTests", "CountryIncomeTests" })
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(suiteName)).First(item => item != null);
            var methods = type.GetMethods();
            var setup = methods.Where(method => Has(method, "SetUpAttribute")).ToArray();
            var cleanup = methods.Where(method => Has(method, "TearDownAttribute")).ToArray();
            foreach (var method in methods)
            {
                var cases = method.GetCustomAttributes(false).Where(attr => attr.GetType().Name == "TestCaseAttribute").ToArray();
                if (!Has(method, "TestAttribute") && cases.Length == 0) continue;
                foreach (var args in cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(attr => (object[])attr.GetType().GetProperty("Arguments").GetValue(attr)))
                {
                    object suite = Activator.CreateInstance(type);
                    var result = new TestResult { Name = suiteName + "." + method.Name + (args.Length > 0 ? "(" + string.Join(",", args) + ")" : "") };
                    try { foreach (var before in setup) before.Invoke(suite, null); method.Invoke(suite, args); result.Passed = true; }
                    catch (Exception error) { result.Error = (error.InnerException ?? error).ToString(); }
                    finally { foreach (var after in cleanup) after.Invoke(suite, null); }
                    results.Add(result);
                }
            }
        }
        Directory.CreateDirectory(Records); File.WriteAllText(Records + "/rules.json", JsonUtility.ToJson(new RuleReport { Tests = results.ToArray() }, true));
        if (results.Any(result => !result.Passed)) throw new InvalidOperationException("Expedition rule checks failed. See rules.json.");
        Debug.Log($"Expedition, roster and income: {results.Count} checks passed.");
    }
    private static bool Has(MethodInfo method, string attribute) => method.GetCustomAttributes(false).Any(attr => attr.GetType().Name == attribute);

    [MenuItem("WarSim/Country/Expedition Preview/Select Nearest")]
    public static void SelectNearest() => Panel.SelectRegion(Enumerable.Range(0, State.Data.Regions.Length).Where(State.CanTarget).OrderBy(State.Data.TravelDays).First());
    [MenuItem("WarSim/Country/Expedition Preview/Select Another")]
    public static void SelectAnother() => Panel.SelectRegion(Enumerable.Range(0, State.Data.Regions.Length).Where(State.CanTarget).OrderByDescending(State.Data.TravelDays).First());
    [MenuItem("WarSim/Country/Expedition Preview/Select All Available Members")]
    public static void AllMembers()
    {
        for (int i = 0; i < PlayDataManager.Instance.Roster.Soldiers.Count; i++)
            if (!Panel.Draft.Contains(i) && State.CanSelect(i, Panel.Draft.Target, out _)) Panel.Draft.Toggle(i);
        Panel.SelectRegion(Panel.Draft.Target);
    }
    [MenuItem("WarSim/Country/Expedition Preview/Advance To First Arrival")]
    public static void FirstArrival() => Advance(State.Active.OrderBy(trip => trip.Arrival.MonthIndex).ThenBy(trip => trip.Arrival.Day).First().Arrival);
    [MenuItem("WarSim/Country/Expedition Preview/Advance To Last Return")]
    public static void LastReturn() => Advance(State.Active.OrderByDescending(trip => trip.Return.MonthIndex).ThenByDescending(trip => trip.Return.Day).First().Return);
    [MenuItem("WarSim/Country/Expedition Preview/Advance To Next Month")]
    public static void NextMonth()
    {
        var date = CountryDate.From(DateManager.Instance);
        Advance(new CountryDate(date.MonthIndex + 1, date.Month % 12 + 1, 1));
    }
    private static void Advance(CountryDate due)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Advance only the current Play session.");
        var date = DateManager.Instance;
        float interval = new SerializedObject(date).FindProperty("_secondsPerDay").floatValue;
        Panel.Close();
        while (CountryDate.From(date).CompareTo(due) < 0)
        {
            Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
            date.Resume(); date.AdvanceTime(interval); date.Pause();
        }
    }

    [MenuItem("WarSim/Country/Expedition Preview/Verify Map Hits")]
    public static void VerifyMapHits()
    {
        var graphics = Panel.GetComponentsInChildren<CountryWorldRegionGraphic>(true).Where(graphic => graphic.name.StartsWith("Region", StringComparison.Ordinal)).OrderBy(graphic => graphic.name).ToArray();
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < graphics.Length; i++)
        {
            var rect = graphics[i].rectTransform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.min + Vector2.Scale(State.Data.Regions[i].Center, rect.rect.size)));
            if (!graphics[i].Raycast(point, null)) throw new InvalidOperationException("Map hit failed for " + i);
            for (int j = 0; j < graphics.Length; j++)
                if (i != j && graphics[j].Raycast(point, null)) throw new InvalidOperationException("Map hit overlaps " + i + "/" + j);
        }
        Debug.Log($"All {graphics.Length} map center hits match the territory polygons.");
    }

    [MenuItem("WarSim/Country/Expedition Preview/Verify Shared Layout")]
    public static void VerifySharedLayout()
    {
        const string folder = "Assets/Prefabs/Country/UI/";
        foreach (var entry in new[] { (Name: "CountryExpedition", Selection: "Window/Shell/Main/Formation/Selection"), (Name: "CountryTraining", Selection: "Selection") })
        {
            string path = folder + entry.Name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.transform.Find(entry.Selection) == null || !AssetDatabase.GetDependencies(path, false).Contains(folder + "CountryRosterSelection.prefab"))
                throw new InvalidOperationException("Training and expedition must share the selection prefab.");
        }
        Debug.Log("Training and expedition share the same selection layout prefab.");
    }

    [MenuItem("WarSim/Country/Expedition Preview/Verify Scroll")]
    public static void VerifyScroll()
    {
        var main = Panel.transform.Find("Window/Shell/Main");
        if (!main.gameObject.activeInHierarchy) throw new InvalidOperationException("Open the expedition members page first.");
        var selection = main.Find("Formation/Selection");
        var roster = selection.Find("Body/Roster");
        var list = roster.Find("Left/List").GetComponent<ScrollRect>();
        var details = roster.Find("Right/Body/Details").GetComponent<ScrollRect>();
        var members = selection.Find("Members").GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases();
        var result = new ScrollReport { List = Step(list, Vector2.down), Details = Step(details, Vector2.down), Horizontal = Step(members, Vector2.left) };
        Directory.CreateDirectory(Records);
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        File.WriteAllText($"{Records}/scroll-{width}x{height}.json", JsonUtility.ToJson(result, true));
        if (Mathf.Abs(result.List - list.scrollSensitivity) > .1f ||
            Mathf.Abs(result.Details - details.scrollSensitivity) > .1f ||
            Mathf.Abs(result.Horizontal - members.scrollSensitivity) > .1f)
            throw new InvalidOperationException("The soldier list, details or member strip did not scroll by the configured step.");
        Debug.Log("The expedition list and details scroll vertically and the member strip scrolls horizontally.");

        float Step(ScrollRect scroll, Vector2 delta)
        {
            if (scroll.vertical) scroll.verticalNormalizedPosition = 1;
            else scroll.horizontalNormalizedPosition = 0;
            var before = scroll.content.anchoredPosition;
            ExecuteEvents.ExecuteHierarchy(scroll.viewport.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = delta }, ExecuteEvents.scrollHandler);
            return Mathf.Abs(Vector2.Dot(scroll.content.anchoredPosition - before, delta));
        }
    }

    [MenuItem("WarSim/Country/Expedition Preview/Capture")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture only the current Play session.");
        Canvas.ForceUpdateCanvases(); Directory.CreateDirectory(Records);
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string path = $"{Records}/Expedition-{width}x{height}";
        var manager = PlayDataManager.Instance; var state = manager.Expeditions;
        var government = Object.FindAnyObjectByType<CountryGovernmentPanel>();
        var report = new CaptureReport {
            Open = Panel.IsOpen, GovernmentOpen = government.IsOpen, Running = DateManager.Instance.IsRunning,
            CameraInput = government.CameraControls.InputEnabled, Today = CountryDate.From(DateManager.Instance).ToString(),
            RegionCount = state.Data.Regions.Length,
            NationLabels = Panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(text => text.name == "Text" && text.transform.parent.name.StartsWith("Nation", StringComparison.Ordinal)).Select(text => new NationLabelReport { Name = text.text, Material = text.fontSharedMaterial.name, OutlineWidth = text.fontSharedMaterial.GetFloat("_OutlineWidth"), Face = text.color, Outline = text.fontSharedMaterial.GetColor("_OutlineColor") }).ToArray(),
            Target = Panel.Draft?.Target ?? -1, Members = Panel.Draft?.Members.ToArray() ?? Array.Empty<int>(),
            Owners = Enumerable.Range(0, state.Data.Regions.Length).Select(state.Owner).ToArray(),
            Available = Enumerable.Range(0, state.Data.Regions.Length).Where(state.CanTarget).ToArray(),
            Active = state.Active.Select(Trip).ToArray(), Returned = state.Returned.Select(Trip).ToArray(),
            Weapons = Enumerable.Range(0, manager.Roster.Soldiers.Count).Select(manager.Roster.EquippedWeapon).ToArray(),
            Deployable = Enumerable.Range(0, manager.Roster.Soldiers.Count).Where(manager.Roster.CanDeploy).ToArray(),
            Capitals = Panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Capital", StringComparison.Ordinal)),
            TerrainImages = Panel.GetComponentsInChildren<Image>(true).Count(image => image.sprite != null && image.sprite.name.Contains("Terrain")),
            EmptySlots = Panel.GetComponentsInChildren<UnityEngine.UI.Image>().Count(image => image.sprite != null && image.sprite.name == "Roster-EmptySlot"),
            TextOverflows = Object.FindObjectsByType<TextMeshProUGUI>().Where(text => text.isActiveAndEnabled && text.isTextOverflowing).Select(text => text.name + ": " + text.text).ToArray()
        };
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true)); ScreenCapture.CaptureScreenshot(Path.GetFullPath(path + ".png"));
    }
    private static TripReport Trip(CountryExpeditionTrip trip) => new() { Target = trip.Target, Returning = trip.Returning, Departure = trip.Departure.ToString(), Arrival = trip.Arrival.ToString(), Return = trip.Return.ToString(), Members = trip.Members.ToArray(), ReportMonth = trip.ReportMonth };
    [Serializable] private sealed class TestResult { public string Name, Error; public bool Passed; }
    [Serializable] private sealed class RuleReport { public TestResult[] Tests; }
    [Serializable] private sealed class ScrollReport { public float List, Details, Horizontal; }
    [Serializable] private sealed class TripReport { public int Target, ReportMonth; public bool Returning; public string Departure, Arrival, Return; public int[] Members; }
    [Serializable] private sealed class NationLabelReport { public string Name, Material; public float OutlineWidth; public Color Face, Outline; }
    [Serializable] private sealed class CaptureReport { public int RegionCount; public NationLabelReport[] NationLabels; public bool Open, GovernmentOpen, Running, CameraInput; public string Today; public int Target, Capitals, TerrainImages, EmptySlots; public int[] Members, Owners, Available, Weapons, Deployable; public TripReport[] Active, Returned; public string[] TextOverflows; }
}
