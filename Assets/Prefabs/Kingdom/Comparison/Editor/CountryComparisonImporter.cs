using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryComparisonImporter
    {
        public const string ModelRoot = "Assets/Models/Kingdom/CityV2";
        public const string PrefabRoot = "Assets/Prefabs/Kingdom/CityV2";

        [Serializable] class Manifest { public Model[] assets; }
        [Serializable] class Model { public string name; public string fbx; public string[] materials; }

        static readonly HashSet<string> Cutouts = new()
        {
            "Weathering", "Fern_Frond", "Leaf_Card", "Canopy_Card", "Cypress_Card",
            "Flower_Card_Gold", "Flower_Card_Pink", "Grass_Tuft", "Iron_Grate"
        };

        [MenuItem("WarSim/Country/Import New City Models")]
        public static void Import()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ModelRoot + "/export_manifest.json"));
            Directory.CreateDirectory(PrefabRoot + "/Materials");
            Directory.CreateDirectory(PrefabRoot + "/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CityV2MaterialQuality.Apply();
            var materials = new Dictionary<string, Material>();
            foreach (var name in manifest.assets.SelectMany(a => a.materials).Distinct())
                materials[name] = CityV2MaterialQuality.IsDedicated(name)
                    ? AssetDatabase.LoadAssetAtPath<Material>(PrefabRoot + "/Materials/" + name + ".mat") : ImportMaterial(name);
            foreach (var record in manifest.assets)
            {
                var importer = AssetImporter.GetAtPath(record.fbx) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing FBX: " + record.fbx);
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.bakeAxisConversion = true;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(record.fbx);
                var instance = UnityEngine.Object.Instantiate(source);
                instance.name = record.name;
                try
                {
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => materials[m.name]).ToArray();
                    var bounds = BoundsOf(instance);
                    var root = new GameObject(record.name);
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/Prefabs/" + record.name + ".prefab");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                finally
                {
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            CityV2MaterialQuality.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("Country comparison: imported " + manifest.assets.Length + " new city prefabs.");
        }

        static Material ImportMaterial(string name)
        {
            var path = PrefabRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            var baseMap = Texture(name + "_BaseColor", false, true);
            var normal = Texture(name + "_Normal", true, false);
            var maskPath = ModelRoot + "/Masks/" + name + "_MetallicSmoothness.png";
            var maskImporter = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            maskImporter.sRGBTexture = false;
            maskImporter.maxTextureSize = 1024;
            maskImporter.SaveAndReimport();
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", .24f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            material.SetFloat("_Smoothness", name.StartsWith("Window_") ? .7f : .65f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", Cutouts.Contains(name) ? 1 : 0);
            material.SetFloat("_Cutoff", .42f);
            if (Cutouts.Contains(name))
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (name.StartsWith("Window_") || name.StartsWith("Liquid_"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", baseMap);
                material.SetColor("_EmissionColor", Color.white * .08f);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D Texture(string file, bool normal, bool srgb)
        {
            var path = ModelRoot + "/Textures/" + file + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = !normal;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
