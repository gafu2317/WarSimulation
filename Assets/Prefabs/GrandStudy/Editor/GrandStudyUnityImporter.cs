#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarSimulation.GrandStudy
{
    public static class GrandStudyUnityImporter
    {
        const string ManifestPath = "docs/Art/GrandStudy/unity_export_manifest.json";
        const string ModelRoot = "Assets/Models/GrandStudy/Models";
        const string TextureRoot = "Assets/Models/GrandStudy/Textures";
        const string MaterialRoot = "Assets/Prefabs/GrandStudy/Materials";
        const string PrefabRoot = "Assets/Prefabs/GrandStudy/Objects";
        const string RoomPrefabPath = "Assets/Prefabs/GrandStudy/GrandStudyRoom.prefab";
        const string ScenePath = "Assets/Scenes/GrandStudyRoom.unity";
        const string ValidationPath = "docs/Art/GrandStudyUnity/unity_validation.json";

        [Serializable]
        class ExportModel
        {
            public string name;
            public string asset_type;
            public string fbx;
            public int parts;
            public int triangles;
            public float[] minimum;
            public float[] maximum;
            public string[] materials;
        }

        [Serializable]
        class RoomInstance
        {
            public string name;
            public string asset;
            public float[] location;
            public float rotation_z;
            public float[] scale;
        }

        [Serializable]
        class CameraData
        {
            public float[] location;
            public float[] target;
            public float lens;
            public float sensor_width;
            public float sensor_height;
            public string sensor_fit;
        }

        [Serializable]
        class RoomData
        {
            public RoomInstance[] instances;
            public CameraData camera;
        }

        [Serializable]
        class ExportManifest
        {
            public string blender;
            public string source;
            public ExportModel[] models;
            public RoomData room;
        }

        [Serializable]
        class ModelValidation
        {
            public string name;
            public string fbx;
            public string prefab;
            public int rendererCount;
            public int materialCount;
            public int triangles;
            public Vector3 expectedSize;
            public Vector3 prefabSize;
            public float groundHeight;
            public bool usesUrpLit;
            public bool grounded;
            public bool dimensionsMatch;
            public string status;
        }

        [Serializable]
        class ValidationReport
        {
            public string unityVersion;
            public string scene;
            public string roomPrefab;
            public int expectedModels;
            public int importedFbx;
            public int createdPrefabs;
            public int roomInstances;
            public bool allUseUrpLit;
            public bool allGrounded;
            public bool allDimensionsMatch;
            public bool roomPrefabConnections;
            public bool cameraConfigured;
            public ModelValidation[] models;
            public string status;
        }

        [MenuItem("WarSim/Grand Study/Import Objects And Build Room")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before importing the grand study.");

            var manifest = ReadManifest();
            Directory.CreateDirectory(MaterialRoot);
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(ValidationPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextures();

            foreach (var model in manifest.models)
            {
                ConfigureModelImporter(model.fbx);
                BuildObjectPrefab(model);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var roomPrefab = BuildRoomPrefab(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildScene(manifest, roomPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var report = Validate(manifest);
            File.WriteAllText(ValidationPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            if (report.status != "PASS")
                throw new InvalidOperationException("Grand study Unity validation failed. See " + ValidationPath);
            Debug.Log($"Grand study imported: {report.createdPrefabs} object prefabs, {report.roomInstances} room instances.");
        }

        static ExportManifest ReadManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("Grand study export manifest was not found.", ManifestPath);
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.models == null || manifest.models.Length != 73)
                throw new InvalidOperationException("Grand study manifest must contain exactly 73 models.");
            if (manifest.room == null || manifest.room.instances == null || manifest.room.instances.Length == 0)
                throw new InvalidOperationException("Grand study manifest has no room instances.");
            return manifest;
        }

        static void ConfigureTextures()
        {
            foreach (var file in new[] { "landscape.png", "portrait.png" })
            {
                var path = TextureRoot + "/" + file;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Unity could not import " + path);
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModelImporter(string manifestPath)
        {
            var path = ToAssetPath(manifestPath);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Unity could not create a ModelImporter for " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.SaveAndReimport();
        }

        static void BuildObjectPrefab(ExportModel model)
        {
            var fbxPath = ToAssetPath(model.fbx);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (source == null) throw new InvalidOperationException("Unity could not load " + fbxPath);

            var root = new GameObject(model.name);
            try
            {
                var visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (visual == null) throw new InvalidOperationException("Unity could not instantiate " + fbxPath);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(1f, 1f, -1f);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ReplaceMaterials(visual, model.name);
                RemoveImportedColliders(visual);
                GroundVisual(root, visual);
                AddPlacementCollider(root, visual, model.asset_type);
                var prefabPath = PrefabRoot + "/" + model.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ReplaceMaterials(GameObject visual, string modelName)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(material => BuildUrpMaterial(material, modelName))
                    .ToArray();
            }
        }

        static Material BuildUrpMaterial(Material imported, string modelName)
        {
            var sourceName = imported == null ? "Fallback" : imported.name.Replace(" (Instance)", string.Empty);
            var sofaSuffix = modelName == "Sofa_ThreeSeat" ? "_Sofa" : string.Empty;
            var materialName = "GrandStudy_" + SanitizeFileName(sourceName) + sofaSuffix;
            var path = MaterialRoot + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", ImportedColor(imported, sourceName));
            material.SetFloat("_Metallic", IsMetal(sourceName) ? 0.78f : 0f);
            var lower = sourceName.ToLowerInvariant();
            material.SetFloat("_Smoothness", IsMetal(sourceName) ? 0.72f : IsGlossy(sourceName) ? 0.48f : lower.Contains("cream") ? 0.12f : 0.24f);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_Cull", modelName == "Sofa_ThreeSeat" ? 0 : 2);
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.enableInstancing = true;

            if (lower.Contains("landscape") || lower.Contains("portrait"))
            {
                var textureName = lower.Contains("landscape") ? "landscape.png" : "portrait.png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/" + textureName);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
            }
            if (lower.Contains("sheer") || lower.Contains("window glass"))
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_Alpha", lower.Contains("sheer") ? 0.38f : 0.22f);
                material.SetColor("_BaseColor", new Color(0.76f, 0.78f, 0.72f, lower.Contains("sheer") ? 0.38f : 0.22f));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Color ImportedColor(Material material, string sourceName)
        {
            var fallback = FallbackColor(sourceName);
            if (material == null) return fallback;
            var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.GetColor("_Color") : material.color;
            return color.maxColorComponent < 0.015f ? fallback : new Color(color.r, color.g, color.b, 1);
        }

        static Color FallbackColor(string name)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("walnut") || lower.Contains("dark wood") || lower.Contains("brown book")) return new Color(0.12f, 0.045f, 0.022f, 1);
            if (lower.Contains("leather")) return new Color(0.08f, 0.025f, 0.014f, 1);
            if (lower.Contains("velvet")) return new Color(0.30f, 0.018f, 0.028f, 1);
            if (lower.Contains("brass") || lower.Contains("bronze") || lower.Contains("gold")) return new Color(0.47f, 0.25f, 0.07f, 1);
            if (lower.Contains("porcelain") || lower.Contains("cream") || lower.Contains("paper")) return new Color(0.72f, 0.68f, 0.54f, 1);
            if (lower.Contains("green") || lower.Contains("leaf") || lower.Contains("plant")) return new Color(0.06f, 0.18f, 0.025f, 1);
            if (lower.Contains("black")) return new Color(0.012f, 0.010f, 0.008f, 1);
            return new Color(0.42f, 0.42f, 0.40f, 1);
        }

        static bool IsMetal(string name)
        {
            var lower = name.ToLowerInvariant();
            return lower.Contains("brass") || lower.Contains("bronze") || lower.Contains("gold") || lower.Contains("iron") || lower.Contains("metal");
        }

        static bool IsGlossy(string name)
        {
            var lower = name.ToLowerInvariant();
            return lower.Contains("leather") || lower.Contains("porcelain") || lower.Contains("glass") || lower.Contains("lacquer");
        }

        static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToHashSet();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }

        static void RemoveImportedColliders(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
        }

        static void GroundVisual(GameObject root, GameObject visual)
        {
            var bounds = RendererBounds(root);
            visual.transform.localPosition += Vector3.down * bounds.min.y;
        }

        static void AddPlacementCollider(GameObject root, GameObject visual, string assetType)
        {
            if (assetType == "Architecture" || assetType == "Furniture")
            {
                var bounds = RendererBounds(root);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
            }
        }

        static GameObject BuildRoomPrefab(ExportManifest manifest)
        {
            var byName = manifest.models.ToDictionary(model => model.name, model => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + model.name + ".prefab"));
            var root = new GameObject("GrandStudyRoom");
            try
            {
                var categoryRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var placement in manifest.room.instances)
                {
                    if (!byName.TryGetValue(placement.asset, out var prefab) || prefab == null)
                        throw new InvalidOperationException("Room references missing prefab: " + placement.asset);
                    var category = manifest.models.First(model => model.name == placement.asset).asset_type;
                    if (!categoryRoots.TryGetValue(category, out var categoryRoot))
                    {
                        categoryRoot = new GameObject(category).transform;
                        categoryRoot.SetParent(root.transform, false);
                        categoryRoots.Add(category, categoryRoot);
                    }
                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    instance.name = placement.name;
                    instance.transform.SetParent(categoryRoot, false);
                    instance.transform.localPosition = MapPosition(placement.location);
                    instance.transform.localRotation = Quaternion.Euler(0, placement.rotation_z * Mathf.Rad2Deg, 0);
                    instance.transform.localScale = new Vector3(placement.scale[0], placement.scale[2], placement.scale[1]);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, RoomPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void BuildScene(ExportManifest manifest, GameObject roomPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GrandStudyRoom";
            var room = PrefabUtility.InstantiatePrefab(roomPrefab) as GameObject;
            room.name = "GrandStudyRoom";

            var cameraObject = new GameObject("GrandStudy Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var cameraData = manifest.room.camera;
            cameraObject.transform.position = MapPosition(cameraData.location);
            cameraObject.transform.LookAt(MapPosition(cameraData.target));
            var sensorHeight = cameraData.sensor_height > 0f ? cameraData.sensor_height : 24f;
            camera.fieldOfView = 2f * Mathf.Atan(sensorHeight / (2f * Mathf.Max(cameraData.lens, 1f))) * Mathf.Rad2Deg;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 100f;
            camera.tag = "MainCamera";
            camera.allowHDR = true;

            var keyObject = new GameObject("GrandStudy Key Light");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.color = new Color(1f, 0.91f, 0.78f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.7f;
            keyObject.transform.rotation = Quaternion.Euler(42, -28, 0);

            var fillObject = new GameObject("GrandStudy Window Fill");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 10f;
            fill.intensity = 22f;
            fill.color = new Color(0.78f, 0.86f, 1f);
            fill.shadows = LightShadows.Soft;
            fillObject.transform.position = new Vector3(-2.5f, 2.4f, 1.2f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.34f);
            RenderSettings.reflectionIntensity = 0.45f;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static ValidationReport Validate(ExportManifest manifest)
        {
            var records = new List<ModelValidation>();
            foreach (var model in manifest.models)
            {
                var prefabPath = PrefabRoot + "/" + model.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                records.Add(ValidateModel(model, prefab, prefabPath));
            }
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
            var report = new ValidationReport
            {
                unityVersion = Application.unityVersion,
                scene = ScenePath,
                roomPrefab = RoomPrefabPath,
                expectedModels = manifest.models.Length,
                importedFbx = manifest.models.Count(model => AssetDatabase.LoadAssetAtPath<GameObject>(ToAssetPath(model.fbx)) != null),
                createdPrefabs = records.Count(record => record.status == "PASS"),
                roomInstances = manifest.room.instances.Length,
                allUseUrpLit = records.All(record => record.usesUrpLit),
                allGrounded = records.All(record => record.grounded),
                allDimensionsMatch = records.All(record => record.dimensionsMatch),
                roomPrefabConnections = roomPrefab != null && roomPrefab.GetComponentsInChildren<Transform>(true).Count() > manifest.room.instances.Length,
                cameraConfigured = scene.IsValid() && scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<Camera>() != null),
                models = records.ToArray(),
            };
            report.status = report.importedFbx == report.expectedModels && report.createdPrefabs == report.expectedModels && report.allUseUrpLit && report.allGrounded && report.allDimensionsMatch && report.roomPrefabConnections && report.cameraConfigured ? "PASS" : "FAIL";
            return report;
        }

        static ModelValidation ValidateModel(ExportModel model, GameObject prefab, string prefabPath)
        {
            if (prefab == null)
                return new ModelValidation { name = model.name, fbx = model.fbx, prefab = prefabPath, status = "FAIL" };
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
            var bounds = RendererBounds(prefab);
            var expected = new Vector3(model.maximum[0] - model.minimum[0], model.maximum[2] - model.minimum[2], model.maximum[1] - model.minimum[1]);
            var usesUrp = materials.Length > 0 && materials.All(material => material.shader != null && material.shader.name == "Universal Render Pipeline/Lit");
            var grounded = Mathf.Abs(bounds.min.y) < 0.01f;
            var dimensionsMatch = Approximately(bounds.size, expected, 0.03f);
            return new ModelValidation
            {
                name = model.name,
                fbx = model.fbx,
                prefab = prefabPath,
                rendererCount = renderers.Length,
                materialCount = materials.Length,
                triangles = renderers.Sum(renderer => renderer is SkinnedMeshRenderer ? 0 : SubmeshTriangleCount(renderer.GetComponent<MeshFilter>()?.sharedMesh)),
                expectedSize = expected,
                prefabSize = bounds.size,
                groundHeight = bounds.min.y,
                usesUrpLit = usesUrp,
                grounded = grounded,
                dimensionsMatch = dimensionsMatch,
                status = renderers.Length > 0 && usesUrp && grounded && dimensionsMatch ? "PASS" : "FAIL",
            };
        }

        static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers found under " + root.name);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        static int SubmeshTriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            var triangles = 0;
            for (var index = 0; index < mesh.subMeshCount; index++)
                triangles += (int)mesh.GetIndexCount(index) / 3;
            return triangles;
        }

        static bool Approximately(Vector3 actual, Vector3 expected, float tolerance)
        {
            return Mathf.Abs(actual.x - expected.x) <= tolerance && Mathf.Abs(actual.y - expected.y) <= tolerance && Mathf.Abs(actual.z - expected.z) <= tolerance;
        }

        static string ToAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) ? path : path.Replace('\\', '/');
        }

        static Vector3 MapPosition(float[] source)
        {
            return new Vector3(source[0], source[2], -source[1]);
        }
    }
}
#endif
