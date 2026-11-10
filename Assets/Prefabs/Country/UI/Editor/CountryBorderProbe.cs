using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CountryBorderProbe
{
    [MenuItem("WarSim/Country/Border Probe/Capture")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode is required.");
        var panel = GameObject.Find("CountryHud/Settings/Window/Panel");
        if (panel == null) throw new InvalidOperationException("Open settings first.");
        Canvas.ForceUpdateCanvases();
        var frame = panel.transform.Find("Raids/Check/Frame").GetComponent<CountryGovernmentPlate>();
        var vh = new VertexHelper();
        typeof(CountryGovernmentPlate).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(frame, new object[] { vh });
        var positions = new Vector2[vh.currentVertCount];
        for (int i = 0; i < positions.Length; i++)
        {
            var vertex = new UIVertex(); vh.PopulateUIVertex(ref vertex, i);
            positions[i] = RectTransformUtility.WorldToScreenPoint(null, frame.transform.TransformPoint(vertex.position));
        }
        vh.Dispose();
        var rendered = frame.canvasRenderer.GetMesh().vertices.Select(v => RectTransformUtility.WorldToScreenPoint(null, frame.transform.TransformPoint(v))).ToArray();
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        var report = new Probe { Width = width, Height = height, CanvasScale = frame.canvas.scaleFactor, PixelPerfect = frame.canvas.pixelPerfect, Vertices = positions, Rendered = rendered };
        Directory.CreateDirectory("Artifacts/CountryUI/ButtonBorders-v1");
        string path = Path.GetFullPath($"Artifacts/CountryUI/ButtonBorders-v1/Fixed-{width}x{height}");
        File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(path + ".png");
    }
    [Serializable] private sealed class Probe { public uint Width, Height; public float CanvasScale; public bool PixelPerfect; public Vector2[] Vertices, Rendered; }
}
