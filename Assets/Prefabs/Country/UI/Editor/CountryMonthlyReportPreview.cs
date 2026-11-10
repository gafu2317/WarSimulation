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

public static class CountryMonthlyReportPreview
{
    private const string Folder = "Artifacts/CountryUI/MonthlyReport-v3";
    private static CountryGovernmentPanel Government => Object.FindAnyObjectByType<CountryGovernmentPanel>();
    private static Transform Page => Government.transform.Find("Window/Shell/Main/Income");

    [MenuItem("WarSim/Country/Monthly Report Preview/Prepare")]
    public static void Prepare()
    {
        RequirePlay();
        var data = PlayDataManager.Instance; var date = DateManager.Instance; var state = data.Expeditions;
        data.SetRaidsEnabled(false);
        int target = Enumerable.Range(0, state.Data.Regions.Length).Where(state.CanTarget).OrderBy(state.Data.TravelDays).First();
        if (!state.TryDepart(target, new[] { 0 }, new[] { 0 }, out string error)) throw new InvalidOperationException(error);
        Government.Close(); date.AdvanceToMonthEnd(); date.AdvanceTime(100);
        Check(Government.IsOpen && state.Returned.Count == 1 && data.LastIncomeReceipt.Tax == 850, "Monthly receipt and return must open the report.");
        data.SetResources(8500, 5000, 3400, 2200);
        CheckTotals("5,000", "3,400", "2,200");
        Check(data.LastIncomeReceipt.Tax == 850, "Changing stocks must preserve the received tax.");
        Debug.Log("Monthly report totals update immediately while the received amount remains unchanged.");
    }

    [MenuItem("WarSim/Country/Monthly Report Preview/Capture")]
    public static void Capture()
    {
        RequirePlay(); Canvas.ForceUpdateCanvases(); CheckTotals("5,000", "3,400", "2,200");
        var resources = Page.Find("Resources");
        var icons = new List<string>();
        foreach (string name in new[] { "Tax", "Wood", "Ore" })
        {
            var icon = resources.Find(name + "/Icon").GetComponent<Image>();
            Check(icon.gameObject.activeInHierarchy && icon.sprite != null, "The " + name + " icon must be visible.");
            icons.Add(icon.sprite.name);
        }
        var overflow = Page.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing && text.overflowMode != TextOverflowModes.Ellipsis)
            .Select(text => text.name + ": " + text.text).ToArray();
        Check(overflow.Length == 0, "Visible text must fit: " + string.Join(", ", overflow));
        Directory.CreateDirectory(Folder); PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string path = $"{Folder}/Report-{width}x{height}";
        var report = new CaptureReport { Date = CountryDate.From(DateManager.Instance).ToString(), Icons = icons.ToArray(),
            Tax = Text("Tax/Stock"), Wood = Text("Wood/Stock"), Ore = Text("Ore/Stock"), Overflow = overflow };
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true)); ScreenCapture.CaptureScreenshot(Path.GetFullPath(path + ".png"));
    }

    [MenuItem("WarSim/Country/Monthly Report Preview/Verify Large Totals")]
    public static void VerifyLarge()
    {
        RequirePlay(); var data = PlayDataManager.Instance;
        data.SetResources(8500, int.MaxValue, int.MaxValue, int.MaxValue); Canvas.ForceUpdateCanvases();
        CheckTotals("99999+", "99999+", "99999+");
        Check(data.Treasury == int.MaxValue && data.Wood == int.MaxValue && data.Ore == int.MaxValue, "Display limits must preserve the stored totals.");
        var overflow = Page.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing && text.overflowMode != TextOverflowModes.Ellipsis).ToArray();
        Check(overflow.Length == 0, "The largest supported totals must fit.");
        data.SetResources(8500, 5000, 3400, 2200);
        Debug.Log("All maximum supported totals fit in the monthly report.");
    }

    [MenuItem("WarSim/Country/Monthly Report Preview/Verify Return Details")]
    public static void VerifyReturnDetails()
    {
        RequirePlay(); var row = Page.GetComponentInChildren<CountryExpeditionRow>(); var button = row.GetComponent<Button>();
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "Return row must receive pointer input.");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        var report = Page.GetComponent<CountryExpeditionReportView>(); Check(report.DetailsOpen, "Return details must open.");
        report.CloseDetails(); Government.OpenBudget(); Government.OpenIncome();
        CheckTotals("5,000", "3,400", "2,200");
        Check(PlayDataManager.Instance.LastIncomeReceipt.Tax == 850, "Reopening must not repeat the income.");
        Debug.Log("Return details and report navigation preserve totals and income.");
    }

    private static string Text(string path) => Page.Find("Resources/" + path).GetComponent<TextMeshProUGUI>().text;
    private static void CheckTotals(string tax, string wood, string ore)
    {
        Check(Text("Tax/Stock") == tax && Text("Wood/Stock") == wood && Text("Ore/Stock") == ore, "Displayed totals must reflect current resources.");
        Check(Page.Find("IncomeColumns/Note").GetComponent<TextMeshProUGUI>().text == "現在の合計量", "Report column must show current totals.");
    }
    private static void RequirePlay() => Check(EditorApplication.isPlaying && Government != null && PlayDataManager.Instance != null, "Open the Country scene in Play mode first.");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class CaptureReport { public string Date, Tax, Wood, Ore; public string[] Icons, Overflow; }
}
