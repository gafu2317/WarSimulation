using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountrySelectionCapture
{
    [MenuItem("WarSim/Country/Selection Verification/Capture Training Prefab")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture only in Edit mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(26, 34, 44, 255);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryTraining.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene); root.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40, 128); rect.offsetMax = new Vector2(-40, -112);
            var training = root.GetComponent<CountryTrainingView>(); var properties = new SerializedObject(training);
            var view = (CountryRosterView)properties.FindProperty("_view").objectReferenceValue;
            var confirm = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryRosterConfirm.prefab"), root.transform).GetComponent<CountryRosterConfirm>();
            properties.FindProperty("_confirmation").objectReferenceValue = confirm; properties.ApplyModifiedPropertiesWithoutUndo();
            var roster = new CountryRoster(view.Data, new CountryDate(0, 1, 1));
            if (!roster.TryStartTraining(1, new[] { 2 }, out string reason)) throw new InvalidOperationException(reason);
            if (!roster.TrySetCP(3, 0)) throw new InvalidOperationException("Could not prepare an unavailable soldier.");
            training.Initialize(roster);
            var viewProperties = new SerializedObject(view);
            var action = (Button)viewProperties.FindProperty("_action").objectReferenceValue;
            var list = (ScrollRect)viewProperties.FindProperty("_list").objectReferenceValue;
            view.SelectSoldier(0); action.onClick.Invoke(); view.SelectSoldier(1);
            Directory.CreateDirectory(CountryPhonePreview.Folder + "/MemberSelection");
            foreach (var size in new[] { CountryPhonePreview.Size })
            {
                var texture = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = texture; camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                foreach (bool bottom in new[] { false, true })
                {
                    list.verticalNormalizedPosition = bottom ? 0 : 1;
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = texture;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    File.WriteAllBytes($"{CountryPhonePreview.Folder}/MemberSelection/Training{(bottom ? "-Unavailable" : "")}-{size.x}x{size.y}.png", image.EncodeToPNG());
                    Object.DestroyImmediate(image); RenderTexture.active = previous;
                }
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(texture);
            }
            Debug.Log("Training prefab captured in an isolated preview scene.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
