using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountrySettingsBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string Records = "Artifacts/CountryUI/ResourceSettings-v1";
    private static readonly string[] FieldNames = { "Population", "Treasury", "Wood", "Ore" };

    [MenuItem("WarSim/Country/Set Up Resource Settings")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating settings prefabs.");
        SavePrefab("CountrySettings", BuildWindow);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        {
            var hud = scope.prefabContentsRoot.transform;
            var root = hud.Find("Settings");
            if (root == null)
            {
                root = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountrySettings.prefab"), hud)).transform;
                root.name = "Settings"; Stretch((RectTransform)root);
            }
            root.SetAsLastSibling();
            var controller = root.GetComponent<CountrySettingsPanel>();
            var button = hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(item => item.name == "SettingsButton");
            Set(controller, ("_open", button), ("_government", hud.Find("Government").GetComponent<CountryGovernmentPanel>()),
                ("_personnel", hud.Find("Personnel").GetComponent<CountryPersonnelPanel>()));
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        const string glyphs = "設定資源の変更人口国庫木材鉱石税収は人口に応じて変わる適用する閉じるキャンセル0〜2,147,483,647の整数を入力×襲撃を発生させるOFFで時間経過による停止ON戦闘へ移動国家戻";
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing settings glyphs: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        Debug.Log("Resource settings connected to the gear button. The open scene was not saved or reloaded.");
    }

    private static void BuildWindow(GameObject root)
    {
        Stretch(root.GetComponent<RectTransform>());
        var controller = Add<CountrySettingsPanel>(root);
        var window = Fill(root.transform, "Window");
        var canvas = Add<Canvas>(window.gameObject); canvas.overrideSorting = true; canvas.sortingOrder = 60;
        Add<UnityEngine.UI.GraphicRaycaster>(window.gameObject);
        var dim = Image(Fill(window, "Dim"), new Color(.03f, .04f, .06f, .72f)); dim.raycastTarget = true;
        var dismiss = Add<UnityEngine.UI.Button>(dim.gameObject); dismiss.targetGraphic = dim; dismiss.transition = UnityEngine.UI.Selectable.Transition.None;
        dismiss.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var panel = Child(window, "Panel"); panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(1160, 900);
        Image(panel, Theme.Background).raycastTarget = true;
        var board = panel.Find("Workboard");
        if (board == null)
        {
            board = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryWorkboard.prefab"), panel)).transform;
            board.name = "Workboard";
        }
        Stretch((RectTransform)board); board.SetAsFirstSibling();
        Icon(Top(panel, "SettingsIcon", 40, 36, 56, 56), "Controls/settings-v1");
        Label(Top(panel, "Title", 112, 28, 840, 76), "設定", 48);
        Label(Top(panel, "Subtitle", 40, 112, 960, 48), "資源の変更", 32, Theme.Accent);
        var close = Button(Top(panel, "Close", 1032, 16, 112, 112), "×", Theme.Panel);
        var fieldsRoot = Fill(panel, "Fields");
        var fields = new TMP_InputField[4]; var frames = new UnityEngine.UI.Graphic[4];
        string[] names = { "人口", "国庫", "木材", "鉱石" }, icons = { "population-v1", "budget-v1", "wood-v1", "ore-v1" };
        for (int i = 0; i < fields.Length; i++)
        {
            var group = Top(fieldsRoot, FieldNames[i], 40 + i % 2 * 560, 168 + i / 2 * 200, 520, 176);
            Icon(Top(group, "Icon", 0, 0, 56, 56), icons[i]);
            Label(Top(group, "Caption", 76, 0, 440, 56), names[i], 32);
            var rect = Top(group, "Input", 0, 64, 520, 112);
            var frame = Plate(rect, Theme.Border, 0); frame.raycastTarget = true; frames[i] = frame;
            var surface = Plate(Fill(rect, "Surface", Vector2.one * 2.5f, Vector2.one * -2.5f), Color.white, 0);
            var viewport = Fill(rect, "Viewport", new Vector2(18, 10), new Vector2(-18, -10));
            Add<UnityEngine.UI.RectMask2D>(viewport.gameObject);
            var text = Label(Fill(viewport, "Value"), "0", 44); text.richText = false;
            var input = Add<TMP_InputField>(rect.gameObject); input.textViewport = viewport; input.textComponent = text;
            input.fontAsset = text.font; input.pointSize = 44; input.targetGraphic = surface;
            input.contentType = TMP_InputField.ContentType.IntegerNumber; input.lineType = TMP_InputField.LineType.SingleLine;
            input.keyboardType = TouchScreenKeyboardType.NumberPad; input.characterLimit = 10; input.text = "0";
            input.customCaretColor = true; input.caretColor = Theme.Accent; input.caretWidth = 2;
            input.selectionColor = new Color(.88f, .78f, .58f, .3f); Theme.Colors(input, Theme.Recess);
            fields[i] = input;
        }
        Label(Top(panel, "TaxHint", 40, 568, 1080, 48), "税収は人口に応じて変わる", 28, Theme.Muted);
        var raidRow = Top(panel, "Raids", 40, 624, 1080, 88);
        Image(raidRow, Theme.Panel).raycastTarget = true;
        Label(Top(raidRow, "Caption", 24, 0, 760, 48), "襲撃を発生させる", 32);
        Label(Top(raidRow, "Hint", 24, 48, 880, 36), "OFFで時間経過による襲撃を停止", 24, Theme.Muted);
        var check = Right(raidRow, "Check", 16, 8, 128, 72);
        var face = Image(check, Theme.Recess);
        Plate(Fill(check, "Frame"), Theme.Border, 0, 2.5f);
        var on = Label(Fill(check, "On"), "ON", 32, Theme.Accent); on.alignment = TextAlignmentOptions.Center;
        var raids = Add<UnityEngine.UI.Toggle>(raidRow.gameObject); raids.targetGraphic = face; raids.isOn = true;
        Theme.Colors(raids, Theme.Recess);
        var error = Label(Top(panel, "Error", 40, 728, 1080, 36), "0〜2,147,483,647の整数を入力", 26, Theme.Error);
        error.gameObject.SetActive(false);
        var footer = BottomFill(panel, "Footer", 128); footer.offsetMin = new Vector2(40, 0); footer.offsetMax = new Vector2(-40, 128);
        var cancel = Button(Top(footer, "Cancel", 0, 0, 264, 112), "キャンセル", Theme.Panel);
        var combat = Button(Top(footer, "Combat", 288, 0, 384, 112), "戦闘へ移動", Theme.Panel);
        var apply = Button(Right(footer, "Apply", 0, 0, 320, 112), "適用する", Theme.Selected, true);
        Set(controller, ("_combat", combat), ("_window", window.gameObject), ("_raids", raids), ("_raidsLabel", on), ("_error", error), ("_apply", apply), ("_cancelButton", cancel), ("_close", close), ("_dismiss", dismiss),
            ("_applyLabel", apply.transform.Find("Label").GetComponent<TextMeshProUGUI>()), ("_applyFrame", apply.transform.Find("Frame").GetComponent<CountryGovernmentPlate>()));
        SetArray(controller, "_fields", fields); SetArray(controller, "_frames", frames);
        window.gameObject.SetActive(false);
    }

    private static void Icon(RectTransform rect, string name)
    {
        var image = Image(rect, Color.white); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/" + name + ".png"); image.preserveAspect = true;
    }

    [MenuItem("WarSim/Country/Resource Settings/Enter Test Values")]
    public static void EnterValues() => FillInputs(new[] { "12000", "5000", "3400", "2200" });
    [MenuItem("WarSim/Country/Resource Settings/Enter Zero Values")]
    public static void EnterZeros() => FillInputs(new[] { "0", "0", "0", "0" });
    [MenuItem("WarSim/Country/Resource Settings/Enter Initial Values")]
    public static void EnterInitial() => FillInputs(new[] { "8500", "0", "1200", "800" });
    private static void FillInputs(string[] values)
    {
        RequirePlay(); var root = GameObject.Find("CountryHud/Settings").transform.Find("Window/Panel/Fields");
        for (int i = 0; i < FieldNames.Length; i++) root.Find(FieldNames[i] + "/Input").GetComponent<TMP_InputField>().text = values[i];
    }

    [MenuItem("WarSim/Country/Resource Settings/Verify Invalid Inputs")]
    public static void VerifyInvalid()
    {
        RequirePlay(); var root = GameObject.Find("CountryHud/Settings").transform.Find("Window/Panel");
        var input = root.Find("Fields/Population/Input").GetComponent<TMP_InputField>();
        var apply = root.Find("Footer/Apply").GetComponent<UnityEngine.UI.Button>();
        foreach (string value in new[] { "", "-1", "2147483648" })
        {
            input.text = value;
            if (apply.interactable || !root.Find("Error").gameObject.activeSelf) throw new InvalidOperationException("Invalid resource input accepted: " + value);
        }
        Debug.Log("Blank, negative and overflowing resource values rejected by settings.");
    }

    [MenuItem("WarSim/Country/Resource Settings/Verify Resource Rules")]
    public static void VerifyRules()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before verifying resource rules.");
        var suite = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("CountryIncomeTests")).First(type => type != null);
        var results = new System.Collections.Generic.List<RuleReport>();
        foreach (var method in suite.GetMethods().Where(method => method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "TestAttribute")))
        {
            var test = Activator.CreateInstance(suite); var result = new RuleReport { Name = method.Name };
            try { suite.GetMethod("SetUp").Invoke(test, null); method.Invoke(test, null); result.Passed = true; }
            catch (Exception exception) { result.Error = (exception.InnerException ?? exception).ToString(); }
            finally { suite.GetMethod("TearDown").Invoke(test, null); }
            results.Add(result);
        }
        Directory.CreateDirectory(Records); File.WriteAllText(Records + "/resource-rules.json", JsonUtility.ToJson(new RuleResults { Tests = results.ToArray() }, true));
        if (results.Any(result => !result.Passed)) throw new InvalidOperationException("Resource rules failed. See " + Records);
        Debug.Log($"Resource rules: {results.Count}/{results.Count} passed. No scene was opened or saved.");
    }

    [MenuItem("WarSim/Country/Resource Settings/Capture")]
    public static void Capture()
    {
        RequirePlay(); Canvas.ForceUpdateCanvases(); var hud = GameObject.Find("CountryHud"); var resources = PlayDataManager.Instance;
        var report = new CaptureReport {
            Open = hud.GetComponentInChildren<CountrySettingsPanel>(true).IsOpen,
            Resources = new[] { resources.Population, resources.Treasury, resources.Wood, resources.Ore },
            TaxIncome = resources.MonthlyTaxIncome.ToString(CultureInfo.InvariantCulture), Running = DateManager.Instance.IsRunning,
            CameraInput = hud.GetComponentInChildren<CountryGovernmentPanel>(true).CameraControls.InputEnabled,
            Fields = hud.GetComponentsInChildren<TMP_InputField>(true).Select(input => input.text).ToArray(),
            TextOverflows = hud.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing).Select(text => text.text).ToArray()
        };
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height); Directory.CreateDirectory(Records);
        string path = Records + $"/Settings-{width}x{height}"; File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(path + ".png"));
    }

    private static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use the existing Editor in Play mode first."); }
    [Serializable] private sealed class RuleReport { public string Name, Error; public bool Passed; }
    [Serializable] private sealed class RuleResults { public RuleReport[] Tests; }
    [Serializable] private sealed class CaptureReport { public bool Open, Running, CameraInput; public int[] Resources; public string TaxIncome; public string[] Fields, TextOverflows; }
}
