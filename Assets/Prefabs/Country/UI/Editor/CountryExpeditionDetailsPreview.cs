using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryExpeditionDetailsPreview
{
    private const string Folder = "Artifacts/CountryUI/ExpeditionDetails-v1";
    private static CountryExpeditionPanel Panel => Object.FindAnyObjectByType<CountryExpeditionPanel>();
    private static CountryExpeditionState State => PlayDataManager.Instance.Expeditions;
    private static T Ref<T>(string field) where T : Object => (T)new SerializedObject(Panel).FindProperty(field).objectReferenceValue;

    [MenuItem("WarSim/Country/Expedition Details Preview/Open")]
    public static void Open()
    {
        if (!EditorApplication.isPlaying || Panel == null) throw new InvalidOperationException("Open the Country scene in Play mode first.");
        Panel.Open();
    }

    [MenuItem("WarSim/Country/Expedition Details Preview/Select Own")]
    public static void SelectOwn() => Panel.SelectRegion(State.Data.HomeRegion);

    [MenuItem("WarSim/Country/Expedition Details Preview/Select Distant")]
    public static void SelectDistant() => Panel.SelectRegion(Enumerable.Range(0, State.Data.Regions.Length).Where(i => State.Owner(i) != 0 && !State.CanTarget(i)).OrderByDescending(State.Data.TravelDays).First());

    [MenuItem("WarSim/Country/Expedition Details Preview/Capture")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying || Panel == null || !Panel.IsOpen) throw new InvalidOperationException("Open the expedition map first.");
        Canvas.ForceUpdateCanvases(); var detail = Panel.transform.Find("Window/Shell/Main/Viewport/Content/Map/Details");
        var overflowing = detail.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing && t.overflowMode != TextOverflowModes.Ellipsis).Select(t => t.name + ": " + t.text).ToArray();
        if (overflowing.Length != 0) throw new InvalidOperationException("Expedition details overflow: " + string.Join(", ", overflowing));
        int target = Panel.Draft.Target;
        var schedule = Ref<TextMeshProUGUI>("_schedule");
        bool hasItinerary = target >= 0 && State.Owner(target) != 0;
        if (schedule.gameObject.activeSelf != hasItinerary) throw new InvalidOperationException("Itinerary must match the selected destination.");
        if (target >= 0 && Ref<TextMeshProUGUI>("_targetName").text != State.Data.Regions[target].Name) throw new InvalidOperationException("Selected territory name is incorrect.");
        if (hasItinerary && (!schedule.text.Contains("出発") || !schedule.text.Contains("到着") || !schedule.text.Contains("帰還"))) throw new InvalidOperationException("All three itinerary dates are required.");
        string state = target < 0 ? "Unselected" : State.Owner(target) == 0 ? "Own" : State.Active.Any(trip => trip.Target == target) ? "Active" : State.CanTarget(target) ? "Available" : "Distant";
        Directory.CreateDirectory(Folder); PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string path = Path.GetFullPath($"{Folder}/{state}-{width}x{height}");
        File.WriteAllText(path + ".json", JsonUtility.ToJson(new CaptureReport { State = state, Target = target, Schedule = schedule.text, Overflow = overflowing }, true));
        ScreenCapture.CaptureScreenshot(path + ".png");
        Debug.Log("Expedition details show the correct destination and itinerary without text overflow.");
    }

    [Serializable] private sealed class CaptureReport { public string State, Schedule; public int Target; public string[] Overflow; }
}
