using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryMeshBatchBuilder
    {
        const string MeshRoot = "Assets/Prefabs/Kingdom/Comparison/Meshes/Combined";

        sealed class Group
        {
            public Material material;
            public MeshRenderer renderer;
            public readonly List<CombineInstance> parts = new();
        }

        [MenuItem("WarSim/Country/Optimize Scene Meshes")]
        public static void BakeScene()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (view == null) throw new System.InvalidOperationException("Open the Country comparison scene.");
            var reduction = view.cities.Where(city => city != null).Sum(city => Bake(city.transform));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(view.gameObject.scene);
            Debug.Log("Country meshes combined: " + reduction + " fewer renderers.");
        }

        public static int Bake(Transform city)
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(city.gameObject)?.EndsWith(".townlayout", System.StringComparison.Ordinal) == true)
                return 0;
            Restore(city);
            var roots = city.GetComponentsInChildren<Transform>(true).Where(t =>
                PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) == t.gameObject).ToArray();
            var meshes = new Dictionary<string, Mesh>();
            return roots.Sum(root => BakeObject(root.gameObject, MeshRoot, meshes));
        }

        public static void Restore(Transform city)
        {
            foreach (var batch in city.GetComponentsInChildren<CountryMeshBatch>(true))
            {
                foreach (var source in batch.sources)
                    if (source != null)
                    {
                        source.enabled = true;
                        if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                    }
                foreach (var mesh in batch.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Distinct())
                    if (mesh != null && !AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(batch.gameObject);
            }
        }

        public static int BakeObject(GameObject root, string meshAssetFolder = null) =>
            BakeObject(root, meshAssetFolder, new Dictionary<string, Mesh>());

        static int BakeObject(GameObject root, string meshAssetFolder, Dictionary<string, Mesh> meshes)
        {
            Restore(root.transform);
            var groups = new List<Group>();
            var sources = new List<MeshRenderer>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!CanCombine(renderer, filter, root.transform)) continue;
                var mesh = filter.sharedMesh;
                var matrix = RelativeMatrix(filter.transform, root.transform);
                if (matrix.determinant <= 0 || Enumerable.Range(0, mesh.subMeshCount).All(i => mesh.GetIndexCount(i) == 0)) continue;
                sources.Add(renderer);
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetIndexCount(sub) == 0) continue;
                    var material = renderer.sharedMaterials[sub];
                    var group = groups.FirstOrDefault(g => g.material == material && SameState(g.renderer, renderer));
                    if (group == null) groups.Add(group = new Group { material = material, renderer = renderer });
                    group.parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                }
            }
            if (groups.Count >= sources.Count) return 0;
            if (meshAssetFolder != null) EnsureFolder(meshAssetFolder);
            var combined = new GameObject("Combined meshes");
            combined.transform.SetParent(root.transform, false);
            var batch = combined.AddComponent<CountryMeshBatch>();
            try
            {
                foreach (var group in groups)
                {
                    var mesh = BuildMesh(root.name, group, meshAssetFolder, meshes);
                    var child = new GameObject(group.material.name, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(combined.transform, false);
                    child.layer = group.renderer.gameObject.layer;
                    child.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.GetComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(group.renderer, renderer);
                    renderer.sharedMaterials = new[] { group.material };
                    renderer.ResetLocalBounds();
                }
                batch.sources = sources.ToArray();
                foreach (var renderer in sources)
                {
                    renderer.enabled = false;
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                return sources.Count - groups.Count;
            }
            catch
            {
                Restore(root.transform);
                throw;
            }
        }

        static bool CanCombine(MeshRenderer renderer, MeshFilter filter, Transform root)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || renderer.HasPropertyBlock() ||
                renderer.additionalVertexStreams != null || filter == null || filter.sharedMesh == null ||
                renderer.lightmapIndex is >= 0 and < 65534 || renderer.realtimeLightmapIndex is >= 0 and < 65534) return false;
            for (var t = renderer.transform; t != root.parent; t = t.parent)
                if (!t.gameObject.activeSelf || t.GetComponent<LODGroup>() != null ||
                    t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null) return false;
            var mesh = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            return materials.Length == mesh.subMeshCount && materials.All(m => m != null && m.renderQueue < 2500 && m.GetTag("RenderType", false) != "Transparent") &&
                Enumerable.Range(0, mesh.subMeshCount).All(i => mesh.GetTopology(i) == MeshTopology.Triangles);
        }

        static bool SameState(MeshRenderer a, MeshRenderer b) =>
            a.gameObject.layer == b.gameObject.layer && a.shadowCastingMode == b.shadowCastingMode &&
            a.receiveShadows == b.receiveShadows && a.renderingLayerMask == b.renderingLayerMask &&
            a.lightProbeUsage == b.lightProbeUsage && a.reflectionProbeUsage == b.reflectionProbeUsage &&
            a.probeAnchor == b.probeAnchor && a.lightProbeProxyVolumeOverride == b.lightProbeProxyVolumeOverride &&
            a.motionVectorGenerationMode == b.motionVectorGenerationMode && a.sortingLayerID == b.sortingLayerID &&
            a.sortingOrder == b.sortingOrder && a.rendererPriority == b.rendererPriority &&
            a.allowOcclusionWhenDynamic == b.allowOcclusionWhenDynamic && a.staticShadowCaster == b.staticShadowCaster;

        static Matrix4x4 RelativeMatrix(Transform child, Transform root)
        {
            var matrix = Matrix4x4.identity;
            for (var t = child; t != root; t = t.parent)
                matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
            return matrix;
        }

        static Mesh BuildMesh(string name, Group group, string folder, Dictionary<string, Mesh> meshes)
        {
            var path = folder == null ? null : MeshPath(group, folder);
            if (path != null && meshes.TryGetValue(path, out var cached)) return cached;
            var mesh = new Mesh { name = name + " " + group.material.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(group.parts.ToArray(), true, true, false);
            if (path == null) return mesh;
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                EditorUtility.SetDirty(saved);
                AssetDatabase.SaveAssetIfDirty(saved);
                Object.DestroyImmediate(mesh);
            }
            return meshes[path] = saved;
        }

        static string MeshPath(Group group, string folder)
        {
            var key = new StringBuilder();
            foreach (var part in group.parts)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(part.mesh, out string guid, out long id);
                key.Append(guid).Append(':').Append(string.IsNullOrEmpty(guid) ? part.mesh.GetEntityId().ToString() : id.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(part.subMeshIndex).Append(';');
                for (var i = 0; i < 16; i++) key.Append(part.transform[i].ToString("R", CultureInfo.InvariantCulture)).Append(',');
            }
            return folder + "/" + Hash128.Compute(key.ToString()) + ".asset";
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            EnsureFolder(path[..slash]);
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }
    }
}
