using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

internal static class CountryButtonSpriteImporter
{
    internal static readonly string[] SamplingTextures = {
        "Assets/Images/UI/Country/Controls/GameTab-v1.png",
        "Assets/Images/UI/Country/Roster/RosterParts-v1.png",
        "Assets/Images/UI/Country/Buttons/CommandMotifParts-v1.png",
        "Assets/Images/UI/Country/Buttons/TrainingPennantParts-v1.png",
        "Assets/Images/UI/Country/Buttons/ConstructionSignParts-v1.png",
        "Assets/Images/UI/Country/Buttons/ExpeditionParts-v1.png",
        "Assets/Images/UI/Country/Controls/SchoolEmblems-v1.png",
        "Assets/Images/UI/Country/Controls/FacilityEmblems-v1.png",
        "Assets/Images/UI/Country/Controls/AllocationCoin-v1.png",
        "Assets/Images/UI/Country/Expedition/MapSymbols-v1.png"
    };

    [MenuItem("WarSim/Country/Update Shared Texture Sampling")]
    internal static void UpdateSampling()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating texture sampling.");
        foreach (string path in SamplingTextures)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear) continue;
            ConfigureSampling(importer);
            importer.SaveAndReimport();
        }
    }

    static void ConfigureSampling(TextureImporter importer)
    {
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
    }

    internal static void Import(string path, SpriteRect[] regions)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        ConfigureSampling(importer);
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new InvalidOperationException("Sprite editing is unavailable: " + path);
        var allowed = capability.GetEditCapability();
        foreach (var operation in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot, EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!allowed.HasCapability(operation)) throw new InvalidOperationException("Sprite importer cannot " + operation + ": " + path);
        var existing = provider.GetSpriteRects();
        foreach (var region in regions)
        {
            region.spriteID = existing.FirstOrDefault(sprite => sprite.name == region.name)?.spriteID ?? GUID.Generate();
            region.pivot = Vector2.one * .5f; region.alignment = SpriteAlignment.Center;
        }
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names == null) throw new InvalidOperationException("Sprite names are unavailable: " + path);
        provider.SetSpriteRects(regions);
        names.SetNameFileIdPairs(regions.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
    }
}
