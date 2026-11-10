using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;

public static class CountryTimeEffectBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private static readonly Color Gold = new Color32(224, 200, 148, 255);
    private static readonly Color Paper = new Color32(236, 224, 198, 255);
    private static readonly Color Ink = new Color32(53, 47, 43, 255);

    [MenuItem("WarSim/Country/Set Up Time Progress Effect")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating time progress.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTimeProgressPopup.prefab")) { Apply(scope.prefabContentsRoot); CountryPaletteBuilder.Apply(scope.prefabContentsRoot); }
        Debug.Log("Time progress effect updated. The open scene was not saved or reloaded.");
    }

    internal static void Apply(GameObject root)
    {
        var panel = root.transform.Find("Modal/Panel");
        panel.Find("Face").gameObject.SetActive(false);
        var oldClock = panel.Find("Clock");
        if (oldClock != null) UnityEngine.Object.DestroyImmediate(oldClock.gameObject);
        var board = panel.Find("Workboard") as RectTransform;
        if (board == null)
        {
            board = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryWorkboard.prefab"), panel)).transform;
            board.name = "Workboard";
        }
        Stretch(board); board.SetSiblingIndex(1);
        var title = Top(panel, "Title", 40, 28, 820, 72).GetComponent<TextMeshProUGUI>(); title.color = Gold;
        panel.Find("CurrentDate").gameObject.SetActive(false);
        var target = Top(panel, "TargetDate", 308, 147, 510, 56).GetComponent<TextMeshProUGUI>(); target.color = new Color32(220, 224, 220, 255);
        var bar = Top(panel, "ProgressBar", 308, 222, 510, 18); bar.GetComponent<UnityEngine.UI.Image>().color = new Color32(58, 69, 82, 255);
        bar.Find("Fill").GetComponent<UnityEngine.UI.Image>().color = new Color32(130, 173, 184, 255);

        var calendar = Top(panel, "Calendar", 48, 112, 208, 208);
        Image(Top(calendar, "Stack", 9, 9, 199, 199), new Color32(125, 111, 94, 255));
        Image(Top(calendar, "Back", 4, 4, 199, 199), new Color32(190, 177, 151, 255));
        Image(Top(calendar, "Sheet", 0, 0, 198, 198), Paper);
        Image(Top(calendar, "Header", 0, 0, 198, 55), new Color32(119, 49, 45, 255));
        var month = Label(Top(calendar, "Month", 0, 2, 198, 51), "1月", 32, Paper); month.alignment = TextAlignmentOptions.Center;
        var day = Label(Top(calendar, "Day", 0, 60, 198, 130), "1", 76, Ink); day.alignment = TextAlignmentOptions.Center;
        var turn = Top(calendar, "TurningPage", 0, 55, 198, 143);
        Image(turn, Paper);
        var turnDay = Label(Top(turn, "Day", 0, 5, 198, 130), "1", 76, Ink); turnDay.alignment = TextAlignmentOptions.Center;
        turn.gameObject.SetActive(false);
        Set(Add<CountryTimeProgressEffect>(calendar.gameObject), ("_month", month), ("_day", day), ("_turnDay", turnDay), ("_turnPage", turn));
    }
}
