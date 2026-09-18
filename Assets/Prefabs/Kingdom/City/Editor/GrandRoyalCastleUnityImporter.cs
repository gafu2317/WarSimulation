#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WarSimulation.Kingdom.EditorOnly;

namespace WarSimulation.Kingdom.City
{
    public static class GrandRoyalCastleUnityImporter
    {
        const string ManifestPath = "docs/Art/GrandRoyalCastle/unity_export_manifest.json";
        const string ModelRoot = "Assets/Models/Kingdom/City";
        const string PrefabRoot = "Assets/Prefabs/Kingdom/City/Prefabs";
        const string MaterialRoot = "Assets/Prefabs/Kingdom/City/Materials";
        const string PrefabPath = PrefabRoot + "/Royal_Castle.prefab";
        const string ValidationPath = "docs/Art/GrandRoyalCastle/unity_validation.json";

        [Serializable]
        class ExportModel
        {
            public string name;
            public string fbx;
            public int triangles;
            public float[] minimum;
            public float[] maximum;
            public int atlas_size;
        }

        [Serializable]
        class ExportManifest
        {
            public ExportModel[] models;
        }

        [Serializable]
        class ValidationReport
        {
            public string source;
            public string fbx;
            public string prefab;
            public int expectedTriangles;
            public int rendererCount;
            public int materialCount;
            public Vector3 expectedSize;
            public Vector3 prefabSize;
            public float groundHeight;
            public bool usesUrpLit;
            public bool grounded;
            public bool dimensionsMatch;
            public bool oldCastleNameAbsent;
            public string status;
        }

        [MenuItem("WarSim/Kingdom/Import Grand Royal Castle And Rebuild Catalog")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before importing the royal castle.");

            var manifest = ReadManifest();
            foreach (var model in manifest.models)
                ImportModel(model);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report = Validate(manifest.models.Single(model => model.name == "Royal_Castle"));
            Directory.CreateDirectory(Path.GetDirectoryName(ValidationPath));
            File.WriteAllText(ValidationPath, JsonUtility.ToJson(report, true));
            if (report.status != "PASS")
                throw new InvalidOperationException("Grand royal castle validation failed. See " + ValidationPath);

            KingdomAssetCatalogBuilder.Build();
            Debug.Log("Imported GrandRoyalCastle and rebuilt the kingdom asset catalog.");
        }

        static ExportManifest ReadManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("Grand royal castle export manifest was not found.", ManifestPath);
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.models == null || manifest.models.Length != 1 || manifest.models[0].name != "Royal_Castle")
                throw new InvalidOperationException("Grand royal castle export manifest is invalid.");
            return manifest;
        }

        static void ImportModel(ExportModel model)
        {
            ImportTexture(model.name + "_BaseColor.png", true, false, model.atlas_size);
            ImportTexture(model.name + "_Normal.png", false, true, model.atlas_size);
            ImportTexture(model.name + "_MetallicSmoothness.png", false, false, model.atlas_size);

            AssetDatabase.ImportAsset(model.fbx, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(model.fbx) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Unity could not import " + model.fbx);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = true;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx);
            if (source == null) throw new InvalidOperationException("Unity could not load " + model.fbx);
            var material = BuildMaterial(model.name, model.atlas_size);
            var root = new GameObject(model.name);
            try
            {
                var visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (visual == null) throw new InvalidOperationException("Unity could not instantiate " + model.fbx);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    var collider = filter.gameObject.GetComponent<MeshCollider>();
                    if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ImportTexture(string file, bool srgb, bool normal, int size)
        {
            var path = ModelRoot + "/Textures/" + file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Unity could not import " + path);
            importer.sRGBTexture = srgb;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaSource = normal || srgb ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material BuildMaterial(string name, int size)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_BaseColor.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_Normal.png"));
            material.SetFloat("_BumpScale", 1);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_MetallicSmoothness.png"));
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static ValidationReport Validate(ExportModel model)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Royal castle prefab was not created.");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
            var bounds = BoundsOf(renderers);
            var expected = new Vector3(model.maximum[0] - model.minimum[0], model.maximum[2] - model.minimum[2], model.maximum[1] - model.minimum[1]);
            var usesUrp = materials.Length > 0 && materials.All(material => material.shader != null && material.shader.name == "Universal Render Pipeline/Lit");
            var grounded = Mathf.Abs(bounds.min.y) < 0.001f;
            var dimensionsMatch = Approximately(bounds.size, expected);
            return new ValidationReport
            {
                source = "ArtSource/Blender/GrandRoyalCastle.blend",
                fbx = model.fbx,
                prefab = PrefabPath,
                expectedTriangles = model.triangles,
                rendererCount = renderers.Length,
                materialCount = materials.Length,
                expectedSize = expected,
                prefabSize = bounds.size,
                groundHeight = bounds.min.y,
                usesUrpLit = usesUrp,
                grounded = grounded,
                dimensionsMatch = dimensionsMatch,
                oldCastleNameAbsent = !AssetDatabase.FindAssets("t:Prefab Kingdom_Castle", new[] { PrefabRoot }).Any(),
                status = usesUrp && grounded && dimensionsMatch ? "PASS" : "FAIL"
            };
        }

        static Bounds BoundsOf(IEnumerable<Renderer> renderers)
        {
            using var enumerator = renderers.GetEnumerator();
            if (!enumerator.MoveNext()) throw new InvalidOperationException("The castle prefab has no renderers.");
            var bounds = enumerator.Current.bounds;
            while (enumerator.MoveNext()) bounds.Encapsulate(enumerator.Current.bounds);
            return bounds;
        }

        static bool Approximately(Vector3 actual, Vector3 expected)
        {
            return Mathf.Abs(actual.x - expected.x) < 0.01f
                && Mathf.Abs(actual.y - expected.y) < 0.01f
                && Mathf.Abs(actual.z - expected.z) < 0.01f;
        }
    }
}
#endif
