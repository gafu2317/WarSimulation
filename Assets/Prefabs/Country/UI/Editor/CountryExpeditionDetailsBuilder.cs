using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;

public static class CountryExpeditionDetailsBuilder
{
    private static readonly Color Ink = new Color32(43, 53, 56, 255);
    private static readonly Color Secondary = new Color32(91, 100, 96, 255);
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
        var detail = root.transform.Find("Window/Shell/Main/Viewport/Content/Map/Details");
        if (detail == null) return;
        var paper = Fill(detail, "ItineraryPaper", new Vector2(8, 8), new Vector2(-8, -8));
        var paperImage = Add<RawImage>(paper.gameObject);
        paperImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/UI/Country/ReportPaper-v1.png");
        paperImage.color = Color.white; paperImage.raycastTarget = false; paper.SetSiblingIndex(1);
        var header = TopFill(detail, "ItineraryHeading", 12, 56, 12, 12); Image(header, Blue); header.SetSiblingIndex(2);
        var caption = TopFill(detail, "Caption", 12, 56, 32, 32).GetComponent<TextMeshProUGUI>();
        caption.color = new Color32(242, 237, 223, 255); caption.fontSize = 28; caption.fontStyle = FontStyles.Bold;
        var name = TopFill(detail, "Name", 78, 64, 32, 32).GetComponent<TextMeshProUGUI>();
        name.color = Ink; name.fontSize = 42; name.fontStyle = FontStyles.Bold;
        var owner = TopFill(detail, "Owner", 145, 48, 32, 32).GetComponent<TextMeshProUGUI>();
        owner.color = Accent; owner.fontSize = 30;
        Image(TopFill(detail, "Divider", 204, 2, 32, 32), Line);
        var travel = TopFill(detail, "TravelCaption", 218, 48, 32, 32).GetComponent<TextMeshProUGUI>();
        travel.color = Secondary; travel.fontSize = 28;
        var days = TopFill(detail, "Days", 262, 96, 32, 32).GetComponent<TextMeshProUGUI>();
        days.color = Accent; days.fontSize = 60;
        var schedule = TopFill(detail, "Schedule", 380, 160, 32, 28).GetComponent<TextMeshProUGUI>();
        schedule.color = Ink; schedule.fontSize = 32; schedule.margin = new Vector4(42, 0, 0, 0);
        var line = Image(Top(schedule.transform, "RouteLine", 9, 28, 2, 92), Line); line.raycastTarget = false;
        for (int i = 0; i < 3; i++)
        {
            var marker = Add<CountryCircleGraphic>(Top(schedule.transform, "RouteStop" + i, 2, 20 + i * 46, 16, 16).gameObject);
            marker.color = i == 1 ? Accent : Blue; marker.raycastTarget = false;
            marker.Configure(i == 1 ? 3 : 0);
        }
        var notice = detail.Find("Notice").GetComponent<TextMeshProUGUI>();
        notice.color = Secondary; notice.margin = new Vector4(32, 0, 32, 12);
    }
}
