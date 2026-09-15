using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SkillVfxAtlasBuilder
{
    [MenuItem("Tools/Combat/Rebuild Skill VFX Atlas")]
    public static void Build()
    {
        const string folder = "Assets/Resources/Combat/Vfx/Art/";
        var names = Enum.GetNames(typeof(SkillVfxShape));
        var sources = new Texture2D[names.Length];
        var packed = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                string source = "Assets/Art/SkillVfx/Source/" + names[i] + ".png";
                sources[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!sources[i].LoadImage(File.ReadAllBytes(source))) throw new InvalidOperationException(source);
                Color32[] pixels = sources[i].GetPixels32();
                if (pixels[0].a > 0 || pixels[^1].a > 0) throw new InvalidOperationException("Transparent corners required: " + source);
            }
            // Packing only: original silhouettes and alpha are preserved, with transparent gutters.
            Rect[] regions = packed.PackTextures(sources, 12, 2048, false);
            File.WriteAllBytes(folder + "Atlas.png", packed.EncodeToPNG());
            AssetDatabase.ImportAsset(folder + "Atlas.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "Atlas.png");
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            string path = folder + "Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("WarSimulation/SkillAtlas"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Atlas.png");
            material.SetFloat("_AlphaFloor", .06f);
            material.SetFloat("_AlphaCeiling", .82f);
            EditorUtility.SetDirty(material);
            path = folder + "Atlas.asset";
            var atlas = AssetDatabase.LoadAssetAtPath<SkillVfxAtlas>(path);
            if (atlas == null) { atlas = ScriptableObject.CreateInstance<SkillVfxAtlas>(); AssetDatabase.CreateAsset(atlas, path); }
            atlas.Material = material; atlas.Regions = regions;
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            Debug.Log($"VFX atlas: {packed.width}×{packed.height}, {regions.Length} shapes");
        }
        finally
        {
            foreach (var source in sources) if (source != null) UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(packed);
        }
    }
}
