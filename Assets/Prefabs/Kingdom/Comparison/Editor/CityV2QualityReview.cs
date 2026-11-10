using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CityV2QualityReview
    {
        public const string Report = "Artifacts/CityV2Quality20261030";
        static Queue<JObject> pending;
        static readonly List<object> records = new();
        static string phase;
        static int total;

        [MenuItem("WarSim/Country/Quality Review/Capture Before")]
        public static void Before() => Start("Before");

        [MenuItem("WarSim/Country/Quality Review/Capture After")]
        public static void After() => Start("After");

        [MenuItem("WarSim/Country/Quality Review/Capture Current Streets")]
        public static void Streets()
        {
            if (pending != null || EditorApplication.isPlaying) throw new InvalidOperationException("Finish model capture in Edit mode first.");
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            var folder = Report + "/Streets";
            Directory.CreateDirectory(folder);
            var temp = new GameObject("Temporary street review camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = temp.AddComponent<Camera>();
            camera.CopyFrom(view.viewCamera);
            camera.enabled = false;
            camera.aspect = 16f / 9;
            camera.fieldOfView = 40;
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(), temp.AddComponent<UniversalAdditionalCameraData>());
            var visible = view.cities.Select(c => c.activeSelf).ToArray();
            var shots = new[] {
                ("Overview", new Vector3(0,6,-5), new Vector3(0,209,-240)),
                ("Market", new Vector3(0,2,-27), new Vector3(22,28,-39)),
                ("Residential", new Vector3(-63,2,-48), new Vector3(18,25,-35)),
                ("Church", new Vector3(-32,5,43), new Vector3(29,36,-49)),
                ("Palace", new Vector3(0,8,137), new Vector3(36,48,-64)),
                ("Library", new Vector3(37,5,48), new Vector3(15,20,-23)),
                ("Observatory", new Vector3(85,7,88), new Vector3(14,17,-22)),
                ("Workshop", new Vector3(68,3,-23), new Vector3(19,21,-28)),
                ("River", new Vector3(-67,2,14.15f), new Vector3(18,21,-25)),
                ("MarketHall", new Vector3(-33,2,-30), new Vector3(23,21,-33))
            };
            try
            {
                for (var set = 0; set < 2; set++)
                {
                    for (var i = 0; i < view.cities.Length; i++) view.SetCityVisible(i, i == set);
                    foreach (var shot in shots)
                    {
                        var target = view.centers[set] + shot.Item2;
                        camera.transform.position = target + shot.Item3;
                        camera.transform.LookAt(target);
                        CountryComparisonBuilder.CaptureCamera(camera, folder + "/" + (set == 0 ? "Old_" : "New_") + shot.Item1 + ".png");
                    }
                }
            }
            finally
            {
                for (var i = 0; i < view.cities.Length; i++) view.SetCityVisible(i, visible[i]);
                Object.DestroyImmediate(temp);
            }
        }

        static void Start(string name)
        {
            if (pending != null || EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires idle Edit mode.");
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity") throw new InvalidOperationException("Open Country first.");
            phase = name;
            var entries = JObject.Parse(File.ReadAllText(CountryComparisonImporter.ModelRoot + "/export_manifest.json"))["assets"].OfType<JObject>().OrderBy(e => (string)e["name"]).ToArray();
            pending = new Queue<JObject>(entries);
            total = entries.Length;
            records.Clear();
            Directory.CreateDirectory(Report + "/" + phase + "/Models");
            Progress("running");
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (pending.Count == 0)
                {
                    File.WriteAllText(Report + "/" + phase + "/inventory.json", JsonConvert.SerializeObject(records, Formatting.Indented));
                    Progress("complete");
                    EditorApplication.update -= Tick;
                    pending = null;
                    return;
                }
                Capture(pending.Dequeue());
                Progress("running");
            }
            catch (Exception error)
            {
                Progress("failed", error.ToString());
                EditorApplication.update -= Tick;
                pending = null;
                Debug.LogException(error);
            }
        }

        static void Progress(string state, string error = null) => File.WriteAllText(Report + "/capture_status.json",
            JsonConvert.SerializeObject(new { state, phase, completed = records.Count, total, utc = DateTime.UtcNow, error }, Formatting.Indented));

        static void Capture(JObject entry)
        {
            var name = (string)entry["name"];
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            var placed = view.cities[1].GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            var source = placed != null ? placed.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(CountryComparisonImporter.PrefabRoot + "/Prefabs/" + name + ".prefab");
            var root = Object.Instantiate(source);
            root.name = "Quality review " + name;
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(true);
            root.transform.SetPositionAndRotation(new Vector3(2000, 0, 2000), Quaternion.identity);
            root.transform.localScale = Vector3.one;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            // The isolated mill has no shared river, so show its supplied water surface here.
            if (name == "Watermill")
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Flowing millrace water", StringComparison.Ordinal))) renderer.enabled = true;
            var temporary = new GameObject("Temporary quality review camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = temporary.AddComponent<Camera>();
            camera.CopyFrom(view.viewCamera);
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.27f, .285f, .29f);
            camera.orthographic = true;
            camera.aspect = 4f / 3;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 500;
            var data = temporary.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(), data);
            data.renderPostProcessing = false;
            GameObject floor = null;
            Material floorMaterial = null;
            try
            {
                var bounds = CountryComparisonImporter.BoundsOf(root);
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.hideFlags = HideFlags.HideAndDontSave;
                floor.layer = 31;
                floor.transform.position = new Vector3(bounds.center.x, bounds.min.y - .055f, bounds.center.z);
                var diameter = Mathf.Max(bounds.size.x, bounds.size.z) * 1.25f;
                floor.transform.localScale = new Vector3(diameter, .1f, diameter);
                floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                floorMaterial.SetColor("_BaseColor", new Color(.34f, .35f, .33f));
                floorMaterial.SetFloat("_Smoothness", .1f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                Shoot(camera, bounds, new Vector3(.8f, .65f, -1.1f), Report + "/" + phase + "/Models/" + name + ".png");
                if ((string)entry["group"] is "Buildings" or "Academies")
                    Shoot(camera, bounds, new Vector3(-.8f, .65f, 1.1f), Report + "/" + phase + "/Models/" + name + "_Rear.png");
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                var meshes = root.GetComponentsInChildren<MeshFilter>(true);
                records.Add(new { name, group = (string)entry["group"], label = (string)entry["display_name"], placed = placed != null,
                    size = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                    missingMeshes = meshes.Count(m => m.sharedMesh == null), missingMaterials = renderers.Sum(r => r.sharedMaterials.Count(m => m == null)),
                    parts = renderers.Select(r => new { name = r.name, enabled = r.enabled, materials = r.sharedMaterials.Select(m => m != null ? m.name : "MISSING").ToArray() }).ToArray() });
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(floorMaterial);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(temporary);
            }
        }

        static void Shoot(Camera camera, Bounds bounds, Vector3 direction, string path)
        {
            camera.transform.position = bounds.center + direction.normalized * Mathf.Max(20, bounds.size.magnitude * 2);
            camera.transform.LookAt(bounds.center);
            float extent = 0;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var point = camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        extent = Mathf.Max(extent, Mathf.Abs(point.y), Mathf.Abs(point.x) / camera.aspect);
                    }
            camera.orthographicSize = extent * 1.13f;
            var previous = RenderTexture.active;
            var target = new RenderTexture(1200, 900, 24) { antiAliasing = 4 };
            var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
