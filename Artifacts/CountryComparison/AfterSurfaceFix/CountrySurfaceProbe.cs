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
        const string Output = "Artifacts/CountryComparison/AfterSurfaceFix";
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


        [MenuItem("WarSim/Country/Diagnostics/Capture Before Ground Fix")]
        public static void CaptureBefore() => CaptureViews("Artifacts/CountryComparison/BeforeSurfaceFix/Captures");
        [MenuItem("WarSim/Country/Diagnostics/Capture After Ground Fix")]
        public static void CaptureAfter() => CaptureViews(Output);

        static void CaptureViews(string folder)
        {
            Directory.CreateDirectory(folder);
            var view=Object.FindAnyObjectByType<CountryComparisonView>();
            var temporary=new GameObject("Temporary surface validation camera") {hideFlags=HideFlags.HideAndDontSave};
            var camera=temporary.AddComponent<Camera>(); camera.CopyFrom(view.viewCamera); camera.enabled=false;
            camera.aspect=16f/9; camera.fieldOfView=40;
            var data=temporary.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(),data);
            var names=new[]{"Gate","EastGate","Junction","Market","Mill"};
            var targets=new[]{new Vector3(0,0,-106),new Vector3(106,0,-34),new Vector3(0,0,112),new Vector3(0,0,-27),new Vector3(-67,0,14.15f)};
            var offsets=new[]{new Vector3(10,12,-14),new Vector3(12,13,-10),new Vector3(9,11,-13),new Vector3(22,28,-39),new Vector3(18,21,-25)};
            try
            {
                for(var city=0;city<2;city++) for(var shot=0;shot<names.Length;shot++)
                {
                    var target=view.centers[city]+targets[shot];
                    camera.transform.position=target+offsets[shot]; camera.transform.LookAt(target+Vector3.up);
                    var label=(city==0?"Old_":"New_")+names[shot];
                    CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+label+"_A.png");
                    camera.transform.position+=Vector3.right*.003f;
                    CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+label+"_B.png");
                }
            }
            finally {Object.DestroyImmediate(temporary);}
        }
    }
}
