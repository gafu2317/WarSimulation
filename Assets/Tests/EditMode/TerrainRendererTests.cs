using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class TerrainRendererTests
{
    [Test]
    public void Render_PaintsWaterLayerInsteadOfForestWhenForestOverlapsWater()
    {
        var go = new GameObject("TerrainRendererTest");
        try
        {
            TerrainRenderer renderer = go.AddComponent<TerrainRenderer>();
            SetPrivateField(renderer, "_alphamapResolutionOverride", 4);

            var height = new HeightMap(4, 4, 1f);
            var ground = new GroundStateGrid(4, 4, 1f);
            ground.SetCell(1, 1, GroundState.Water);

            var map = new MapData(height, ground, seed: 1);
            map.AddForestRegion(new ForestRegion(new Vector2(1.5f, 1.5f), 1f, 0f, 1f));

            renderer.Render(map);

            float[,,] alphas = renderer.Terrain.terrainData.GetAlphamaps(0, 0, 4, 4);
            Assert.That(alphas[1, 1, 3], Is.EqualTo(1f).Within(0.001f), "water layer");
            Assert.That(alphas[1, 1, 4], Is.EqualTo(0f).Within(0.001f), "forest layer");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void SetPrivateField<T>(Object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }
}
