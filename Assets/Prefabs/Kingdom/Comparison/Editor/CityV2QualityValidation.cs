using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CityV2QualityValidation
    {
        [MenuItem("WarSim/Country/Quality Review/Validate Geometry And Materials")]
        public static void Validate()
        {
            var entries = JObject.Parse(File.ReadAllText(CountryComparisonImporter.ModelRoot + "/export_manifest.json"))["assets"];
            var failures = new List<string>();
            var records = new List<object>();
            foreach (var entry in entries)
            {
                var name = (string)entry["name"];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountryComparisonImporter.PrefabRoot + "/Prefabs/" + name + ".prefab");
                if (prefab == null) { failures.Add(name + ": missing prefab"); continue; }
                var triangleCount = 0;
                var degenerates = new List<string>();
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (mesh == null || renderer == null) { failures.Add(name + "/" + filter.name + ": missing mesh or renderer"); continue; }
                    var vertices = mesh.vertices;
                    var uv = mesh.uv;
                    if (vertices.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z))) failures.Add(name + "/" + filter.name + ": invalid vertex");
                    if (uv.Length != vertices.Length || uv.Any(v => !Finite(v.x) || !Finite(v.y))) failures.Add(name + "/" + filter.name + ": missing or invalid UV");
                    if (renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null || m.shader == null || ShaderUtil.ShaderHasError(m.shader)))
                        failures.Add(name + "/" + filter.name + ": missing material or shader error");
                    for (var sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var indices = mesh.GetTriangles(sub);
                        triangleCount += indices.Length / 3;
                        var count = 0;
                        for (var i = 0; i < indices.Length; i += 3)
                            if (Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).sqrMagnitude < 1e-14f) count++;
                        if (count > 0) degenerates.Add(filter.name + ": " + count);
                    }
                }
                var bounds = CountryComparisonImporter.BoundsOf(prefab);
                if (Mathf.Abs(bounds.min.y) > .001f) failures.Add(name + ": prefab base is not grounded");
                if (degenerates.Count > 0) failures.Add(name + ": degenerate triangles");
                records.Add(new { name, triangles = triangleCount, degenerateParts = degenerates });
            }
            var observatory = AssetDatabase.LoadAssetAtPath<GameObject>(CountryComparisonImporter.PrefabRoot + "/Prefabs/Observatory.prefab");
            var glass = observatory.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m.name == "Architectural_Glass");
            if (glass == null || glass.GetFloat("_Surface") != 1 || glass.GetColor("_BaseColor").a >= 1 || glass.GetFloat("_ZWrite") != 0)
                failures.Add("Observatory glass must transmit light and reveal the telescope.");
            var cobble = AssetDatabase.LoadAssetAtPath<Material>(CountryComparisonImporter.PrefabRoot + "/Materials/Cobble.mat");
            if (cobble.shader.name != "WarSimulation/City Surface Tone" || cobble.GetColor("_BaseColor").a > .2f)
                failures.Add("Paving saturation improvement is missing.");
            File.WriteAllText(CityV2QualityReview.Report + "/geometry_validation.json", JsonConvert.SerializeObject(new {
                status = failures.Count == 0 ? "PASS" : "FAIL", utc = DateTime.UtcNow, assets = records.Count, failures, records
            }, Formatting.Indented));
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures.Take(16)));
            CountryRoleFacilities.Validate();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
