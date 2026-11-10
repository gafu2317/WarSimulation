using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class CountrySoldierPreview
{
    private const string Folder = "Artifacts/CountryUI/EffectiveMonth-v1";

    [MenuItem("WarSim/Country/Soldier Preview/Verify Rules Without Scene Changes")]
    public static void VerifyRules()
    {
        // TestRunner opens scenes; these focused tests only create and destroy ScriptableObjects.
        var suite = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("CountryRosterTests")).FirstOrDefault(type => type != null);
        if (suite == null) throw new InvalidOperationException("CountryRosterTests assembly is not loaded.");
        var results = new List<RuleResult>();
        foreach (var method in suite.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attributes = method.GetCustomAttributes(false);
            var cases = attributes.Where(attribute => attribute.GetType().Name == "TestCaseAttribute").ToArray();
            if (cases.Length == 0 && !attributes.Any(attribute => attribute.GetType().Name == "TestAttribute")) continue;
            var arguments = cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(attribute => (object[])attribute.GetType().GetProperty("Arguments").GetValue(attribute)).ToArray();
            foreach (var args in arguments)
            {
                var result = new RuleResult { Name = method.Name + "(" + string.Join(",", args) + ")" };
                var instance = Activator.CreateInstance(suite);
                try { suite.GetMethod("SetUp").Invoke(instance, null); method.Invoke(instance, args); result.Passed = true; }
                catch (Exception exception) { result.Error = (exception.InnerException ?? exception).ToString(); }
                finally { suite.GetMethod("TearDown").Invoke(instance, null); }
                results.Add(result);
            }
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/rules.json", JsonUtility.ToJson(new RuleReport { Tests = results.ToArray() }, true));
        int failures = results.Count(result => !result.Passed);
        Debug.Log($"Country soldier rules: {results.Count - failures}/{results.Count} passed. No scene was opened or saved.");
        if (failures > 0) throw new InvalidOperationException($"{failures} soldier rule tests failed. See {Folder}/rules.json.");
    }

    [MenuItem("WarSim/Country/Soldier Preview/Capture Current UI")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        Canvas.ForceUpdateCanvases();
        var personnel = Object.FindAnyObjectByType<CountryPersonnelPanel>();
        var government = Object.FindAnyObjectByType<CountryGovernmentPanel>();
        var root = personnel != null && personnel.IsOpen ? personnel.gameObject : government.gameObject;
        bool personnelOpen = personnel != null && personnel.IsOpen;
        string page = personnelOpen ? root.transform.Find("Window/Shell/Header/Context").GetComponent<TMP_Text>().text
            : root.transform.Find("Window/Shell/Main").Cast<Transform>().First(child => child.gameObject.activeSelf).name;
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        string name = !personnelOpen ? page : page.Contains("装備") ? "Equipment" : page.Contains("武器一覧") ? "Weapons" : "Soldiers";
        if (root.GetComponentsInChildren<CountryRosterConfirm>().Any(confirm => confirm.IsOpen)) name += "-Confirm";
        Directory.CreateDirectory(Folder);
        string path = Path.GetFullPath($"{Folder}/{name}-{width}x{height}");
        var report = new UIReport {
            Page = page, Width = (int)width, Height = (int)height, EditorPixelsPerPoint = EditorGUIUtility.pixelsPerPoint,
            Month = DateManager.Instance.CurrentMonth, Day = DateManager.Instance.CurrentDay,
            Soldiers = PlayDataManager.Instance.Roster.Soldiers.Count,
            Training = PlayDataManager.Instance.Roster.Soldiers.Count(soldier => soldier.Training != null),
            EventSystems = Object.FindObjectsByType<EventSystem>().Length,
            Inputs = root.GetComponentsInChildren<UnityEngine.UI.Selectable>()
                .Where(input => input is not UnityEngine.UI.Scrollbar).Select(Input).ToArray(),
            Texts = root.GetComponentsInChildren<TextMeshProUGUI>().Select(text => new TextResult {
                Path = AnimationUtility.CalculateTransformPath(text.transform, root.transform), Text = text.text,
                Overflow = text.isTextOverflowing, Font = text.font.name
            }).ToArray(),
            Scrolls = root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Select(scroll => new ScrollResult {
                Path = AnimationUtility.CalculateTransformPath(scroll.transform, root.transform), Horizontal = scroll.horizontal, Vertical = scroll.vertical,
                Viewport = scroll.viewport.rect.size, Content = scroll.content.rect.size
            }).ToArray()
        };
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(path + ".png");
        Debug.Log("Soldier UI capture: " + path);
    }

    [MenuItem("WarSim/Country/Soldier Preview/Advance One Month")]
    public static void AdvanceMonth()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
        DateManager.Instance.AdvanceToMonthEnd(); DateManager.Instance.AdvanceTime(100);
    }

    [MenuItem("WarSim/Country/Soldier Preview/Advance One Day")]
    public static void AdvanceDay()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
        var date = DateManager.Instance;
        double seconds = new SerializedObject(date).FindProperty("_secondsPerDay").doubleValue;
        date.Resume(); date.AdvanceTime(seconds); date.Pause();
    }

    private static InputResult Input(UnityEngine.UI.Selectable input)
    {
        var canvas = input.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        Rect Bounds(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        var bounds = Bounds((RectTransform)input.transform); var clip = new Rect(0, 0, Screen.width, Screen.height);
        foreach (var scroll in input.GetComponentsInParent<UnityEngine.UI.ScrollRect>())
        {
            if (!input.transform.IsChildOf(scroll.content)) continue;
            var viewport = Bounds(scroll.viewport);
            clip = Rect.MinMaxRect(Mathf.Max(clip.xMin, viewport.xMin), Mathf.Max(clip.yMin, viewport.yMin), Mathf.Min(clip.xMax, viewport.xMax), Mathf.Min(clip.yMax, viewport.yMax));
        }
        return new InputResult { Path = AnimationUtility.CalculateTransformPath(input.transform, null), Width = bounds.width, Height = bounds.height,
            Interactable = input.IsInteractable(), FullyVisible = clip.Contains(bounds.min) && clip.Contains(bounds.max - Vector2.one * .01f) };
    }

    [Serializable] private sealed class RuleReport { public RuleResult[] Tests; }
    [Serializable] private sealed class RuleResult { public string Name, Error; public bool Passed; }
    [Serializable] private sealed class UIReport
    {
        public string Page;
        public int Width, Height, Month, Day, Soldiers, Training, EventSystems;
        public float EditorPixelsPerPoint;
        public TextResult[] Texts;
        public ScrollResult[] Scrolls;
        public InputResult[] Inputs;
    }
    [Serializable] private sealed class TextResult { public string Path, Text, Font; public bool Overflow; }
    [Serializable] private sealed class ScrollResult { public string Path; public bool Horizontal, Vertical; public Vector2 Viewport, Content; }
    [Serializable] private sealed class InputResult { public string Path; public float Width, Height; public bool Interactable, FullyVisible; }
}
