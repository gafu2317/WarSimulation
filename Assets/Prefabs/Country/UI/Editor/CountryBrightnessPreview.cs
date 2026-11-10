using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryBrightnessPreview
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string Records = CountryPhonePreview.Folder + "/Brightness";

    [MenuItem("WarSim/Country/Brightness Preview/Capture Before")]
    public static void Before() => Capture("Before");
    [MenuItem("WarSim/Country/Brightness Preview/Capture After")]
    public static void After() => Capture("After");

    private static void Capture(string stage)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture in Edit mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Preview Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(42, 49, 59, 255); cameraObject.transform.position = new Vector3(0, 0, -10);
            var host = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(host, scene);
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            string[] pages = stage == "Before" ? new[] { "Training", "Settings", "Report" } : new[] { "Training", "Settings", "Report", "Time", "Defense", "Raid" };
            foreach (string name in pages)
            {
                string prefab = name switch {
                    "Training" => "CountryTraining", "Settings" => "CountrySettings", "Report" => "CountryGovernment",
                    "Time" => "CountryTimeProgressPopup", _ => "CountryDefense"
                };
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"), scene);
                root.transform.SetParent(host.transform, false);
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                if (name == "Training") PrepareTraining(root, rect);
                else if (name == "Settings") root.transform.Find("Window").gameObject.SetActive(true);
                else if (name == "Report")
                {
                    root.transform.Find("Window").gameObject.SetActive(true);
                    var main = root.transform.Find("Window/Shell/Main");
                    foreach (Transform child in main) child.gameObject.SetActive(child.name == "Income");
                }
                else if (name == "Time") root.transform.Find("Modal").gameObject.SetActive(true);
                else
                {
                    root.transform.Find("Window").gameObject.SetActive(true);
                    root.transform.Find("Window/Shell/Formation").gameObject.SetActive(name == "Defense");
                    root.transform.Find("Window/Shell/Alert").gameObject.SetActive(name == "Raid");
                }
                foreach (var size in new[] { CountryPhonePreview.Size })
                {
                    var texture = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = texture; camera.aspect = (float)size.x / size.y;
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = texture;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    Directory.CreateDirectory(Records);
                    File.WriteAllBytes($"{Records}/{stage}-{name}-{size.x}x{size.y}.png", image.EncodeToPNG());
                    Object.DestroyImmediate(image); RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(texture);
                }
                Object.DestroyImmediate(root);
            }
            Debug.Log("Country brightness previews captured without changing the open scene.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void PrepareTraining(GameObject root, RectTransform rect)
    {
        rect.offsetMin = new Vector2(40, 128); rect.offsetMax = new Vector2(-40, -112);
        var training = root.GetComponent<CountryTrainingView>(); var properties = new SerializedObject(training);
        var view = (CountryRosterView)properties.FindProperty("_view").objectReferenceValue;
        var confirm = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CountryRosterConfirm.prefab"), root.transform).GetComponent<CountryRosterConfirm>();
        properties.FindProperty("_confirmation").objectReferenceValue = confirm; properties.ApplyModifiedPropertiesWithoutUndo();
        var roster = new CountryRoster(view.Data, new CountryDate(0, 1, 1));
        if (!roster.TryStartTraining(1, new[] { 2 }, out string reason)) throw new InvalidOperationException(reason);
        if (!roster.TrySetCP(3, 0)) throw new InvalidOperationException("Could not prepare an unavailable soldier.");
        training.Initialize(roster);
        var action = (Button)new SerializedObject(view).FindProperty("_action").objectReferenceValue;
        view.SelectSoldier(0); action.onClick.Invoke(); view.SelectSoldier(1);
    }
}
