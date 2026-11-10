using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryRaidDispatchPreview
{
    private const string Folder = "Artifacts/CountryUI/RaidDispatch-v7";
    private static CountryDefensePanel Panel => Object.FindAnyObjectByType<CountryDefensePanel>();
    private static T Ref<T>(string name) where T : Object => (T)new SerializedObject(Panel).FindProperty(name).objectReferenceValue;

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Prepare")]
    public static void Prepare()
    {
        if (!EditorApplication.isPlaying || PlayDataManager.Instance == null) throw new InvalidOperationException("Open the Country scene in Play mode first.");
        if (!PlayDataManager.Instance.Defense.TryRegister(new[] { 0, 1 }, new[] { 0, 1 }, out string reason)) throw new InvalidOperationException(reason);
        CountryDefensePreview.AdvanceToRaid();
    }

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Show Empty")]
    public static void ShowEmpty() => SetMembers(Array.Empty<int>());

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Show Full")]
    public static void ShowFull() => SetMembers(Enumerable.Range(0, 26).ToArray());

    private static void SetMembers(int[] members)
    {
        Panel.Close();
        if (!PlayDataManager.Instance.Defense.TryRegister(members, members, out string reason)) throw new InvalidOperationException(reason);
        Panel.Open(); Ref<Button>("_back").onClick.Invoke();
        if (!Panel.IsOpen || Ref<GameObject>("_formation").activeInHierarchy) throw new InvalidOperationException("Raid popup must reopen.");
        if (Ref<Button>("_fight").interactable != (members.Length > 0)) throw new InvalidOperationException("Defense must require members.");
    }

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Show Unavailable")]
    public static void ShowUnavailable()
    {
        Panel.Close();
        if (!PlayDataManager.Instance.Roster.TrySetCP(0, 0)) throw new InvalidOperationException("The member must become unavailable.");
        Panel.Open(); Ref<Button>("_back").onClick.Invoke();
        if (Ref<Button>("_fight").interactable || string.IsNullOrEmpty(Ref<TextMeshProUGUI>("_raidSummary").text)) throw new InvalidOperationException("Unavailable members need a reason and cannot defend.");
    }

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Verify Edit")]
    public static void VerifyEdit()
    {
        ShowEmpty(); Ref<Button>("_edit").onClick.Invoke();
        var view = Ref<CountryRosterView>("_view"); view.SelectSoldier(0);
        var action = (Button)new SerializedObject(view).FindProperty("_action").objectReferenceValue;
        action.onClick.Invoke();
        if (PlayDataManager.Instance.Defense.Members.Count != 1 || PlayDataManager.Instance.Defense.Members[0] != 0) throw new InvalidOperationException("Editing must update the registered defense formation.");
        Ref<Button>("_back").onClick.Invoke();
        var content = Ref<RectTransform>("_raidMembers");
        if (content.GetComponentsInChildren<CountryRosterRow>().Length != 1 || !Ref<Button>("_fight").interactable) throw new InvalidOperationException("The raid popup must display the edited member.");
        Debug.Log("Raid popup updates when the defense formation is edited.");
    }

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Scroll To Last")]
    public static void ScrollToLast()
    {
        Canvas.ForceUpdateCanvases();
        var content = Ref<RectTransform>("_raidMembers"); var scroll = content.parent.parent.GetComponent<ScrollRect>();
        if (PlayDataManager.Instance.Defense.Members.Count != 26 || !scroll.horizontalScrollbar.gameObject.activeInHierarchy) throw new InvalidOperationException("A full formation needs a visible scrollbar.");
        scroll.horizontalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
        var last = content.GetComponentsInChildren<CountryRosterRow>().Last().transform as RectTransform;
        var center = RectTransformUtility.WorldToScreenPoint(null, last.TransformPoint(last.rect.center));
        if (!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center)) throw new InvalidOperationException("The last member must be reachable.");
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"{Folder}/FullEnd-{width}x{height}.png"));
        Debug.Log("All 26 defense members can be reached by scrolling.");
    }

    [MenuItem("WarSim/Country/Raid Dispatch Preview/Capture")]
    public static void Capture()
    {
        Canvas.ForceUpdateCanvases();
        if (!EditorApplication.isPlaying || !Panel.IsOpen || PlayDataManager.Instance.Defense.Pending == null) throw new InvalidOperationException("An active raid is required.");
        var overflow = Panel.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing && t.overflowMode != TextOverflowModes.Ellipsis).Select(t => t.name + ": " + t.text).ToArray();
        if (overflow.Length != 0) throw new InvalidOperationException("Text overflow: " + string.Join(", ", overflow));
        foreach (string name in new[] { "_edit", "_fight" })
        {
            var button = Ref<Button>(name); var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject) throw new InvalidOperationException("Button cannot receive input: " + name);
        }
        if (Ref<TextMeshProUGUI>("_raidSummary").text.Contains("CP")) throw new InvalidOperationException("Temporary battle cost must not be displayed.");
        var content = Ref<RectTransform>("_raidMembers");
        int visible = content.GetComponentsInChildren<CountryRosterRow>().Length;
        int registered = PlayDataManager.Instance.Defense.Members.Count;
        int empty = content.GetComponentsInChildren<Image>().Count(image => image.sprite != null && image.sprite.name == "Roster-EmptySlot");
        if (Ref<GameObject>("_raidEmptyMessage").activeSelf != (registered == 0)) throw new InvalidOperationException("Empty formation message must match the registered formation.");
        if (visible != registered || empty != (registered == 0 ? 1 : 0)) throw new InvalidOperationException("Displayed member icons must match the registered formation.");
        Directory.CreateDirectory(Folder); PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string state = registered == 0 ? "Empty" : !Ref<Button>("_fight").interactable ? "Unavailable" : registered > 2 ? "Full" : "Raid";
        string path = Path.GetFullPath($"{Folder}/{state}-{width}x{height}");
        File.WriteAllText(path + ".json", JsonUtility.ToJson(new CaptureReport { Date = CountryDate.From(DateManager.Instance).ToString(), Members = PlayDataManager.Instance.Defense.Members.Count, Overflow = overflow }, true));
        ScreenCapture.CaptureScreenshot(path + ".png");
        Debug.Log("Raid dispatch text fits and both actions receive pointer input.");
    }
    [Serializable] private sealed class CaptureReport { public string Date; public int Members; public string[] Overflow; }
}
