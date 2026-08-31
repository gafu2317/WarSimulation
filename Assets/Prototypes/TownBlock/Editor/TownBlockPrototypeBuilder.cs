#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace WarSimulation.Prototypes.TownBlock
{
    public static class TownBlockPrototypeBuilder
    {
        public const string Root = "Assets/Prototypes/TownBlock";
        public const string ScenePath = Root + "/TownBlockReview.unity";
        const string Review = "docs/Art/UnityTownBlock";
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        static readonly List<GameObject> Placed = new List<GameObject>();
        static readonly List<GameObject> Roads = new List<GameObject>();
        static readonly List<GameObject> Buildings = new List<GameObject>();

        [Serializable] public class ExportModel
        {
            public string name;
            public float[] minimum;
            public float[] maximum;
            public int triangles;
            public int atlas_size;
            public string fbx;
        }
        [Serializable] public class ExportManifest { public ExportModel[] models; }
        [Serializable] public class ModelCheck
        {
            public string name;
            public Vector3 dimensions;
            public Vector3 expectedDimensions;
            public float minimumY;
            public float tolerance;
            public int triangles;
        }
        [Serializable] public class ValidationReport
        {
            public string scene;
            public string unityVersion;
            public string renderPipeline;
            public ModelCheck[] models;
            public int prefabCount;
            public int placedPrefabCount;
            public int roadConnections;
            public float minimumHouseSeparation;
            public float minimumBuildingToCarriageway;
            public bool allRootsUnitScale;
            public bool groundContact;
            public bool roadsConnected;
            public bool noBuildingOverlap;
            public bool allMaterialsUrp;
            public int supportedGroundVertices;
            public string[] limitations;
        }

        [MenuItem("WarSim/Prototypes/Build Town Block Review")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building the prototype.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Unsaved scene changes are preserved. Save them before rebuilding this prototype.");
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(Review + "/export_manifest.json"));
            foreach (string directory in new[] { "Materials", "Prefabs" }) Directory.CreateDirectory(Root + "/" + directory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TownBlockReview";
            Prefabs.Clear(); Placed.Clear(); Roads.Clear(); Buildings.Clear();
            foreach (var model in manifest.models) BuildPrefab(model);
            BuildGround();
            BuildStreets();
            foreach (float x in new[] { -16f, 16f })
                foreach (float z in new[] { -4f, 4f }) Place("Paved_Plot", new Vector3(x, 0, z));
            foreach (float x in new[] { -16.5f, 16.5f })
                foreach (float z in new[] { -4.4f, 4.4f })
                    Buildings.Add(Place("Fantasy_House", new Vector3(x, 0.12f, z), x < 0 ? 270 : 90));
            Buildings.Add(Place("Produce_Stall", new Vector3(-1.95f, 0.12f, -1f), 180));
            Buildings.Add(Place("Cloth_Stall", new Vector3(1.95f, 0.12f, -1f), 180));
            foreach (float x in new[] { -2.3f, 2.3f }) Place("Bench", new Vector3(x, 0.12f, 2.2f));
            Place("Crate_Closed", new Vector3(-3.1f, 0.12f, -3f));
            Place("Barrel", new Vector3(3.1f, 0.12f, -3f));
            foreach (float x in new[] { -3.5f, 3.5f })
                foreach (float z in new[] { -3.5f, 3.5f }) Place("Streetlamp", new Vector3(x, 0.12f, z));
            foreach (float x in new[] { -13f, 13f }) Place("Streetlamp", new Vector3(x, 0.12f, 0));
            BuildScaleReference();
            BuildLightingAndCamera();
            Physics.SyncTransforms();
            Validate(manifest);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 2, 0), Quaternion.Euler(38, 218, 0), 29);
            Debug.Log("Town block prototype saved: " + ScenePath);
        }

        static void BuildPrefab(ExportModel model)
        {
            foreach (string suffix in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
            {
                string path = Root + "/Textures/" + model.name + "_" + suffix + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "BaseColor";
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = model.atlas_size;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(model.fbx);
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.importAnimation = false;
            modelImporter.globalScale = 1;
            modelImporter.useFileScale = true;
            modelImporter.bakeAxisConversion = true;
            modelImporter.importNormals = ModelImporterNormals.Import;
            modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
            modelImporter.isReadable = true;
            modelImporter.SaveAndReimport();
            string materialPath = Root + "/Materials/" + model.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", Texture(model.name, "BaseColor"));
            material.SetTexture("_BumpMap", Texture(model.name, "Normal"));
            material.SetTexture("_MetallicGlossMap", Texture(model.name, "MetallicSmoothness"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1);
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            var root = new GameObject(model.name);
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx));
            imported.name = "Visual";
            imported.transform.SetParent(root.transform, false);
            foreach (var renderer in imported.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            foreach (var filter in imported.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            Prefabs[model.name] = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + model.name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static Texture2D Texture(string name, string suffix)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + "_" + suffix + ".png");
        }

        static GameObject Place(string name, Vector3 position, float yaw = 0)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[name]);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            obj.name = name + "_" + Placed.Count.ToString("00");
            Placed.Add(obj);
            return obj;
        }

        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        static bool[] RoadPorts(GameObject obj)
        {
            Physics.SyncTransforms();
            var collider = obj.GetComponentInChildren<MeshCollider>();
            var ports = new bool[4];
            for (int i = 0; i < Directions.Length; i++)
            {
                var d = Directions[i];
                Vector3 p = obj.transform.TransformPoint(new Vector3(d.x * 3.5f, 2f, d.y * 3.5f));
                RaycastHit hit;
                if (!collider.Raycast(new Ray(p, Vector3.down), out hit, 4)) throw new InvalidOperationException("Missing road surface: " + obj.name);
                ports[i] = Mathf.Abs(hit.point.y - obj.transform.position.y) < GeometryTolerance(collider.bounds);
            }
            return ports;
        }

        static void BuildStreets()
        {
            var basePorts = new Dictionary<string, bool[]>();
            foreach (string name in new[] { "Road_Straight", "Road_Corner" })
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[name]);
                basePorts[name] = RoadPorts(probe);
                UnityEngine.Object.DestroyImmediate(probe);
            }
            var tiles = new HashSet<Vector2Int>();
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    if (x != 0 || z != 0) tiles.Add(new Vector2Int(x, z));
            foreach (var tile in tiles.OrderBy(v => v.x).ThenBy(v => v.y))
            {
                bool placed = false;
                foreach (var type in basePorts)
                {
                    for (int turn = 0; turn < 4 && !placed; turn++)
                    {
                        bool fits = true;
                        for (int i = 0; i < 4; i++)
                            if (type.Value[(i - turn + 4) % 4] != tiles.Contains(tile + Directions[i])) fits = false;
                        if (!fits) continue;
                        Roads.Add(Place(type.Key, new Vector3(tile.x * 8, 0, tile.y * 8), turn * 90));
                        placed = true;
                    }
                    if (placed) break;
                }
                if (!placed) throw new InvalidOperationException("No road module for " + tile);
            }
            Place("Paved_Plot", Vector3.zero);
        }

        static Material PlainMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Review Ground";
            ground.transform.position = new Vector3(0, -0.3f, 0);
            ground.transform.localScale = new Vector3(50, 0.24f, 34);
            ground.GetComponent<Renderer>().sharedMaterial = PlainMaterial("ReviewGround", new Color(0.43f, 0.46f, 0.41f));
        }

        static void BuildScaleReference()
        {
            var figure = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            figure.name = "Scale Reference - 1.8m";
            figure.transform.position = new Vector3(-12.8f, 1.02f, 4.4f);
            figure.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            figure.GetComponent<Renderer>().sharedMaterial = PlainMaterial("ScaleReference", new Color(0.17f, 0.52f, 0.63f));
        }

        static void BuildLightingAndCamera()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.67f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.46f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.29f, 0.27f);
            RenderSettings.reflectionIntensity = 0.6f;
            var light = new GameObject("Review Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1, 0.94f, 0.84f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48, -35, 0);
            RenderSettings.sun = light;
            var fill = new GameObject("Review Sky Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.8f;
            fill.color = new Color(0.74f, 0.83f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(55, 145, 0);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.35f, 0.4f, 0.47f));
            RenderSettings.ambientProbe = ambient;
            var camera = new GameObject("Town Block Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(34, 31, 44);
            camera.transform.LookAt(new Vector3(0, 2, 0));
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.28f, 0.33f, 0.36f);
            camera.aspect = 1.6f;
            var urp = camera.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var volume = new GameObject("Review Color Grading").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/ReviewVolume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, Root + "/ReviewVolume.asset");
                var tone = profile.Add<Tonemapping>();
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.AddObjectToAsset(tone, profile);
            }
            volume.sharedProfile = profile;
            ColorAdjustments colorAdjustments;
            if (!profile.TryGet<ColorAdjustments>(out colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>();
                AssetDatabase.AddObjectToAsset(colorAdjustments, profile);
            }
            colorAdjustments.postExposure.Override(0.5f);
            EditorUtility.SetDirty(profile);
            FrameCamera(camera, Placed.SelectMany(o => o.GetComponentsInChildren<Renderer>()));
        }

        static void FrameCamera(Camera camera, IEnumerable<Renderer> renderers)
        {
            var points = new List<Vector3>();
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            points.Add(camera.transform.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            }
            float left = points.Min(p => p.x), right = points.Max(p => p.x);
            float bottom = points.Min(p => p.y), top = points.Max(p => p.y);
            camera.transform.position += camera.transform.TransformVector(new Vector3((left + right) / 2, (bottom + top) / 2, 0));
            camera.orthographicSize = Mathf.Max((right - left) / camera.aspect, top - bottom) * 0.56f;
            var forward = camera.transform.forward;
            var depthAxis = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
            float nearest = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Min(r => camera.transform.InverseTransformPoint(r.bounds.center).z - Vector3.Dot(depthAxis, r.bounds.extents));
            camera.transform.position += forward * (nearest - 2f);
        }

        static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        static float GeometryTolerance(Bounds bounds) { return Mathf.Max(1, bounds.size.magnitude) * Mathf.Pow(2, -20); }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Validate(ExportManifest manifest)
        {
            var checks = new List<ModelCheck>();
            foreach (var model in manifest.models)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[model.name]);
                var bounds = BoundsOf(instance);
                var expected = new Vector3(model.maximum[0] - model.minimum[0], model.maximum[2] - model.minimum[2], model.maximum[1] - model.minimum[1]);
                float tolerance = GeometryTolerance(bounds);
                Require((bounds.size - expected).magnitude <= tolerance, "Imported dimensions differ: " + model.name);
                Require(Mathf.Abs(bounds.min.y - model.minimum[2]) <= tolerance, "Pivot differs: " + model.name);
                Require(instance.transform.localScale == Vector3.one, "Prefab scale differs: " + model.name);
                Require(instance.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"), "Non URP material: " + model.name);
                checks.Add(new ModelCheck { name = model.name, dimensions = bounds.size, expectedDimensions = expected, minimumY = bounds.min.y, tolerance = tolerance, triangles = model.triangles });
                UnityEngine.Object.DestroyImmediate(instance);
            }
            int supportedVertices = 0;
            var supports = Placed.Where(o => o.name.StartsWith("Paved_Plot") || o.name.StartsWith("Road_")).SelectMany(o => o.GetComponentsInChildren<MeshCollider>()).ToArray();
            foreach (var obj in Placed)
            {
                string name = PrefabUtility.GetCorrespondingObjectFromSource(obj).name;
                var source = manifest.models.Single(m => m.name == name);
                var bounds = BoundsOf(obj);
                Require(Mathf.Abs(bounds.min.y - (obj.transform.position.y + source.minimum[2])) <= GeometryTolerance(bounds), "Floating or sunk asset: " + obj.name);
                Require(obj.transform.localScale == Vector3.one, "Instance scale differs: " + obj.name);
                if (!obj.name.StartsWith("Road_") && !obj.name.StartsWith("Paved_Plot"))
                {
                    foreach (var filter in obj.GetComponentsInChildren<MeshFilter>())
                        foreach (var vertex in filter.sharedMesh.vertices)
                        {
                            var point = filter.transform.TransformPoint(vertex);
                            if (Mathf.Abs(point.y - bounds.min.y) > GeometryTolerance(bounds)) continue;
                            bool supported = false;
                            foreach (var support in supports)
                            {
                                RaycastHit hit;
                                if (support.Raycast(new Ray(point + Vector3.up, Vector3.down), out hit, 2) && Mathf.Abs(hit.point.y - point.y) <= GeometryTolerance(bounds))
                                { supported = true; break; }
                            }
                            Require(supported, "Unsupported ground vertex: " + obj.name + " / " + point);
                            supportedVertices++;
                        }
                }
            }
            float houseGap = float.PositiveInfinity;
            for (int i = 0; i < Buildings.Count; i++)
                for (int j = i + 1; j < Buildings.Count; j++)
                {
                    var a = BoundsOf(Buildings[i]); var b = BoundsOf(Buildings[j]);
                    Require(!a.Intersects(b), "Building overlap: " + Buildings[i].name + " / " + Buildings[j].name);
                    if (Buildings[i].name.StartsWith("Fantasy_House") && Buildings[j].name.StartsWith("Fantasy_House"))
                    {
                        float dx = Mathf.Max(0, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
                        float dz = Mathf.Max(0, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
                        houseGap = Mathf.Min(houseGap, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                }
            int connections = 0;
            float clearance = float.PositiveInfinity;
            foreach (var road in Roads)
            {
                bool[] ports = RoadPorts(road);
                for (int i = 0; i < 4; i++)
                {
                    if (!ports[i]) continue;
                    Vector3 delta = road.transform.TransformDirection(new Vector3(Directions[i].x * 8, 0, Directions[i].y * 8));
                    var neighbor = Roads.FirstOrDefault(r => Vector3.Distance(r.transform.position, road.transform.position + delta) < GeometryTolerance(BoundsOf(road)));
                    Require(neighbor != null, "Unconnected road: " + road.name);
                    var neighborCollider = neighbor.GetComponentInChildren<MeshCollider>();
                    var ownCollider = road.GetComponentInChildren<MeshCollider>();
                    var seam = road.transform.position + delta / 2;
                    float epsilon = GeometryTolerance(BoundsOf(road));
                    RaycastHit ownHit, nextHit;
                    bool hitA = ownCollider.Raycast(new Ray(seam - delta.normalized * epsilon + Vector3.up * 2, Vector3.down), out ownHit, 4);
                    bool hitB = neighborCollider.Raycast(new Ray(seam + delta.normalized * epsilon + Vector3.up * 2, Vector3.down), out nextHit, 4);
                    Require(hitA && hitB && Mathf.Abs(ownHit.point.y - nextHit.point.y) <= epsilon && Mathf.Abs(ownHit.point.y) <= epsilon, "Road seam mismatch: " + road.name);
                    connections++;
                }
                foreach (var building in Buildings)
                {
                    var b = BoundsOf(building);
                    var center = road.transform.position;
                    foreach (Vector2Int d in Directions)
                    {
                        Vector3 probe = center + new Vector3(d.x * 3.5f, 2, d.y * 3.5f);
                        RaycastHit hit;
                        if (!road.GetComponentInChildren<MeshCollider>().Raycast(new Ray(probe, Vector3.down), out hit, 4) || Mathf.Abs(hit.point.y) > GeometryTolerance(BoundsOf(road))) continue;
                        var travel = new Bounds(center + new Vector3(d.x, 0, d.y), new Vector3(d.x == 0 ? 4 : 6, 1, d.y == 0 ? 4 : 6));
                        float dx = Mathf.Max(0, Mathf.Max(b.min.x - travel.max.x, travel.min.x - b.max.x));
                        float dz = Mathf.Max(0, Mathf.Max(b.min.z - travel.max.z, travel.min.z - b.max.z));
                        Require(dx > 0 || dz > 0, "Building blocks carriageway: " + building.name);
                        clearance = Mathf.Min(clearance, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                }
            }
            var report = new ValidationReport { scene = ScenePath, unityVersion = Application.unityVersion,
                renderPipeline = GraphicsSettings.currentRenderPipeline.GetType().Name, models = checks.ToArray(),
                prefabCount = Prefabs.Count, placedPrefabCount = Placed.Count, roadConnections = connections / 2,
                minimumHouseSeparation = houseGap, minimumBuildingToCarriageway = clearance,
                allRootsUnitScale = true, groundContact = true, roadsConnected = true, noBuildingOverlap = true, allMaterialsUrp = true,
                supportedGroundVertices = supportedVertices,
                limitations = new[] { "Rendering and placement prototype only", "Static mesh colliders; no NavMesh or gameplay integration", "No target-device performance guarantee" } };
            File.WriteAllText(Review + "/unity_validation.json", JsonUtility.ToJson(report, true));
        }
    }
}
#endif
