using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryTimeControlsPreview
{
    private const string Folder = CountryPhonePreview.Folder + "/TimeModes";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("WarSim/Country/Time Controls Preview/Capture Smartphone Modes")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit mode for the isolated time-control preview.");
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        var size = CountryPhonePreview.Size;
        var target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            var cameraRoot = new GameObject("Time controls preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.scene = scene; camera.targetTexture = target; camera.aspect = (float)size.x / size.y;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = CountryGovernmentTheme.Background;
            var state = new GameObject("Time controls preview state");
            SceneManager.MoveGameObjectToScene(state, scene);
            var date = state.AddComponent<DateManager>();
            var resources = state.AddComponent<PlayDataManager>();
            var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryHud.prefab"), scene);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var resourceHud = hud.GetComponent<CountryResourceHud>();
            typeof(CountryResourceHud).GetField("_resources", Private).SetValue(resourceHud, resources);
            typeof(CountryResourceHud).GetMethod("Subscribe", Private).Invoke(resourceHud, null);
            var controls = hud.GetComponent<CountryTimeControls>();
            typeof(CountryTimeControls).GetMethod("Awake", Private).Invoke(controls, null);
            typeof(CountryTimeControls).GetField("_date", Private).SetValue(controls, date);
            typeof(CountryTimeControls).GetMethod("Subscribe", Private).Invoke(controls, null);
            var button = (Button)typeof(CountryTimeControls).GetField("_modeButton", Private).GetValue(controls);
            var feedback = button.GetComponent<CountryButtonFeedback>();
            typeof(CountryButtonFeedback).GetMethod("Awake", Private).Invoke(feedback, null);
            typeof(CountryButtonFeedback).GetMethod("OnEnable", Private).Invoke(feedback, null);
            Directory.CreateDirectory(Folder);
            foreach (string mode in new[] { "Stopped", "Running", "DoubleSpeed" })
            {
                Canvas.ForceUpdateCanvases();
                foreach (var label in hud.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(Folder, $"{mode}-{size.x}x{size.y}.png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
                button.onClick.Invoke();
            }
            var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
            var bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            File.WriteAllText(Path.Combine(Folder, "layout.json"), JsonUtility.ToJson(new LayoutReport {
                Width = size.x, Height = size.y, ButtonWidth = top.x - bottom.x, ButtonHeight = top.y - bottom.y,
                Scene = active.path, SceneDirtyBefore = active.isDirty,
                Overflow = hud.transform.Find("TopBar").GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.name).ToArray()
            }, true));
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Debug.Log($"Three time-control modes captured at {size.x}×{size.y} in an isolated preview scene.");
    }

    [Serializable] private sealed class LayoutReport
    {
        public int Width, Height;
        public float ButtonWidth, ButtonHeight;
        public string Scene;
        public bool SceneDirtyBefore;
        public string[] Overflow;
    }
}
