using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryLayoutInstaller
    {
        public const string AssetPath = "Assets/Models/Kingdom/CityV2/TownLayout.townlayout";
        const string ScenePath = "Assets/Scenes/Country.unity";
        const string RiverPath = "Assets/Prefabs/Kingdom/Comparison/Meshes/ApprovedTownRiver.asset";
        const string Reports = "Artifacts/CountryLayoutUnity";
        const string Buildings = "Buildings and courtyards";
        const string Furnishings = "Market, yards and street life";
        const string Quay = "Central river, millrace and working quay";
        const string Streets = "Connected streets and entrance paths";

        [Serializable] sealed class Plan
        {
            public Plot[] buildings, props;
            public Road[] accessPaths, secondaryAccessPaths, amenityPaths, roads;
            public Point[] wallOutline;
        }
        [Serializable] sealed class Point { public float x, z; }
        [Serializable] sealed class Road { public string name; public float width; public Point[] points; }
        [Serializable] sealed class Plot
        {
            public string name, zone;
            public float x, y, z, scale, yaw;
        }

        public static bool IsApproved(GameObject city) =>
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(city) == AssetPath;

        [MenuItem("WarSim/Country/Apply Approved Town Layout")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Open the saved Country scene in Edit Mode before applying the town layout.");
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (view == null || view.gameObject.scene != scene) throw new InvalidOperationException("Country view is missing.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (asset == null) throw new InvalidOperationException("Import the approved town layout before applying it.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply approved town layout");
            try
            {
                ApplyTo(view, asset);
                ValidateModern(view);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undo);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Country.unity.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        internal static void ApplyTo(CountryComparisonView view, GameObject asset)
        {
            var (plan, json) = ReadPlan();
            var old = view.cities[1];
            bool replace = !IsApproved(old);
            var city = old;
            if (replace)
            {
                city = (GameObject)PrefabUtility.InstantiatePrefab(asset, old.transform.parent);
                Undo.RegisterCreatedObjectUndo(city, "Place approved town");
                city.name = old.name;
                city.transform.SetLocalPositionAndRotation(old.transform.localPosition, old.transform.localRotation);
                city.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                city.SetActive(old.activeSelf);
                PrefabUtility.RecordPrefabInstancePropertyModifications(city.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(city);
            }
            ValidatePlacements(city.transform, plan);

            Undo.RecordObject(view, "Set modern country");
            view.cities[1] = city;
            view.centers[1] = city.transform.position;
            var castle = plan.buildings.Single(plot => plot.name == "Royal_Castle");
            view.initialFocus = new Vector3(castle.x, 6, castle.z);
            LowerModernRiver(view, plan);
            if (replace) Undo.DestroyObjectImmediate(old);
            CountryFacilityBuilder.BindTargets(view, Reports + "/facilities.json");
            CountryMinimapBuilder.BindModernLayout(json, view);
            if (replace)
            {
                Undo.RecordObject(view.viewCamera, "Focus on the new town");
                Undo.RecordObject(view.viewCamera.transform, "Focus on the new town");
                Undo.RecordObjects(view.cities.Concat(view.hiddenCountryGround).Where(g => g != null).ToArray(),
                    "Show the new town");
                view.ShowInitialView();
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }

        static (Plan plan, string json) ReadPlan()
        {
            var json = AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<TextAsset>()
                .Single(asset => asset.name == "Town layout plan").text;
            return (JsonUtility.FromJson<Plan>(json), json);
        }

        static void LowerModernRiver(CountryComparisonView view, Plan plan)
        {
            var river = view.transform.Find("River beyond city walls/Continuous central river").GetComponent<MeshFilter>();
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(RiverPath);
            if (river.sharedMesh == saved) return;
            var mesh = Object.Instantiate(river.sharedMesh);
            mesh.name = "Continuous river at the modern quay";
            var vertices = mesh.vertices;
            var start = view.centers[0].x + plan.wallOutline.Max(point => point.x) + 2;
            var end = view.centers[1].x + plan.wallOutline.Min(point => point.x) - 2;
            for (int i = 0; i < vertices.Length; ++i)
            {
                var world = river.transform.TransformPoint(vertices[i]);
                world.y = Mathf.Lerp(world.y, .012f, Mathf.InverseLerp(start, end, world.x));
                vertices[i] = river.transform.InverseTransformPoint(world);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (saved == null)
            {
                saved = mesh;
                AssetDatabase.CreateAsset(saved, RiverPath);
            }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            Undo.RecordObject(river, "Connect the river to the new quays");
            river.sharedMesh = saved;
        }

        static void ValidatePlacements(Transform city, Plan plan)
        {
            ValidateGroup(city, Buildings, plan.buildings);
            ValidateGroup(city, Furnishings, plan.props, Quay);
            var renderers = city.GetComponentsInChildren<MeshRenderer>(true);
            var invalid = renderers.FirstOrDefault(renderer => renderer.GetComponent<MeshFilter>().sharedMesh == null);
            if (invalid != null) throw new InvalidDataException("Town mesh or material is invalid: " + invalid.name);
            if (renderers.SelectMany(renderer=>renderer.sharedMaterials).Distinct()
                .Any(mat => mat == null || mat.shader == null || ShaderUtil.ShaderHasError(mat.shader)))
                throw new InvalidDataException("The new town has a missing material or a shader error.");
        }

        static void ValidateGroup(Transform city, string path, Plot[] placements, string excluded = null)
        {
            var group = city.Find(path).Cast<Transform>()
                .Where(zone => zone.name != excluded)
                .SelectMany(zone => zone.Cast<Transform>().Select(item => (zone.name, item)))
                .ToLookup(entry => (entry.name, ModelName(entry.item.name)), entry => entry.item);
            var matches = new HashSet<Transform>();
            foreach (var plot in placements)
            {
                var target = new Vector2(plot.x, plot.z);
                var match = group[(plot.zone, plot.name)].Where(item => !matches.Contains(item))
                    .Select(item =>
                    {
                        var bounds = item.GetComponent<MeshRenderer>().bounds;
                        var center = city.InverseTransformPoint(bounds.center);
                        return (item, distance: Vector2.Distance(target, new Vector2(center.x, center.z)));
                    }).OrderBy(entry => entry.distance).FirstOrDefault();
                if (match.item == null || match.distance > .25f ||
                    Mathf.Abs(Mathf.DeltaAngle(match.item.localEulerAngles.y, plot.yaw)) > .01f)
                    throw new InvalidDataException($"Different town placement: {plot.name} at ({plot.x}, {plot.z}).");
                matches.Add(match.item);
            }
            if (matches.Count != group.Sum(items => items.Count()))
                throw new InvalidDataException("Unexpected town placements in " + path);
        }

        static string ModelName(string instanceName)
        {
            int suffix = instanceName.LastIndexOf(" (", StringComparison.Ordinal);
            return suffix < 0 ? instanceName : instanceName[..suffix];
        }

        [MenuItem("WarSim/Country/Validate Approved Town Layout")]
        public static void ValidateSaved()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            ValidateModern(view);
        }

        internal static void ValidateModern(CountryComparisonView view)
        {
            if (view == null || !IsApproved(view.cities[1]))
                throw new InvalidOperationException("The modern town layout is not placed in the Country scene.");
            var city = view.cities[1].transform;
            var (plan, _) = ReadPlan();
            ValidatePlacements(city, plan);
            var pavement = city.Find(Streets + "/Continuous stone streets and courts");
            var probe = new GameObject("Town paving validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                probe.transform.SetPositionAndRotation(pavement.position, pavement.rotation);
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = pavement.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms();
                foreach (var path in plan.accessPaths.Concat(plan.secondaryAccessPaths).Concat(plan.amenityPaths))
                    for (int i = 1; i < path.points.Length; ++i)
                        foreach (var t in new[] { .05f, .5f, .95f })
                        {
                            var a = path.points[i - 1]; var b = path.points[i];
                            var ray = new Ray(city.TransformPoint(new Vector3(Mathf.Lerp(a.x,b.x,t),1,Mathf.Lerp(a.z,b.z,t))), Vector3.down);
                            if (!collider.Raycast(ray, out var hit, 1.02f) || Mathf.Abs(hit.point.y - .028f) > .003f)
                                throw new InvalidDataException("Discontinuous imported paving: " + path.name);
                        }
            }
            finally { Object.DestroyImmediate(probe); }
            var banners = Object.FindAnyObjectByType<CountryFacilityLayer>(FindObjectsInactive.Include);
            var cities = view.cities.Where(item => item != null).ToArray();
            if (banners == null || banners.Targets.Any(target => target.building == null) ||
                cities.Any(item => banners.Targets.Count(target => target.building.IsChildOf(item.transform)) != CountryFacilityBuilder.FacilityCount))
                throw new InvalidDataException("Country facility banner bindings are incomplete.");
            if (!AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Country/UI/Minimap/ApprovedTownLayout.asset"))
                throw new InvalidDataException("Modern town minimap geometry is missing.");
            Directory.CreateDirectory(Reports);
            var report = $"Approved town layout verified: {plan.buildings.Length} buildings, {plan.props.Length} props, " +
                         $"{plan.roads.Length} streets, {plan.accessPaths.Length + plan.secondaryAccessPaths.Length} entrances, " +
                         $"{banners.Targets.Count} facility banners.";
            File.WriteAllText(Reports + "/Unity validation.txt", report + Environment.NewLine);
            Debug.Log(report);
        }
    }
}
