#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public static class NaturalEnvironmentMaterialLibrary
    {
        private const string MaterialDirectory = "Assets/Materials/Environment/Natural";
        private const string TreeTextureDirectory = "Assets/Models/Environment/NaturalTreeVariants/Textures";
        private const string RockTextureDirectory = "Assets/Models/Environment/NaturalRockVariants/Textures";
        private static readonly string[] LeafNames = { "Deep", "Forest", "Fresh", "Olive" };
        private static readonly int[] LeafOrder = { 1, 1, 2, 1, 0, 2, 1, 3, 1, 0 };

        [MenuItem("WarSim/Map/Create Stable Natural Materials")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MaterialDirectory);
            string barkNormalPath = $"{TreeTextureDirectory}/NaturalTree_Bark_Normal.png";
            ConfigureNormalMap(barkNormalPath);
            Upsert("TreeBark", $"{TreeTextureDirectory}/NaturalTree_Bark_Albedo.png", 0.08f, barkNormalPath);
            for (int i = 0; i < LeafNames.Length; i++)
                Upsert($"TreeLeaf{LeafNames[i]}",
                    $"{TreeTextureDirectory}/NaturalTree_Leaf_{LeafNames[i]}_Albedo.png", 0.10f);
            Upsert("RockGranite", $"{RockTextureDirectory}/NaturalRock_Granite_Albedo.png", 0.05f);
            Upsert("RockSlate", $"{RockTextureDirectory}/NaturalRock_Slate_Albedo.png", 0.05f);
            Upsert("RockWarm", $"{RockTextureDirectory}/NaturalRock_Warm_Albedo.png", 0.05f);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyTreeMaterials(GameObject root, int variantIndex)
        {
            Material bark = Load("TreeBark");
            Material leaf = Load($"TreeLeaf{LeafNames[LeafOrder[variantIndex % LeafOrder.Length]]}");
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = renderers[i].name.StartsWith("Foliage", StringComparison.OrdinalIgnoreCase)
                    ? leaf
                    : bark;
        }

        public static void ApplyRockMaterials(GameObject root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = RockMaterial(renderers[i].name);
        }

        private static Material RockMaterial(string objectName)
        {
            if (objectName is "Rock_Main.005" or "Rock_Front" or "Rock_Main.008")
                return Load("RockWarm");
            if (objectName is "Rock_Main" or "Rock_Base" or "Rock_Main.001" or "Rock_Left" or "Rock_Right" or "NaturalRock_11_Trapezoid")
                return Load("RockGranite");
            return Load("RockSlate");
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDirectory}/{name}.mat");
        }

        private static void Upsert(string name, string texturePath, float smoothness, string normalPath = null)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("A supported Lit shader was not found.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) throw new InvalidOperationException($"Natural texture was not found: {texturePath}");
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (!string.IsNullOrEmpty(normalPath))
            {
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal == null) throw new InvalidOperationException($"Natural normal map was not found: {normalPath}");
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.55f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureNormalMap(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
#endif
