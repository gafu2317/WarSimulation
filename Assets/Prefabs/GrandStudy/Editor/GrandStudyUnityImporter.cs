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
        const string LightOakTexturePath = TextureRoot + "/grandstudy_light_oak_base.png";
        const string WalnutGrainTexturePath = TextureRoot + "/grandstudy_walnut_grain.png";
        const string RugLargeTexturePath = TextureRoot + "/grandstudy_rug_large.png";
        const string RugRunnerTexturePath = TextureRoot + "/grandstudy_rug_runner.png";
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
        class ExportMaterial
        {
            public string name;
            public float[] base_color;
            public float metallic;
            public float roughness;
            public float alpha;
            public string base_texture;
            public string mask_texture;
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
            public ExportMaterial[] materials;
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
            ConfigureTextures(manifest);
            var materials = MaterialDefinitions(manifest);

            foreach (var model in manifest.models)
            {
                ConfigureModelImporter(model.fbx);
                BuildObjectPrefab(model, materials);
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

        [MenuItem("WarSim/Grand Study/Import Objects And Build Room Prefab")]
        public static void BuildPrefabsOnly()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before importing the grand study.");

            var manifest = ReadManifest();
            Directory.CreateDirectory(MaterialRoot);
            Directory.CreateDirectory(PrefabRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextures(manifest);
            var materials = MaterialDefinitions(manifest);

            foreach (var model in manifest.models)
            {
                ConfigureModelImporter(model.fbx);
                BuildObjectPrefab(model, materials);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildRoomPrefab(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Grand study prefabs imported: {manifest.models.Length} object prefabs, {manifest.room.instances.Length} room instances.");
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

        static void ConfigureTextures(ExportManifest manifest)
        {
            var colorTextures = new[]
            {
                TextureRoot + "/landscape.png",
                TextureRoot + "/portrait.png",
                LightOakTexturePath,
                WalnutGrainTexturePath,
                RugLargeTexturePath,
                RugRunnerTexturePath,
            }
                .Concat(manifest.materials.Where(material => !string.IsNullOrEmpty(material.base_texture)).Select(material => ToAssetPath(material.base_texture)))
                .Distinct();
            foreach (var path in colorTextures)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Unity could not import " + path);
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = path.EndsWith("landscape.png", StringComparison.Ordinal)
                    || path.EndsWith("portrait.png", StringComparison.Ordinal)
                    ? TextureWrapMode.Clamp
                    : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            foreach (var path in manifest.materials
                         .Where(material => !string.IsNullOrEmpty(material.mask_texture))
                         .Select(material => ToAssetPath(material.mask_texture))
                         .Distinct())
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Unity could not import " + path);
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
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
            importer.isReadable = path.Contains("Rug_");
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }

        static IReadOnlyDictionary<string, ExportMaterial> MaterialDefinitions(ExportManifest manifest)
        {
            if (manifest.materials == null || manifest.materials.Length == 0)
                throw new InvalidOperationException("Grand study manifest has no Blender material definitions.");
            return manifest.materials.ToDictionary(material => material.name);
        }

        static void BuildObjectPrefab(ExportModel model, IReadOnlyDictionary<string, ExportMaterial> materials)
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
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ReplaceMaterials(visual, model.name, materials);
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

        static void ReplaceMaterials(GameObject visual, string modelName, IReadOnlyDictionary<string, ExportMaterial> materials)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(material => BuildUrpMaterial(material, modelName, materials))
                    .ToArray();
                if (modelName == "Ceiling_Coffer_2m")
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Material BuildUrpMaterial(Material imported, string modelName, IReadOnlyDictionary<string, ExportMaterial> materials)
        {
            if (imported == null)
                throw new InvalidOperationException("Grand study renderer has an empty material slot.");
            var sourceName = imported.name.Replace(" (Instance)", string.Empty);
            if (!materials.TryGetValue(sourceName, out var definition) || definition.base_color == null || definition.base_color.Length < 3)
                throw new InvalidOperationException("Blender material definition is missing: " + sourceName);
            var isPictureBrass = sourceName == "Brass"
                && (modelName.StartsWith("Picture_", StringComparison.Ordinal) || modelName == "Photo_Frame");
            var isFloor = modelName == "Floor_2m";
            var isRug = modelName == "Rug_Large" || modelName == "Rug_Runner";
            var tint = FurnitureTint(modelName, sourceName);
            var variantSuffix = modelName == "Sofa_ThreeSeat" ? "_Sofa" : isPictureBrass ? "_Picture" : string.Empty;
            if (tint != Color.white || isRug)
                variantSuffix = "_" + SanitizeFileName(modelName);
            if (isFloor)
                variantSuffix = "_Floor_2m";
            var materialName = "GrandStudy_" + SanitizeFileName(sourceName) + variantSuffix;
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
            var lower = sourceName.ToLowerInvariant();
            var alpha = definition.base_color.Length > 3 ? definition.base_color[3] : definition.alpha;
            material.SetColor("_BaseColor", new Color(definition.base_color[0], definition.base_color[1], definition.base_color[2], alpha));
            material.SetFloat("_Metallic", Mathf.Clamp01(definition.metallic));
            material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(definition.roughness));
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Cull", 0);
            material.renderQueue = -1;
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.DisableKeyword("_SPECULAR_SETUP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EnvironmentReflections", 1);
            material.SetFloat("_SpecularHighlights", 1);
            material.SetFloat("_WorkflowMode", 1);
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_MainTex", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_SpecGlossMap", null);
            material.enableInstancing = true;

            if (!isPictureBrass && !string.IsNullOrEmpty(definition.base_texture))
            {
                var baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ToAssetPath(definition.base_texture));
                if (baseTexture == null)
                    throw new InvalidOperationException("Grand study base texture is missing: " + definition.base_texture);
                material.SetTexture("_BaseMap", baseTexture);
                material.SetColor("_BaseColor", new Color(
                    definition.base_color[0],
                    definition.base_color[1],
                    definition.base_color[2],
                    alpha));
            }
            if (!isPictureBrass && !string.IsNullOrEmpty(definition.mask_texture))
            {
                var maskTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ToAssetPath(definition.mask_texture));
                if (maskTexture == null)
                    throw new InvalidOperationException("Grand study mask texture is missing: " + definition.mask_texture);
                material.SetTexture("_MetallicGlossMap", maskTexture);
                material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            var isScaleStableDark = lower == "dark wood"
                || lower == "walnut"
                || lower == "leather"
                || lower == "back leather";
            var isAgedBronze = lower == "aged bronze";
            var useSimpleLit = (isScaleStableDark && !string.IsNullOrEmpty(definition.base_texture))
                || isAgedBronze
                || isFloor
                || isRug && (lower == "rug" || lower == "rug dark" || lower == "cream");
            if (useSimpleLit)
            {
                var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (simpleLit == null)
                    throw new InvalidOperationException("Universal Render Pipeline/Simple Lit shader is unavailable.");
                material.shader = simpleLit;
                material.SetColor("_BaseColor", isAgedBronze ? new Color(0.28f, 0.16f, 0.06f, 1f) : Color.white);
                material.SetFloat("_Smoothness", 0);
                material.SetFloat("_SpecularHighlights", 0);
                material.SetFloat("_EnvironmentReflections", 0);
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if ((lower == "walnut" || lower == "dark wood") && !isFloor)
            {
                var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(WalnutGrainTexturePath);
                if (grain == null)
                    throw new InvalidOperationException("Grand study walnut grain is missing: " + WalnutGrainTexturePath);
                material.SetTexture("_BaseMap", grain);
                material.SetColor("_BaseColor", lower == "dark wood" ? new Color(0.78f, 0.7f, 0.62f, 1f) : Color.white);
            }
            if (isFloor)
            {
                if (lower == "oak seam")
                {
                    material.SetTexture("_BaseMap", null);
                    material.SetColor("_BaseColor", new Color(0.18f, 0.13f, 0.08f, 1f));
                }
                else
                {
                    var oak = AssetDatabase.LoadAssetAtPath<Texture2D>(LightOakTexturePath);
                    if (oak == null)
                        throw new InvalidOperationException("Grand study light oak texture is missing: " + LightOakTexturePath);
                    material.SetTexture("_BaseMap", oak);
                    material.SetColor("_BaseColor", Color.white);
                }
            }
            if (isRug && lower == "rug")
            {
                var oak = AssetDatabase.LoadAssetAtPath<Texture2D>(LightOakTexturePath);
                if (oak == null)
                    throw new InvalidOperationException("Grand study light oak texture is missing: " + LightOakTexturePath);
                material.SetTexture("_BaseMap", oak);
                material.SetColor("_BaseColor", new Color(0.32f, 0.16f, 0.09f, 1f));
            }
            else if (isRug && lower == "rug dark")
            {
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", new Color(0.1f, 0.045f, 0.02f, 1f));
            }
            else if (isRug && lower == "cream")
            {
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", new Color(0.62f, 0.5f, 0.34f, 1f));
            }
            if (lower == "brass" && !isPictureBrass)
            {
                var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (simpleLit == null)
                    throw new InvalidOperationException("Universal Render Pipeline/Simple Lit shader is unavailable.");
                material.shader = simpleLit;
                material.SetTexture("_BaseMap", null);
                material.SetTexture("_MetallicGlossMap", null);
                material.SetColor("_BaseColor", new Color(0.72f, 0.5f, 0.18f, 1f));
                material.SetFloat("_Smoothness", 0.35f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (lower == "wallpaper")
            {
                material.SetColor("_BaseColor", new Color(0.22f, 0.34f, 0.30f, 1f));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.04f);
            }
            if ((modelName == "Chair_Red" || modelName == "Chair_Arms") && lower == "red velvet")
            {
                var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (simpleLit == null)
                    throw new InvalidOperationException("Universal Render Pipeline/Simple Lit shader is unavailable.");
                material.shader = simpleLit;
                material.SetTexture("_BaseMap", null);
                material.SetColor("_BaseColor", new Color(0.1f, 0.2f, 0.16f, 1f));
                material.SetFloat("_Smoothness", 0.18f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (tint != Color.white)
                material.SetColor("_BaseColor", tint);

            if (isPictureBrass)
            {
                material.SetColor("_BaseColor", new Color(0.12f, 0.035f, 0.004f, alpha));
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", 0.1f);
                material.SetFloat("_WorkflowMode", 0);
                material.SetColor("_SpecColor", Color.black);
                material.SetFloat("_SpecularHighlights", 0);
                material.EnableKeyword("_SPECULAR_SETUP");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }

            var isArtwork = lower.Contains("landscape")
                || lower.Contains("portrait")
                || modelName == "Photo_Frame" && lower.Contains("oil on canvas");
            if (isArtwork)
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

        static Color FurnitureTint(string modelName, string sourceName)
        {
            var lower = sourceName.ToLowerInvariant();
            if (modelName.StartsWith("Wall_", StringComparison.Ordinal) && lower == "cream")
                return new Color(0.34f, 0.46f, 0.4f, 1f);
            if ((modelName == "Chair_Red" || modelName == "Chair_Arms") && lower == "red velvet")
                return new Color(0.1f, 0.2f, 0.16f, 1f);
            if (lower != "dark wood" && lower != "walnut")
                return Color.white;
            switch (modelName)
            {
                case "Meeting_Table": return new Color(0.92f, 0.86f, 0.78f, 1f);
                case "Coffee_Table": return new Color(0.88f, 0.82f, 0.74f, 1f);
                case "Telephone": return new Color(0.62f, 0.56f, 0.5f, 1f);
                case "Desk_Pedestal": return new Color(1.06f, 0.98f, 0.9f, 1f);
                case "Sideboard": return new Color(0.96f, 0.9f, 0.82f, 1f);
                default: return Color.white;
            }
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
            var modelsByName = manifest.models.ToDictionary(model => model.name);
            var prefabsByName = manifest.models.ToDictionary(model => model.name, model => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + model.name + ".prefab"));
            var root = new GameObject("GrandStudyRoom");
            try
            {
                var categoryRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var placement in manifest.room.instances)
                {
                    if (!modelsByName.TryGetValue(placement.asset, out var model) ||
                        !prefabsByName.TryGetValue(placement.asset, out var prefab) ||
                        prefab == null)
                        throw new InvalidOperationException("Room references missing prefab: " + placement.asset);
                    var category = model.asset_type;
                    if (!categoryRoots.TryGetValue(category, out var categoryRoot))
                    {
                        categoryRoot = new GameObject(category).transform;
                        categoryRoot.SetParent(root.transform, false);
                        categoryRoots.Add(category, categoryRoot);
                    }
                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    instance.name = placement.name;
                    instance.transform.SetParent(categoryRoot, false);
                    instance.transform.localPosition = MapGroundedPrefabPosition(placement, model);
                    instance.transform.localRotation = Quaternion.Euler(0, -placement.rotation_z * Mathf.Rad2Deg, 0);
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
            var usesUrp = materials.Length > 0 && materials.All(material =>
                material.shader != null
                && (material.shader.name == "Universal Render Pipeline/Lit"
                    || material.shader.name == "Universal Render Pipeline/Simple Lit"));
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
            return new Vector3(source[0], source[2], source[1]);
        }

        static Vector3 MapGroundedPrefabPosition(RoomInstance placement, ExportModel model)
        {
            var position = MapPosition(placement.location);
            position.y += model.minimum[2] * placement.scale[2];
            return position;
        }
    }
}
#endif
