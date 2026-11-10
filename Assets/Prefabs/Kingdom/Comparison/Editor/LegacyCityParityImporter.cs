using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class LegacyCityParityImporter
    {
        const string ModelRoot = "Assets/Models/Kingdom/City";
        const string PrefabRoot = "Assets/Prefabs/Kingdom/City";
        const string ManifestPath = ModelRoot + "/legacy_parity_manifest.json";
        [Serializable] class Manifest { public string status; public Model[] models; }
        [Serializable] class Model
        {
            public string name, fbx;
            public int atlas_size;
            public float[] minimum, maximum;
        }
        [Serializable] class CheckedModel
        {
            public string name;
            public Vector3 expectedSize, importedSize;
            public int triangles;
            public bool dimensionsMatch, grounded, textured, hasUV;
        }
        [Serializable] class Report { public string status; public CheckedModel[] models; }

        [MenuItem("WarSim/Country/Import Legacy Parity Models")]
        public static void Import()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest.status != "PASS" || manifest.models.Length != 50)
                throw new InvalidOperationException("The fifty legacy exports are incomplete.");
            Directory.CreateDirectory(PrefabRoot + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var checks = new List<CheckedModel>();
            foreach (var model in manifest.models)
            {
                foreach (var suffix in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
                    ImportTexture(model.name, suffix, model.atlas_size);
                var importer = (ModelImporter)AssetImporter.GetAtPath(model.fbx);
                if (importer == null) throw new InvalidOperationException("Missing FBX: " + model.fbx);
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
                var material = MaterialFor(model.name);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx);
                var root = new GameObject(model.name);
                try
                {
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    visual.name = "Visual";
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    var bounds = CountryComparisonImporter.BoundsOf(root);
                    visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    bounds = CountryComparisonImporter.BoundsOf(root);
                    var expected = new Vector3(model.maximum[0]-model.minimum[0], model.maximum[2]-model.minimum[2], model.maximum[1]-model.minimum[1]);
                    var meshes = visual.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
                    checks.Add(new CheckedModel
                    {
                        name = model.name, expectedSize = expected, importedSize = bounds.size,
                        triangles = meshes.Sum(mesh => mesh.triangles.Length/3),
                        dimensionsMatch = Vector3.Distance(expected, bounds.size) < .03f,
                        grounded = Mathf.Abs(bounds.min.y) < .002f,
                        textured = material.GetTexture("_BaseMap") != null && material.GetTexture("_BumpMap") != null && material.GetTexture("_MetallicGlossMap") != null,
                        hasUV = meshes.All(mesh => mesh.uv.Length == mesh.vertexCount)
                    });
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/Prefabs/" + model.name + ".prefab");
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            var pass = checks.All(c => c.dimensionsMatch && c.grounded && c.textured && c.hasUV && c.triangles > 0);
            Directory.CreateDirectory("Artifacts/CountryComparison/LegacyParity");
            File.WriteAllText("Artifacts/CountryComparison/LegacyParity/unity_import_validation.json", JsonUtility.ToJson(new Report { status = pass ? "PASS" : "FAIL", models = checks.ToArray() }, true));
            if (!pass) throw new InvalidOperationException("A legacy parity model failed import validation.");
            Debug.Log("Legacy parity: fifty models imported with matching dimensions, grounded origins, UVs and three baked texture maps.");
        }

        static void ImportTexture(string name, string suffix, int size)
        {
            var path = ModelRoot + "/Textures/" + name + "_" + suffix + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
            importer.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = suffix == "BaseColor";
            importer.alphaSource = suffix == "MetallicSmoothness" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material MaterialFor(string name)
        {
            var path = PrefabRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            foreach (var pair in new[] { ("_BaseMap", "BaseColor"), ("_BumpMap", "Normal"), ("_MetallicGlossMap", "MetallicSmoothness") })
                material.SetTexture(pair.Item1, AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_" + pair.Item2 + ".png"));
            material.SetFloat("_BumpScale", 1);
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
