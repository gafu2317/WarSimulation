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
    public static class CountryFacilityIdentityReview
    {
        const string Report = "Artifacts/CityV2Identity20261030";

        [MenuItem("WarSim/Country/Facility Identity/Capture Before")]
        public static void Before() => Capture("Before");

        [MenuItem("WarSim/Country/Facility Identity/Capture After")]
        public static void After() => Capture("After");

        static void Capture(string phase)
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.cities.Length != 2 || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open the two-city Country scene in Edit mode.");
            var folder = Report + "/" + phase + "/Streets";
            Directory.CreateDirectory(folder);
            var specs = JObject.Parse(File.ReadAllText("Tools/Blender/RoleFacilities/design.json"))["assets"].OfType<JObject>()
                .Where(e => (bool)e["placed"] && e["identity"] != null).ToArray();
            var visible = view.cities.Select(c => c.activeSelf).ToArray();
            var temp = new GameObject("Temporary facility identity review") { hideFlags = HideFlags.HideAndDontSave };
            var camera = temp.AddComponent<Camera>();
            camera.CopyFrom(view.viewCamera);
            camera.enabled = false;
            camera.aspect = 16f / 9;
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 800;
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(), temp.AddComponent<UniversalAdditionalCameraData>());
            var records = new List<object>();
            try
            {
                for (var i = 0; i < view.cities.Length; i++) view.SetCityVisible(i, i == 1);
                foreach (var spec in specs)
                {
                    var name = (string)spec["name"];
                    var plot = spec["plot"];
                    var size = Mathf.Max((float)plot["width"], (float)plot["depth"]);
                    var target = view.centers[1] + new Vector3((float)plot["x"], 5, (float)plot["z"]);
                    camera.transform.position = target + new Vector3(.55f, .78f, -1.15f).normalized * 150;
                    camera.transform.LookAt(target);
                    var subject = view.cities[1].GetComponentsInChildren<Transform>(true)
                        .Single(t => t.name == name && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
                    foreach (var distance in new[] { "Medium", "Near" })
                    {
                        camera.orthographicSize = size * (distance == "Medium" ? 1.05f : .68f);
                        CountryComparisonBuilder.CaptureCamera(camera, folder + "/" + name + "_" + distance + ".png");
                        if (distance != "Medium") continue;
                        var renderers = subject.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.name.StartsWith("Landmark_", StringComparison.Ordinal)).ToArray();
                        var min = Vector2.one * float.PositiveInfinity;
                        var max = Vector2.one * float.NegativeInfinity;
                        foreach (var renderer in renderers)
                        {
                            var b = renderer.bounds;
                            for (var x = -1; x <= 1; x += 2)
                                for (var y = -1; y <= 1; y += 2)
                                    for (var z = -1; z <= 1; z += 2)
                                    {
                                        var p = (Vector2)camera.WorldToViewportPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                                        min = Vector2.Min(min, p);
                                        max = Vector2.Max(max, p);
                                    }
                        }
                        records.Add(new { name, label = (string)spec["label"], identity = (string)spec["identity"],
                            landmarkRenderers = renderers.Length,
                            identityPixelsAt960 = renderers.Length == 0 ? new[] { 0f, 0f } : new[] { (max.x - min.x) * 960, (max.y - min.y) * 540 },
                            position = new[] { subject.position.x, subject.position.y, subject.position.z },
                            scale = new[] { subject.localScale.x, subject.localScale.y, subject.localScale.z } });
                    }
                }
                camera.orthographic = false;
                camera.fieldOfView = 40;
                for (var set = 0; set < 2; set++)
                {
                    for (var i = 0; i < view.cities.Length; i++) view.SetCityVisible(i, i == set);
                    var target = view.centers[set] + new Vector3(0, 6, -5);
                    camera.transform.position = target + new Vector3(0, 209, -240);
                    camera.transform.LookAt(target);
                    CountryComparisonBuilder.CaptureCamera(camera, folder + "/" + (set == 0 ? "Old" : "New") + "_Overview.png");
                }
                File.WriteAllText(Report + "/" + phase + "/identity_capture.json", JsonConvert.SerializeObject(new {
                    status = "PASS", phase, facilities = specs.Length, images = Directory.GetFiles(folder, "*.png").Length,
                    utc = DateTime.UtcNow, records
                }, Formatting.Indented));
            }
            finally
            {
                for (var i = 0; i < view.cities.Length; i++) view.SetCityVisible(i, visible[i]);
                Object.DestroyImmediate(temp);
            }
        }
    }
}
