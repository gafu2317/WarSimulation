using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryDefensePreview
{
    private const string Folder = "Artifacts/CountryUI/Defense-v1";
    private static CountryDefensePanel Panel => Object.FindAnyObjectByType<CountryDefensePanel>();
    private static PlayDataManager Data => PlayDataManager.Instance;
    private static T Ref<T>(Object target, string field) where T : Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;

    [MenuItem("WarSim/Country/Defense Preview/Open Formation")]
    public static void Open()
    {
        RequirePlay(); Object.FindAnyObjectByType<CountryPersonnelPanel>().OpenSoldiers();
        Ref<Button>(Object.FindAnyObjectByType<CountryPersonnelPanel>(), "_defenseTab").onClick.Invoke();
    }

    [MenuItem("WarSim/Country/Defense Preview/Advance To Raid")]
    public static void AdvanceToRaid()
    {
        RequirePlay(); Panel.Close();
        for (int i = 0; i < 40 && Data.Defense.Pending == null; i++) AdvanceDay();
        Check(Data.Defense.Pending != null && Panel.IsOpen && !DateManager.Instance.IsRunning, "Raid must open its popup and pause the clock.");
    }
    private static void AdvanceDay()
    {
        Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
        var date = DateManager.Instance;
        float interval = new SerializedObject(date).FindProperty("_secondsPerDay").floatValue;
        date.Resume(); date.AdvanceTime(interval); date.Pause();
    }

    [MenuItem("WarSim/Country/Defense Preview/Capture")]
    public static void Capture()
    {
        RequirePlay(); Canvas.ForceUpdateCanvases(); Directory.CreateDirectory(Folder);
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string page = Ref<GameObject>(Panel, "_formation").activeInHierarchy ? "Formation" : Panel.IsOpen ? "Raid" : "Settings";
        string path = Path.GetFullPath($"{Folder}/{page}-{width}x{height}");
        var target = Panel.IsOpen ? Panel.transform : Object.FindAnyObjectByType<CountrySettingsPanel>().transform;
        var overflows = target.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing && text.overflowMode != TextOverflowModes.Ellipsis).Select(text => text.name + ": " + text.text).ToArray();
        var report = new CaptureReport { Page = page, Date = CountryDate.From(DateManager.Instance).ToString(), Running = DateManager.Instance.IsRunning,
            Members = Data.Defense.Members.ToArray(), Weapons = Data.Defense.Weapons.ToArray(), CP = Data.Roster.Soldiers.Select(s => s.CP).ToArray(),
            TextOverflows = overflows, RaidsEnabled = Data.RaidsEnabled };
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true)); ScreenCapture.CaptureScreenshot(path + ".png");
        Check(overflows.Length == 0, "Visible text must fit: " + string.Join("; ", overflows));
    }
    private static void RequirePlay() => Check(EditorApplication.isPlaying && Data?.Defense != null, "Open the Country scene in Play mode first.");
    private static void Check(bool accepted, string message) { if (!accepted) throw new InvalidOperationException(message); }
    [Serializable] private sealed class CaptureReport { public string Page, Date; public bool Running, RaidsEnabled; public int[] Members, Weapons, CP; public string[] TextOverflows; }
}
