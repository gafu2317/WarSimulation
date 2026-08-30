using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;

public sealed class TerrainRendererPlayModeTests
{
    [UnityTest]
    public IEnumerator TerrainRenderer_ClearThenRenderInSameFrame_KeepsReplacementAfterFrameEnd()
    {
        var host = new GameObject("TerrainRendererPlayModeHost");
        try
        {
            TerrainRenderer renderer = host.AddComponent<TerrainRenderer>();
            var map = new MapData(
                new HeightMap(33, 33, 1f),
                new GroundStateGrid(33, 33, 1f),
                seed: 1);

            renderer.Render(map);
            Terrain first = renderer.Terrain;

            renderer.Clear();
            renderer.Render(map);
            Terrain replacement = renderer.Terrain;

            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement, Is.Not.SameAs(first));
            Assert.That(replacement.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                replacement.materialTemplate,
                Is.SameAs(Resources.Load<Material>("Combat/Map/GeneratedTerrainMaterial")));

            yield return null;

            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.gameObject.activeInHierarchy, Is.True);
            Assert.That(host.transform.Find("GeneratedTerrain")?.GetComponent<Terrain>(), Is.SameAs(replacement));
        }
        finally
        {
            Object.Destroy(host);
        }
    }
}
