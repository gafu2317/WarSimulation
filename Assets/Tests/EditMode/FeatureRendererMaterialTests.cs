using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class FeatureRendererMaterialTests
{
    [Test]
    public void RenderMagicStones_UsesPersistentSharedTeamMaterialsInEditMode()
    {
        GameObject host = new GameObject("FeatureRendererMaterialTestHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            var map = new MapData(new HeightMap(10, 10, 1f), new GroundStateGrid(10, 10, 1f), seed: 1);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(2f, 0f, 2f)));
            map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(8f, 0f, 8f)));

            renderer.Render(map);
            MagicStone[] stones = renderer.GetComponentsInChildren<MagicStone>();
            Assert.That(stones, Has.Length.EqualTo(2));
            Material own = FindCoreMaterial(stones, FeatureType.OwnMainStone);
            Material enemy = FindCoreMaterial(stones, FeatureType.EnemyMainStone);
            Assert.That(AssetDatabase.Contains(own), Is.True);
            Assert.That(AssetDatabase.Contains(enemy), Is.True);
            Assert.That(own, Is.Not.SameAs(enemy));
            Assert.That(ReadColor(own).b, Is.GreaterThan(ReadColor(own).r));
            Assert.That(ReadColor(enemy).r, Is.GreaterThan(ReadColor(enemy).b));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static Material FindCoreMaterial(MagicStone[] stones, FeatureType type)
    {
        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i].FeatureType != type) continue;
            Transform core = FindChild(stones[i].transform, "Core");
            return core.GetComponentInChildren<Renderer>().sharedMaterial;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChild(root.GetChild(i), name);
            if (match != null) return match;
        }
        return null;
    }

    private static Color ReadColor(Material material)
    {
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return material.color;
    }
}
