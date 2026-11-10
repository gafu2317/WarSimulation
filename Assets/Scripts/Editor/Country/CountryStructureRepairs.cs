using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryStructureRepairs
    {
        const string Root = "Assets/Prefabs/Kingdom/Comparison/Meshes/ArenaPassage";
        const float HalfWidth = 1.1f;

        public static void ApplyBridgeOrientation(GameObject instance)
        {
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true).Where(f=>
                f.name.StartsWith("Continuous cambered bridge paving") || f.name.StartsWith("Bridge arch intrados")))
            {
                var source=filter.sharedMesh;
                var path="Assets/Prefabs/Kingdom/Comparison/Meshes/"+filter.name+" oriented.asset";
                var mesh=Object.Instantiate(source);var triangles=mesh.triangles;var positions=mesh.vertices;
                var up=filter.name.StartsWith("Continuous cambered bridge paving")?1:-1;
                for(var i=0;i<triangles.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(positions[triangles[i]]);var b=filter.transform.TransformPoint(positions[triangles[i+1]]);var c=filter.transform.TransformPoint(positions[triangles[i+2]]);
                    if(Vector3.Cross(b-a,c-a).y*up<0)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
                }
                mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
                else{EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
                filter.sharedMesh=saved;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
        }

        public static void ApplyArenaPassage(GameObject instance)
        {
            if (instance.name != "Arena") return;
            var targets = instance.GetComponentsInChildren<MeshFilter>(true).Where(f =>
                f.name.StartsWith("TieredSpectatorSeating_") || f.name.StartsWith("SupportedArenaArcades_")).ToArray();
            if (targets.Length == 0) return;
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/Prefabs/Kingdom/Comparison/Meshes", "ArenaPassage");
            foreach (var filter in targets)
            {
                var source = filter.sharedMesh;
                if (AssetDatabase.GetAssetPath(source).StartsWith(Root + "/")) continue;
                if (source.subMeshCount != 1) throw new InvalidOperationException("Arena passage expects a single-material part: " + filter.name);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
                var path = Root + "/" + filter.name + "_" + guid + "_" + localId + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var matrix = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = OpenArenaPassage(source, matrix);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); }
                filter.sharedMesh = saved;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                foreach (var collider in filter.GetComponents<MeshCollider>().Where(c => c.sharedMesh == source))
                {
                    collider.sharedMesh = saved;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }
        }

        public static Mesh OpenArenaPassage(Mesh source, Matrix4x4 localToArena)
        {
            var cutout = new[] { new Vector2(-HalfWidth, -30), new Vector2(HalfWidth, -30),
                new Vector2(HalfWidth, 0), new Vector2(-HalfWidth, 0) };
            var mesh = CountryPavingCutout.Subtract(source, localToArena, new[] { cutout });
            var vertices = mesh.vertices.ToList(); var uv = mesh.uv.ToList(); var triangles = mesh.triangles.ToList();
            foreach (var plane in new[] { -HalfWidth, HalfWidth })
                CloseCut(vertices, uv, triangles, localToArena, plane);
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh.name = source.name + " with open arena passage";
            return mesh;
        }

        static void CloseCut(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Matrix4x4 matrix, float plane)
        {
            var positions = vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var points = new Dictionary<Vector2Int, Vector3>();
            var edges = new Dictionary<(Vector2Int, Vector2Int), int>();
            Vector2Int Key(Vector3 p) => new(Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            for (var i = 0; i < triangles.Count; i += 3)
                for (var j = 0; j < 3; j++)
                {
                    var a = positions[triangles[i + j]]; var b = positions[triangles[i + (j + 1) % 3]];
                    if (Mathf.Abs(a.x - plane) > .0001f || Mathf.Abs(b.x - plane) > .0001f || a.z > .0001f || b.z > .0001f) continue;
                    var ka = Key(a); var kb = Key(b);
                    if (ka == kb) continue;
                    points[ka] = a; points[kb] = b;
                    var edge = ka.x < kb.x || ka.x == kb.x && ka.y < kb.y ? (ka, kb) : (kb, ka);
                    edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
            var boundary = new HashSet<(Vector2Int, Vector2Int)>(edges.Where(e => e.Value == 1).Select(e => e.Key));
            while (boundary.Count > 0)
            {
                var first = boundary.First(); boundary.Remove(first);
                var loop = new List<Vector2Int> { first.Item1 }; var current = first.Item2;
                while (current != loop[0])
                {
                    loop.Add(current);
                    var next = boundary.FirstOrDefault(e => e.Item1 == current || e.Item2 == current);
                    if (!boundary.Remove(next)) throw new InvalidOperationException("An arena passage cut has an open side boundary.");
                    current = next.Item1 == current ? next.Item2 : next.Item1;
                }
                Cap(loop.Select(k => points[k]).ToList(), vertices, uv, triangles, matrix.inverse, plane < 0 ? 1 : -1);
            }
        }

        static void Cap(List<Vector3> loop, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Matrix4x4 inverse, int normal)
        {
            float Cross(Vector3 a, Vector3 b, Vector3 c) => (b.y - a.y) * (c.z - a.z) - (b.z - a.z) * (c.y - a.y);
            var area = 0f;
            for (var i = 0; i < loop.Count; i++) area += loop[i].y * loop[(i + 1) % loop.Count].z - loop[i].z * loop[(i + 1) % loop.Count].y;
            var sign = Mathf.Sign(area); var remaining = Enumerable.Range(0, loop.Count).ToList();
            var start = vertices.Count;
            foreach (var point in loop) { vertices.Add(inverse.MultiplyPoint3x4(point)); uv.Add(new Vector2(point.z / 2, point.y / 2)); }
            while (remaining.Count >= 3)
            {
                var found = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var a = remaining[(i + remaining.Count - 1) % remaining.Count]; var b = remaining[i]; var c = remaining[(i + 1) % remaining.Count];
                    if (Cross(loop[a], loop[b], loop[c]) * sign <= .0000001f) continue;
                    if (remaining.Any(p => p != a && p != b && p != c && Cross(loop[a], loop[b], loop[p]) * sign > .0000001f &&
                        Cross(loop[b], loop[c], loop[p]) * sign > .0000001f && Cross(loop[c], loop[a], loop[p]) * sign > .0000001f)) continue;
                    triangles.AddRange(sign == normal ? new[] { start + a, start + b, start + c } : new[] { start + c, start + b, start + a });
                    remaining.RemoveAt(i); found = true; break;
                }
                if (!found)
                {
                    // Straight subdivisions left by clipping have no remaining face area.
                    if (remaining.All(i => Mathf.Abs(Cross(loop[remaining[0]], loop[remaining[1]], loop[i])) < .000001f)) break;
                    throw new InvalidOperationException("An arena passage side could not be closed.");
                }
            }
        }
    }
}
