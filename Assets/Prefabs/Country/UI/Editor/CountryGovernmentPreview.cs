using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class CountryGovernmentPreview
{
    private const string Folder = CountryPhonePreview.Folder + "/Government";

    [MenuItem("WarSim/Country/Government Preview/Phone Simulator", false, 0)]
    public static void Phone() => CountryPhonePreview.Configure();

    [MenuItem("WarSim/Country/Government Preview/Capture Country View")]
    public static void CaptureCountry()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        Directory.CreateDirectory(Folder);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"{Folder}/Country-{width}x{height}.png"));
    }

    [MenuItem("WarSim/Country/Government Preview/Capture Press In 3 Seconds")]
    public static void CapturePress()
    {
        double captureAt = EditorApplication.timeSinceStartup + 3;
        EditorApplication.update += CaptureWhenReady;
        void CaptureWhenReady()
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            EditorApplication.update -= CaptureWhenReady;
            if (!EditorApplication.isPlaying) return;
            PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
            Directory.CreateDirectory(Folder);
            string path = Path.GetFullPath($"{Folder}/Pressed-{width}x{height}");
            var report = new PressReport {
                Month = DateManager.Instance.CurrentMonth, Day = DateManager.Instance.CurrentDay,
                Running = DateManager.Instance.IsRunning,
                Speed = DateManager.Instance.SpeedMultiplier,
                Buttons = Object.FindObjectsByType<UnityEngine.UI.Button>().Where(button => button.targetGraphic != null).Select(button => new ButtonReport {
                    Path = AnimationUtility.CalculateTransformPath(button.transform, null),
                    FaceTop = button.targetGraphic.rectTransform.offsetMax.y,
                    Color = button.targetGraphic.canvasRenderer.GetColor(),
                    ShadowVisible = (button.transform.Find("Shadow") ?? button.targetGraphic.transform.parent.Find("Shadow"))?.gameObject.activeSelf ?? false
                }).ToArray()
            };
            File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
            ScreenCapture.CaptureScreenshot(path + ".png");
        }
    }

    [MenuItem("WarSim/Country/Government Preview/Capture Current Page")]
    public static void Capture()
    {
        var panel = Object.FindAnyObjectByType<CountryGovernmentPanel>();
        if (!EditorApplication.isPlaying || panel == null || !panel.IsOpen) throw new InvalidOperationException("Open government UI in Play mode first.");
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        var main = panel.transform.Find("Window/Shell/Main");
        string page = main.Cast<Transform>().First(t => t.gameObject.activeSelf).name;
        string path = Path.GetFullPath($"{Folder}/{page}-{width}x{height}");
        Directory.CreateDirectory(Folder);
        var report = new Report {
            Width = (int)width, Height = (int)height, Page = page, IsOpen = panel.IsOpen,
            Funding = panel.Draft.FundingTotal, TreasuryForecast = panel.Draft.TreasuryForecast,
            Orders = panel.Draft.OrderCount, Running = DateManager.Instance.IsRunning,
            CameraInput = Object.FindAnyObjectByType<CountryCameraController>().InputEnabled,
            EventSystems = Object.FindObjectsByType<EventSystem>().Length,
            Inputs = panel.GetComponentsInChildren<UnityEngine.UI.Selectable>()
                .Where(control => control is not UnityEngine.UI.Scrollbar).Select(Input).ToArray(),
            Texts = panel.GetComponentsInChildren<TextMeshProUGUI>().Select(text => {
                return new TextReport { Name = text.name, Text = text.text, Font = text.font.name,
                    Size = text.fontSize, Width = text.rectTransform.rect.width, Height = text.rectTransform.rect.height,
                    Overflow = text.isTextOverflowing };
            }).ToArray(),
            Scrolls = panel.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Select(scroll => new ScrollReport {
                Name = scroll.name, ViewportHeight = scroll.viewport.rect.height,
                ContentHeight = scroll.content.rect.height, Vertical = scroll.vertical, Horizontal = scroll.horizontal
            }).ToArray()
        };
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(path + ".png");
        Debug.Log("Country government capture: " + path);
    }

    private static InputReport Input(UnityEngine.UI.Selectable control)
    {
        var canvas = control.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        var bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        var clip = new Rect(0, 0, Screen.width, Screen.height);
        foreach (var scroll in control.GetComponentsInParent<UnityEngine.UI.ScrollRect>())
        {
            if (!control.transform.IsChildOf(scroll.content)) continue;
            scroll.viewport.GetWorldCorners(corners);
            min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            clip = Rect.MinMaxRect(Mathf.Max(clip.xMin, min.x), Mathf.Max(clip.yMin, min.y), Mathf.Min(clip.xMax, max.x), Mathf.Min(clip.yMax, max.y));
        }
        return new InputReport {
            Path = AnimationUtility.CalculateTransformPath(control.transform, null),
            Width = bounds.width, Height = bounds.height, Interactable = control.IsInteractable(),
            FullyVisible = clip.Contains(bounds.min) && clip.Contains(bounds.max - Vector2.one * .01f)
        };
    }

    [Serializable] private sealed class Report
    {
        public int Width, Height, Funding, TreasuryForecast, Orders, EventSystems;
        public string Page;
        public bool IsOpen, Running, CameraInput;
        public TextReport[] Texts;
        public ScrollReport[] Scrolls;
        public InputReport[] Inputs;
    }
    [Serializable] private sealed class TextReport
    {
        public string Name, Text, Font;
        public float Size, Width, Height;
        public bool Overflow;
    }
    [Serializable] private sealed class ScrollReport
    {
        public string Name;
        public float ViewportHeight, ContentHeight;
        public bool Vertical, Horizontal;
    }
    [Serializable] private sealed class InputReport
    {
        public string Path;
        public float Width, Height;
        public bool Interactable, FullyVisible;
    }
    [Serializable] private sealed class PressReport
    {
        public int Month, Day;
        public bool Running;
        public int Speed;
        public ButtonReport[] Buttons;
    }
    [Serializable] private sealed class ButtonReport
    {
        public string Path;
        public float FaceTop;
        public Color Color;
        public bool ShadowVisible;
    }
}
