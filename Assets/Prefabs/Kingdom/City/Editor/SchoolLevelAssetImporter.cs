#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Kingdom.Schools
{
    public static class SchoolLevelAssetImporter
    {
        const string ManifestPath = "docs/Art/SchoolLevelUnity/export_manifest.json";
        const string MaterialRoot = "Assets/Prefabs/Kingdom/City/Materials/SchoolLevels";
        const string PrefabRoot = "Assets/Prefabs/Kingdom/City/Prefabs";
        const string ValidationPath = "docs/Art/SchoolLevelUnity/unity_validation.json";

        [Serializable]
        class ExportModel
        {
            public string name;
            public string asset_type;
            public string fbx;
            public int triangles;
            public float[] minimum;
            public float[] maximum;
        }

        [Serializable]
        class ExportManifest
        {
            public ExportModel[] models;
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
            public int expectedModels;
            public int importedFbx;
            public int createdPrefabs;
            public int createdMaterials;
            public bool allUseUrpLit;
            public bool allGrounded;
            public bool allDimensionsMatch;
            public ModelValidation[] models;
            public string status;
        }

        [MenuItem("WarSim/Kingdom/Import School Level Assets")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before importing school level assets.");

            var manifest = ReadManifest();
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var model in manifest.models)
            {
                ConfigureModelImporter(model.fbx);
                BuildPrefab(model);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report = Validate(manifest);
            Directory.CreateDirectory(Path.GetDirectoryName(ValidationPath));
            File.WriteAllText(ValidationPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            if (report.status != "PASS")
                throw new InvalidOperationException("School level asset validation failed. See " + ValidationPath);
            Debug.Log($"Imported {report.createdPrefabs} school level prefabs. Validation: {ValidationPath}");
        }

        static ExportManifest ReadManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("School level export manifest was not found.", ManifestPath);
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.models == null || manifest.models.Length != 15)
                throw new InvalidOperationException("School level manifest must contain exactly 15 models.");
            return manifest;
        }

        static void ConfigureModelImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Unity could not create a ModelImporter for " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        static void BuildPrefab(ExportModel model)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx);
            if (source == null)
                throw new InvalidOperationException("Unity could not load " + model.fbx);

            var root = new GameObject(model.name);
            try
            {
                var visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (visual == null)
                    throw new InvalidOperationException("Unity could not instantiate " + model.fbx);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ReplaceMaterials(visual);
                RemoveImportedColliders(visual);
                GroundVisual(root, visual);
                AddPlacementCollider(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/" + model.name + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ReplaceMaterials(GameObject visual)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(BuildUrpMaterial).ToArray();
        }

        static Material BuildUrpMaterial(Material imported)
        {
            var sourceName = imported == null ? "Fallback" : imported.name;
            var materialName = "SchoolLevel_" + SanitizeFileName(sourceName.Replace(" (Instance)", string.Empty));
            var path = MaterialRoot + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", ImportedColor(imported));
            material.SetFloat("_Metallic", ImportedFloat(imported, "_Metallic", 0));
            material.SetFloat("_Smoothness", ImportedFloat(imported, "_Glossiness", 0.24f));
            material.SetFloat("_Surface", 0);
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Color ImportedColor(Material material)
        {
            if (material == null) return new Color(0.5f, 0.5f, 0.5f, 1);
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return material.color;
        }

        static float ImportedFloat(Material material, string property, float fallback)
        {
            return material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
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
            visual.transform.localPosition += Vector3.up * -bounds.min.y;
        }

        static void AddPlacementCollider(GameObject root)
        {
            var bounds = RendererBounds(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }

        static ValidationReport Validate(ExportManifest manifest)
        {
            var records = manifest.models.Select(ValidateModel).ToList();
            var report = new ValidationReport
            {
                unityVersion = Application.unityVersion,
                expectedModels = manifest.models.Length,
                importedFbx = manifest.models.Count(model => AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx) != null),
                createdPrefabs = records.Count(record => record.status == "PASS"),
                createdMaterials = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }).Length,
                allUseUrpLit = records.All(record => record.usesUrpLit),
                allGrounded = records.All(record => record.grounded),
                allDimensionsMatch = records.All(record => record.dimensionsMatch),
                models = records.ToArray()
            };
            report.status = report.importedFbx == report.expectedModels
                && report.createdPrefabs == report.expectedModels
                && report.allUseUrpLit
                && report.allGrounded
                && report.allDimensionsMatch ? "PASS" : "FAIL";
            return report;
        }

        static ModelValidation ValidateModel(ExportModel model)
        {
            var prefabPath = PrefabRoot + "/" + model.name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return new ModelValidation { name = model.name, fbx = model.fbx, prefab = prefabPath, status = "MISSING_PREFAB" };

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
            var bounds = RendererBounds(prefab);
            var expected = new Vector3(
                model.maximum[0] - model.minimum[0],
                model.maximum[2] - model.minimum[2],
                model.maximum[1] - model.minimum[1]);
            var usesUrp = materials.Length > 0 && materials.All(material => material.shader != null && material.shader.name == "Universal Render Pipeline/Lit");
            var grounded = Mathf.Abs(bounds.min.y) < 0.001f;
            var dimensionsMatch = Approximately(bounds.size, expected);
            var record = new ModelValidation
            {
                name = model.name,
                fbx = model.fbx,
                prefab = prefabPath,
                rendererCount = renderers.Length,
                materialCount = materials.Length,
                triangles = model.triangles,
                expectedSize = expected,
                prefabSize = bounds.size,
                groundHeight = bounds.min.y,
                usesUrpLit = usesUrp,
                grounded = grounded,
                dimensionsMatch = dimensionsMatch
            };
            record.status = renderers.Length > 0 && usesUrp && grounded && dimensionsMatch ? "PASS" : "FAIL";
            return record;
        }

        static bool Approximately(Vector3 actual, Vector3 expected)
        {
            return Mathf.Abs(actual.x - expected.x) < 0.01f
                && Mathf.Abs(actual.y - expected.y) < 0.01f
                && Mathf.Abs(actual.z - expected.z) < 0.01f;
        }

        static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer found on " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
#endif
