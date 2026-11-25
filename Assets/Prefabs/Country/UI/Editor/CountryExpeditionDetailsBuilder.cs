using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountryExpeditionDetailsBuilder
{
    private static readonly Color Ink = new Color32(43, 53, 56, 255);
    private static readonly Color Blue = new Color32(42, 68, 79, 255);
    private static readonly Color Accent = new Color32(132, 91, 49, 255);
    private static readonly Color Line = new Color32(180, 165, 138, 255);

    [MenuItem("WarSim/Country/Set Up Expedition Details")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating expedition details.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryExpedition.prefab")) { Apply(scope.prefabContentsRoot); CountryPaletteBuilder.Apply(scope.prefabContentsRoot); }
        Debug.Log("Expedition details updated. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var detail = root.transform.Find("Window/Shell/Map/Details");
        if (detail == null) return;
        if (detail.Find("Workboard") is Transform board) UnityEngine.Object.DestroyImmediate(board.gameObject);
        var paper = Fill(detail, "ItineraryPaper");
        var paperImage = Add<RawImage>(paper.gameObject);
        paperImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/UI/Country/ReportPaper-v1.png");
        paperImage.color = Color.white; paperImage.raycastTarget = false; paper.SetAsFirstSibling();
        Image(TopFill(detail, "ItineraryHeading", 0, 64), Blue).transform.SetSiblingIndex(1);
        Plate(Fill(detail, "ItineraryFrame"), Theme.Border, 0, 2.5f).transform.SetAsLastSibling();
        var caption = Label(TopFill(detail, "Caption", 0, 64, 32, 32), "遠征先", 28, Theme.Text);
        caption.fontStyle = FontStyles.Bold;
        var name = Label(TopFill(detail, "Name", 84, 64, 32, 32), "領地を選ぶ", 42, Ink);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        var owner = Label(TopFill(detail, "Owner", 152, 48, 32, 32), "", 30, Ink);
        var notice = Label(TopFill(detail, "Notice", 152, 78, 32, 32), "明るい境界の領地へ遠征できる", 28, Theme.PaperMuted);
        notice.margin = Vector4.zero; notice.textWrappingMode = TextWrappingModes.Normal; notice.alignment = TextAlignmentOptions.TopLeft;

        var route = TopFill(detail, "Route", 288, 288, 32, 32);
        foreach (string child in new[] { "Divider", "TravelCaption", "Days", "Schedule" })
            if (detail.Find(child) is Transform old) old.SetParent(route, false);
        Image(TopFill(route, "Divider", 0, 2), Line);
        Label(TopFill(route, "TravelCaption", 12, 60, 0, 100), "片道の移動", 28, Theme.PaperMuted);
        var days = Label(TopFill(route, "Days", 12, 60), "", 40, Accent);
        days.alignment = TextAlignmentOptions.MidlineRight;
        var schedule = Label(TopFill(route, "Schedule", 96, 164), "", 32, Ink);
        schedule.alignment = TextAlignmentOptions.TopLeft; schedule.margin = new Vector4(32, 0, 0, 0);
        var line = Image(Top(schedule.transform, "RouteLine", 9, 28, 2, 92), Line); line.raycastTarget = false;
        for (int i = 0; i < 3; i++)
        {
            var marker = Add<CountryCircleGraphic>(Top(schedule.transform, "RouteStop" + i, 2, 20 + i * 46, 16, 16).gameObject);
            marker.color = i == 1 ? Accent : Blue; marker.raycastTarget = false;
            marker.Configure(i == 1 ? 3 : 0);
        }
        route.gameObject.SetActive(false); schedule.gameObject.SetActive(false); owner.gameObject.SetActive(false);
        Set(root.GetComponent<CountryExpeditionPanel>(), ("_route", route.gameObject), ("_targetName", name),
            ("_ownerName", owner), ("_days", days), ("_schedule", schedule), ("_targetNotice", notice));
    }
}
