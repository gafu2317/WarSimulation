using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountrySurfaceProbe
    {
        const string Output = "Artifacts/CountryComparison/FlickerInvestigation_2026-10-23";
        [Serializable] class Face
        {
            public string city, path, mesh, material;
            public Vector3 a, b, c;
            public Vector2 ua, ub, uc;
        }
        [Serializable] class MeshInfo
        {
            public string asset, name, error;
            public int instances, vertices, triangles, duplicates, differentMaterialDuplicates, sameFacingDuplicates, mismatchedUvDuplicates;
            public List<string> examples = new();
        }
        [Serializable] class MaterialInfo
        {
            public string path, shader, texture, filter;
            public int queue, width, mipmaps, anisotropy;
            public float smoothness;
        }
        [Serializable] class Report
        {
            public string scene, unity, graphics;
            public bool sceneDirty;
            public float near, far;
            public int renderers;
            public List<string> excessMaterialSlots = new();
            public List<Face> ground = new();
            public List<MeshInfo> meshes = new();
            public List<MaterialInfo> materials = new();
        }

        [MenuItem("WarSim/Country/Diagnostics/Audit Surface Overlaps")]
        public static void Audit()
        {
            Directory.CreateDirectory(Output);
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (view == null) throw new InvalidOperationException("Open the Country comparison scene.");
            var root = view.transform;
            var camera = view.viewCamera;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var report = new Report { scene = SceneManager.GetActiveScene().path, sceneDirty = SceneManager.GetActiveScene().isDirty,
                unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), near = camera.nearClipPlane,
                far = camera.farClipPlane, renderers = renderers.Length };
            var meshes = new Dictionary<Mesh, MeshInfo>();
            var materials = new HashSet<Material>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                var slots = renderer.sharedMaterials;
                foreach (var material in slots) if (material != null) materials.Add(material);
                if (slots.Length > mesh.subMeshCount)
                    report.excessMaterialSlots.Add(PathOf(renderer.transform) + " slots=" + slots.Length + " submeshes=" + mesh.subMeshCount);
                if (!meshes.TryGetValue(mesh, out var info))
                {
                    info = InspectMesh(mesh);
                    meshes.Add(mesh, info);
                    report.meshes.Add(info);
                }
                info.instances++;
                if (info.error != null) continue;
                var city = renderer.transform.IsChildOf(view.cities[0].transform) ? "old" :
                    renderer.transform.IsChildOf(view.cities[1].transform) ? "new" : "shared";
                var matrix = renderer.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                var world = vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    var material = slots.Length > sub ? slots[sub] : null;
                    for (var i = 0; i < indices.Length; i += 3)
                    {
                        var ia = indices[i]; var ib = indices[i + 1]; var ic = indices[i + 2];
                        var a = world[ia]; var b = world[ib]; var c = world[ic];
                        if (a.y < -.1f || a.y > .3f || Mathf.Abs(a.y-b.y) > .0001f || Mathf.Abs(a.y-c.y) > .0001f) continue;
                        var normal = Vector3.Cross(b-a,c-a);
                        if (normal.y <= .000001f) continue;
                        report.ground.Add(new Face { city = city, path = PathOf(renderer.transform), mesh = info.asset,
                            material = material == null ? "null" : AssetDatabase.GetAssetPath(material), a = a, b = b, c = c,
                            ua = uv.Length > ia ? uv[ia] : Vector2.zero, ub = uv.Length > ib ? uv[ib] : Vector2.zero,
                            uc = uv.Length > ic ? uv[ic] : Vector2.zero });
                    }
                }
            }
            foreach (var material in materials)
            {
                var texture = material.GetTexture("_BaseMap") as Texture2D;
                report.materials.Add(new MaterialInfo { path = AssetDatabase.GetAssetPath(material), shader = material.shader.name,
                    texture = texture == null ? null : AssetDatabase.GetAssetPath(texture), queue = material.renderQueue,
                    width = texture == null ? 0 : texture.width, mipmaps = texture == null ? 0 : texture.mipmapCount,
                    filter = texture == null ? null : texture.filterMode.ToString(), anisotropy = texture == null ? 0 : texture.anisoLevel,
                    smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : -1 });
            }
            File.WriteAllText(Output + "/surface_audit.json", JsonUtility.ToJson(report, true));
            Debug.Log("Surface audit: " + report.renderers + " renderers, " + report.meshes.Count + " meshes, " + report.ground.Count + " ground triangles.");
        }

        static MeshInfo InspectMesh(Mesh mesh)
        {
            var info = new MeshInfo { asset = AssetDatabase.GetAssetPath(mesh), name = mesh.name, vertices = mesh.vertexCount };
            try
            {
                var vertices = mesh.vertices;
                var known = new Dictionary<string, (int sub, int a, int b, int c)>();
                var uv = mesh.uv;
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    info.triangles += indices.Length / 3;
                    for (var i = 0; i < indices.Length; i += 3)
                    {
                        var a = vertices[indices[i]]; var b = vertices[indices[i+1]]; var c = vertices[indices[i+2]];
                        if (Vector3.Cross(b-a,c-a).sqrMagnitude < 1e-12f) continue;
                        var keys = new[] { Key(a), Key(b), Key(c) };
                        Array.Sort(keys, StringComparer.Ordinal);
                        var key = string.Join("/", keys);
                        if (known.TryGetValue(key, out var previous))
                        {
                            info.duplicates++;
                            if (previous.sub != sub) info.differentMaterialDuplicates++;
                            var oldNormal = Vector3.Cross(vertices[previous.b]-vertices[previous.a],vertices[previous.c]-vertices[previous.a]).normalized;
                            var normalDot = Vector3.Dot(oldNormal, Vector3.Cross(b-a,c-a).normalized);
                            if (normalDot > .99f) info.sameFacingDuplicates++;
                            var oldUv = uv.Length > previous.c ? (uv[previous.a]+uv[previous.b]+uv[previous.c])/3 : Vector2.zero;
                            var newUv = uv.Length > indices[i+2] ? (uv[indices[i]]+uv[indices[i+1]]+uv[indices[i+2]])/3 : Vector2.zero;
                            if ((oldUv-newUv).sqrMagnitude > .000001f) info.mismatchedUvDuplicates++;
                            if (info.examples.Count < 6) info.examples.Add("submeshes " + previous.sub + "/" + sub + " at " + (a+b+c)/3 + " normalDot=" + normalDot + " UV=" + oldUv.ToString("F5") + "/" + newUv.ToString("F5"));
                        }
                        else known.Add(key, (sub,indices[i],indices[i+1],indices[i+2]));
                    }
                }
            }
            catch (Exception error) { info.error = error.Message; }
            return info;
        }

        static string Key(Vector3 p) => Mathf.RoundToInt(p.x * 10000) + "," + Mathf.RoundToInt(p.y * 10000) + "," + Mathf.RoundToInt(p.z * 10000);
        static string PathOf(Transform item) => item.parent == null ? item.name : PathOf(item.parent) + "/" + item.name;

        [Serializable] class CaptureInfo
        {
            public string city, site, intervention;
            public Vector2[] region;
            public int removedSubmeshes;
        }

        [MenuItem("WarSim/Country/Diagnostics/Capture Overlap Experiments")]
        public static void CaptureExperiments()
        {
            Directory.CreateDirectory(Output);
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            var temporary = new GameObject("Temporary surface investigation camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(view.viewCamera); camera.enabled = false;
            camera.aspect = 16f / 9; camera.fieldOfView = 40;
            var data = temporary.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(), data);
            var records = new List<CaptureInfo>();
            try
            {
                for (var city = 0; city < 2; city++)
                {
                    var label = city == 0 ? "Old" : "New";
                    var center = view.centers[city];
                    SetCamera(camera, center + new Vector3(0,0,-106), new Vector3(10,12,-14));
                    var paving = view.cities[city].transform.Find("Connected streets and entrance paths").Cast<Transform>()
                        .Where(t => t.name == "Paved_Plot").SelectMany(t => t.GetComponentsInChildren<MeshRenderer>()).ToArray();
                    var states = paving.Select(r => r.enabled).ToArray();
                    Pair(camera, label + "_Gate_Baseline");
                    var region = Region(camera, center + new Vector3(0,.025f,-107.5f), 7.5f,6);
                    try
                    {
                        foreach (var renderer in paving) renderer.enabled = false;
                        Pair(camera, label + "_Gate_OneSurface");
                    }
                    finally { for (var i=0;i<paving.Length;i++) paving[i].enabled = states[i]; }
                    records.Add(new CaptureInfo { city = label, site = "South gate", intervention = "Temporarily hide overlapping Paved_Plot renderers", region = region });

                    SetCamera(camera, center + new Vector3(0,0,112), new Vector3(9,11,-13));
                    Pair(camera, label + "_Junction_Baseline");
                    var junction = view.cities[city].transform.Find("Market, yards and street life/Public courts and lit trading apron/Road_Cross");
                    var removed = RemoveSubmeshes(junction, m => m.name == "Earth", () => Pair(camera, label + "_Junction_OneSurface"));
                    records.Add(new CaptureInfo { city = label, site = "North junction", intervention = "Temporarily omit Road_Cross Earth submesh", region = Region(camera, center+new Vector3(0,0,112),6,6), removedSubmeshes = removed });

                    SetCamera(camera, center + new Vector3(-67,0,14.15f), new Vector3(18,21,-25));
                    Pair(camera, label + "_Mill_Baseline");
                    var mill = view.cities[city].transform.Find("Buildings and courtyards/Mill quay/Watermill");
                    removed = RemoveSubmeshes(mill, m => m.name == "Water", () => Pair(camera, label + "_Mill_OneSurface"));
                    records.Add(new CaptureInfo { city = label, site = "Watermill", intervention = "Temporarily omit built-in Water submesh", removedSubmeshes = removed });
                }
                File.WriteAllText(Output + "/capture_experiments.json", JsonUtility.ToJson(new CaptureList { experiments = records.ToArray() }, true));
            }
            finally { Object.DestroyImmediate(temporary); }
        }

        [Serializable] class CaptureList { public CaptureInfo[] experiments; }

        static int RemoveSubmeshes(Transform root, Func<Material,bool> predicate, Action capture)
        {
            var originals = new Dictionary<MeshFilter,Mesh>(); var temporary = new List<Mesh>(); var removed = 0;
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var indices = renderer.sharedMaterials.Select((m,i) => (m,i)).Where(p => predicate(p.m)).Select(p => p.i).ToArray();
                    if (indices.Length == 0) continue;
                    var filter = renderer.GetComponent<MeshFilter>(); var original = filter.sharedMesh;
                    var clone = Object.Instantiate(original); clone.hideFlags = HideFlags.HideAndDontSave;
                    originals.Add(filter,original); temporary.Add(clone);
                    foreach (var index in indices) { clone.SetTriangles(Array.Empty<int>(),index); removed++; }
                    filter.sharedMesh = clone;
                }
                capture();
            }
            finally
            {
                foreach (var entry in originals) entry.Key.sharedMesh = entry.Value;
                foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
            }
            return removed;
        }

        static void Pair(Camera camera, string name)
        {
            var position = camera.transform.position;
            try
            {
                CountryComparisonBuilder.CaptureCamera(camera, Output + "/" + name + "_A.png");
                camera.transform.position += Vector3.right * .003f;
                CountryComparisonBuilder.CaptureCamera(camera, Output + "/" + name + "_B.png");
            }
            finally { camera.transform.position = position; }
        }

        static void SetCamera(Camera camera, Vector3 target, Vector3 offset)
        {
            camera.transform.position = target + offset;
            camera.transform.LookAt(target + Vector3.up);
        }

        static Vector2[] Region(Camera camera, Vector3 center, float width, float depth)
        {
            return new[] {new Vector3(-width/2,0,-depth/2),new Vector3(width/2,0,-depth/2),new Vector3(width/2,0,depth/2),new Vector3(-width/2,0,depth/2)}
                .Select(p => camera.WorldToViewportPoint(center+p)).Select(p => new Vector2(p.x*1920,(1-p.y)*1080)).ToArray();
        }
    }
}
