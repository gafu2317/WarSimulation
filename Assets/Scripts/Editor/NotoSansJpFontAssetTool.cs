#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// NotoSansJP TMP SDF is gitignored (large). Prefabs still reference a fixed GUID.
/// Run this once locally after clone so Japanese UI resolves.
/// EditMode AI labels also create an in-memory dynamic font from the TTF when the SDF is absent.
/// </summary>
public static class NotoSansJpFontAssetTool
{
    private const string SourceFontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf";
    private const string FontAssetPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";
    private const string FontAssetGuid = "b23c7cacab96446749d20c4835e528a1";
    private const string FontAssetName = "NotoSansJP-Regular SDF";

    [MenuItem("Tools/War Simulation/Generate NotoSansJP TMP Font Asset")]
    public static void Generate()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        string existingGuid = AssetDatabase.AssetPathToGUID(FontAssetPath);
        if (existing != null && existingGuid == FontAssetGuid)
        {
            Debug.Log($"[NotoSansJpFontAssetTool] Already present: {FontAssetPath} (guid={existingGuid}).");
            return;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
        {
            Debug.LogError($"[NotoSansJpFontAssetTool] Source font missing: {SourceFontPath}");
            return;
        }

        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[NotoSansJpFontAssetTool] TMP Essential Resources are not imported.");
            return;
        }

        if (existing != null || !string.IsNullOrEmpty(existingGuid) || File.Exists(FontAssetPath))
        {
            AssetDatabase.DeleteAsset(FontAssetPath);
        }

        CleanupFontAssetArtifacts();
        EnsureMetaWithFixedGuid();

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);
        if (fontAsset == null)
        {
            CleanupFontAssetArtifacts();
            Debug.LogError(
                "[NotoSansJpFontAssetTool] CreateFontAsset failed. " +
                "Enable Include Font Data on the TTF import settings.");
            return;
        }

        fontAsset.name = FontAssetName;
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        Texture2D atlas = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
            ? fontAsset.atlasTextures[0]
            : null;
        if (atlas != null)
        {
            atlas.name = FontAssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = FontAssetName + " Atlas Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsureMetaWithFixedGuid();
        AssetDatabase.Refresh();

        string guid = AssetDatabase.AssetPathToGUID(FontAssetPath);
        TMP_FontAsset loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (loaded == null || guid != FontAssetGuid)
        {
            CleanupFontAssetArtifacts();
            AssetDatabase.Refresh();
            Debug.LogError(
                $"[NotoSansJpFontAssetTool] Generated asset GUID mismatch. expected={FontAssetGuid} actual={guid}. Cleaned up.");
            return;
        }

        Debug.Log(
            $"[NotoSansJpFontAssetTool] Generated {FontAssetPath} (guid={guid}). " +
            "If prefab shared materials show Missing, reassign fontSharedMaterial from this asset once.");
    }

    private static void CleanupFontAssetArtifacts()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(FontAssetPath) != null || File.Exists(FontAssetPath))
        {
            AssetDatabase.DeleteAsset(FontAssetPath);
        }

        string metaPath = FontAssetPath + ".meta";
        if (File.Exists(FontAssetPath))
        {
            File.Delete(FontAssetPath);
        }

        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    private static void EnsureMetaWithFixedGuid()
    {
        string metaPath = FontAssetPath + ".meta";
        string meta =
            "fileFormatVersion: 2\n" +
            $"guid: {FontAssetGuid}\n" +
            "NativeFormatImporter:\n" +
            "  externalObjects: {}\n" +
            "  mainObjectFileID: 11400000\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";
        File.WriteAllText(metaPath, meta);
    }
}
#endif
