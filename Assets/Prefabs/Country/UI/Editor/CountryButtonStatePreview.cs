using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryButtonStatePreview
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string Records = CountryPhonePreview.Folder + "/ButtonStates";

    [MenuItem("WarSim/Country/Button State Preview/Update Report Title")]
    public static void UpdateReportTitle()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryGovernment.prefab"))
            scope.prefabContentsRoot.transform.Find("Window/Shell/Main/Income/Title").GetComponent<TextMeshProUGUI>().text = "報告書";
    }

    [MenuItem("WarSim/Country/Button State Preview/Capture")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture in Edit mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Preview Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(30, 35, 43, 255); cameraObject.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var paper = new GameObject("Paper", typeof(RectTransform), typeof(Image)); paper.transform.SetParent(canvasObject.transform, false);
            var paperRect = (RectTransform)paper.transform; paperRect.anchorMin = paperRect.anchorMax = paperRect.pivot = Vector2.zero;
            paperRect.anchoredPosition = new Vector2(160, 285); paperRect.sizeDelta = new Vector2(1500, 240);
            paper.GetComponent<Image>().color = new Color32(226, 211, 184, 255);
            var roster = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryRosterView.prefab").GetComponent<CountryRosterView>();
            var rosterButton = (Button)new SerializedObject(roster).FindProperty("_action").objectReferenceValue;
            var defense = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryDefense.prefab").GetComponent<CountryDefensePanel>();
            var fight = (Button)new SerializedObject(defense).FindProperty("_fight").objectReferenceValue;
            foreach (var item in new[] { (rosterButton, 610f), (fight, 350f) })
            {
                AddButton(item.Item1, canvasObject.transform, 260, item.Item2, true);
                AddButton(item.Item1, canvasObject.transform, 1050, item.Item2, false);
            }
            Directory.CreateDirectory(Records);
            foreach (var size in new[] { CountryPhonePreview.Size })
            {
                var texture = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = texture; camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases(); camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = texture;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                File.WriteAllBytes($"{Records}/Buttons-{size.x}x{size.y}.png", image.EncodeToPNG());
                Object.DestroyImmediate(image); RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(texture);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void AddButton(Button source, Transform parent, float x, float y, bool enabled)
    {
        var button = Object.Instantiate(source, parent); button.gameObject.SetActive(true);
        var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(520, 112);
        var feedback = button.GetComponent<CountryButtonFeedback>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CountryButtonFeedback).GetMethod("Awake", flags).Invoke(feedback, null);
        button.interactable = enabled;
        typeof(CountryButtonFeedback).GetMethod("OnEnable", flags).Invoke(feedback, null);
        var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.text = enabled ? "使用できる" : "使用できない";
    }
}
