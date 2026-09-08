using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public static class RiverFlowPreview
    {
        [MenuItem("WarSim/Map/川の流れを表示に反映")]
        public static void ApplyCurrent()
        {
            Apply(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private static void Apply(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MapSceneHost host in root.GetComponentsInChildren<MapSceneHost>(true))
            {
                RiverRenderer renderer = host.GetComponent<RiverRenderer>();
                if (renderer == null) continue;
                MapData map = host.LastAppliedMap;
                if (map == null)
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:BakedMapData"))
                    {
                        BakedMapData baked = AssetDatabase.LoadAssetAtPath<BakedMapData>(
                            AssetDatabase.GUIDToAssetPath(guid));
                        if (baked.BakeFingerprint != host.BakedRenderFingerprint) continue;
                        map = baked.CreateRuntimeMap();
                        break;
                    }
                }
                if (map == null) throw new InvalidOperationException("川の元MapDataが見つかりません: " + host.name);
                renderer.Render(map);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        [MenuItem("WarSim/Map/保存済みの川の流れを更新")]
        public static void ApplySavedScenes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("先に編集中のSceneを保存してください。");
            Material material = Resources.Load<Material>("Combat/Map/StylizedRiver");
            if (material == null || ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("River shader failed to compile.");
            ApplySavedScene("Assets/Scenes/GafuTest.unity");
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/BakedMaps" }))
                ApplySavedScene(AssetDatabase.GUIDToAssetPath(guid));
            if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup);
            Debug.Log("RIVER_FLOW_APPLIED");
        }

        private static void ApplySavedScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Apply(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("川のSceneを保存できません: " + path);
        }

        [MenuItem("WarSim/Map/川の流れを撮影")]
        public static void Capture()
        {
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene("Assets/Scenes/GafuTest.unity", OpenSceneMode.Single);
            Material material = Resources.Load<Material>("Combat/Map/StylizedRiver");
            if (material == null || ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("River shader failed to compile.");
            RiverRenderer river = UnityEngine.Object.FindFirstObjectByType<RiverRenderer>();
            MeshRenderer surface = river.transform.Find("GeneratedRivers").GetComponentInChildren<MeshRenderer>();
            Bounds bounds = surface.bounds;
            var cameraObject = new GameObject("RiverPreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 25f;
            camera.transform.position = bounds.center + Vector3.up * (bounds.size.magnitude + 100f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.farClipPlane = bounds.size.magnitude + 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.enabled = false;
            var target = new RenderTexture(1024, 1024, 24);
            camera.targetTexture = target;
            int frame = 0;
            double next = EditorApplication.timeSinceStartup;
            Color32[] previous = null;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                camera.Render();
                RenderTexture previousTarget = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                RenderTexture.active = previousTarget;
                File.WriteAllBytes("/private/tmp/river-flow-" + frame + ".png", texture.EncodeToPNG());
                Color32[] pixels = texture.GetPixels32();
                if (frame == 1)
                {
                    int changed = 0;
                    for (int i = 0; i < pixels.Length; i++)
                        if (!pixels[i].Equals(previous[i])) changed++;
                    Debug.Log("RIVER_FLOW_CAPTURE_CHANGED_PIXELS=" + changed);
                    EditorApplication.update -= capture;
                    camera.targetTexture = null;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(texture);
                    if (Application.isBatchMode) EditorApplication.Exit(changed > 0 ? 0 : 1);
                    return;
                }
                previous = pixels;
                UnityEngine.Object.DestroyImmediate(texture);
                frame++;
                next = EditorApplication.timeSinceStartup
                    + surface.sharedMaterial.GetFloat("_PatternScale") / Mathf.Abs(surface.sharedMaterial.GetFloat("_FlowSpeed"));
            };
            EditorApplication.update += capture;
        }
    }
}
