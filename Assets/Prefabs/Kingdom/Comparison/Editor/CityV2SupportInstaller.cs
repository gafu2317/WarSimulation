using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CityV2SupportInstaller
    {
        const string Report = CityV2QualityReview.Report;
        const string Prefabs = CountryComparisonImporter.PrefabRoot;
        sealed class Placement
        {
            public GameObject root;
            public Transform parent;
            public string name;
            public Vector3 position, scale, center, size;
            public Quaternion rotation;
            public Dictionary<string, Material> roofs;
        }

        [MenuItem("WarSim/Country/Quality Review/Install Support Assets")]
        public static void Install()
        {
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation before importing models.");
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            try { InstallModels(); }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        static void InstallModels()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.cities.Length != 2 || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open the two-city Country scene in Edit mode.");
            var entries = JObject.Parse(File.ReadAllText(Report + "/Generated/support_generation.json"))["assets"].OfType<JObject>().ToArray();
            if (entries.Length != 50 || entries.Any(e => !File.Exists((string)e["staged"]))) throw new InvalidOperationException("Expected all 50 reviewed support assets.");
            var names = entries.Select(e => (string)e["name"]).ToHashSet();
            var placements = view.cities[1].GetComponentsInChildren<Transform>(true)
                .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && names.Contains(t.name)).Select(Snapshot).ToArray();
            File.WriteAllText(Report + "/support_placements_before.json", JsonConvert.SerializeObject(placements.Select(p => new {
                p.name, position = Values(p.position), scale = Values(p.scale), center = Values(p.center), size = Values(p.size)
            }), Formatting.Indented));
            CityV2MaterialQuality.Apply();
            var manifestPath = CountryComparisonImporter.ModelRoot + "/export_manifest.json";
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var sizes = new Dictionary<string, Vector3>();
            foreach (var entry in entries)
            {
                var name = (string)entry["name"];
                var path = (string)entry["fbx"];
                File.Copy((string)entry["staged"], path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.bakeAxisConversion = true;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var root = new GameObject(name);
                try
                {
                    var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
                    model.name = "Reviewed support geometry";
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Prefabs + "/Materials/" + m.name + ".mat")
                            ?? throw new InvalidOperationException("Missing material " + m.name)).ToArray();
                    var bounds = CountryComparisonImporter.BoundsOf(root);
                    sizes[name] = bounds.size;
                    model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/Prefabs/" + name + ".prefab");
                    var target = manifest["assets"].OfType<JObject>().Single(e => (string)e["name"] == name);
                    foreach (var property in entry.Properties()) target[property.Name] = property.Value.DeepClone();
                    target["minimum"] = new JArray(-bounds.size.x / 2, 0, -bounds.size.z / 2);
                    target["maximum"] = new JArray(bounds.size.x / 2, bounds.size.y, bounds.size.z / 2);
                    target["coordinateSystem"] = "Unity X right, Y up, front -Z";
                }
                finally { Object.DestroyImmediate(root); }
            }
            foreach (var p in placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Prefabs/" + p.name + ".prefab");
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p.parent);
                replacement.name = p.name;
                replacement.transform.SetPositionAndRotation(p.position, p.rotation);
                var size = sizes[p.name];
                var ratio = new Vector3(p.size.x / size.x, p.size.y / size.y, p.size.z / size.z);
                var group = (string)entries.Single(e => (string)e["name"] == p.name)["group"];
                // Bridges and stairs must meet their existing landings exactly.
                if (group != "Infrastructure")
                {
                    var fit = Mathf.Min(ratio.x, ratio.z);
                    if (group != "Buildings") fit = Mathf.Min(fit, ratio.y);
                    ratio = Vector3.one * fit;
                }
                replacement.transform.localScale = Vector3.Scale(p.scale, ratio);
                var bounds = CountryComparisonImporter.BoundsOf(replacement);
                replacement.transform.position += p.center - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var renderer in replacement.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => p.roofs.GetValueOrDefault(m.name, m)).ToArray();
                    // The continuous river already covers the millrace; a second water plane flickers.
                    if (p.name == "Watermill" && renderer.name.StartsWith("Flowing millrace water", StringComparison.Ordinal)) renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
                Undo.RegisterCreatedObjectUndo(replacement, "Improve city support asset");
                Undo.DestroyObjectImmediate(p.root);
            }
            File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(manifestPath);
            CountryGroundSurfaces.Apply(view.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            File.WriteAllText(Report + "/support_installation.json", JsonConvert.SerializeObject(new {
                assets = entries.Length, instances = placements.Length, utc = DateTime.UtcNow
            }, Formatting.Indented));
            CountryRoleFacilities.Validate();
        }

        static Placement Snapshot(Transform t)
        {
            var world = CountryComparisonImporter.BoundsOf(t.gameObject);
            var points = new List<Vector3>();
            foreach (var renderer in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = renderer.localBounds;
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                            points.Add(t.InverseTransformPoint(renderer.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)))));
            }
            var local = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) local.Encapsulate(point);
            var roofs = new Dictionary<string, Material>();
            foreach (var material in t.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials))
            {
                var offset = material.name.IndexOf("Roof_", StringComparison.Ordinal);
                if (offset > 0) roofs[material.name.Substring(offset)] = material;
            }
            return new Placement { root = t.gameObject, parent = t.parent, name = t.name, position = t.position, scale = t.localScale,
                rotation = t.rotation, size = local.size, center = new Vector3(world.center.x, world.min.y, world.center.z), roofs = roofs };
        }

        static float[] Values(Vector3 value) => new[] { value.x, value.y, value.z };
    }
}
