using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TerrainMaterialPersistenceTests
{
    [Test]
    public void MapScenes_UsePersistentTerrainRenderingAssets()
    {
        Material expected = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Resources/Combat/Map/GeneratedTerrainMaterial.mat");
        Assert.That(expected, Is.Not.Null);
        Assert.That(expected.IsKeywordEnabled("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"), Is.True);

        var paths = new List<string> { "Assets/Scenes/GafuTest.unity" };
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/BakedMaps" });
        for (int i = 0; i < guids.Length; i++) paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        for (int i = 0; i < paths.Count; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Additive);
            try
            {
                Terrain terrain = FindTerrain(scene);
                Assert.That(terrain, Is.Not.Null, paths[i]);
                Assert.That(terrain.materialTemplate, Is.SameAs(expected), paths[i]);
                TerrainLayer[] layers = terrain.terrainData.terrainLayers;
                Assert.That(layers, Has.Length.EqualTo(7), paths[i]);
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    Assert.That(AssetDatabase.Contains(layers[layer]), Is.True, paths[i]);
                    Assert.That(AssetDatabase.Contains(layers[layer].diffuseTexture), Is.True, paths[i]);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static Terrain FindTerrain(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Terrain terrain = roots[i].GetComponentInChildren<Terrain>(true);
            if (terrain != null) return terrain;
        }
        return null;
    }
}
