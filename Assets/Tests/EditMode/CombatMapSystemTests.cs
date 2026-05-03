using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatMapSystemTests
{
    [Test]
    public void TryGetTerrainInfo_ReturnsBaseTerrainInfoAtWorldPosition()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(1.25f, 99f, 1.25f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.IsInBounds, Is.True);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Swamp));
            Assert.That(info.Height, Is.EqualTo(3f).Within(0.001f));
            Assert.That(info.WorldPosition.y, Is.EqualTo(3f).Within(0.001f));
            Assert.That(info.IsWater, Is.False);
            Assert.That(info.IsForest, Is.True);
            Assert.That(info.BiomeId, Is.EqualTo(MapData.UnsetBiomeId));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_ReportsOutOfBoundsButSamplesClampedEdge()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(-10f, 0f, 50f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.IsInBounds, Is.False);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Water));
            Assert.That(info.Height, Is.EqualTo(12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_UsesCurrentMapGroundStateAndBiome()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            Assert.That(system.SetGroundState(new Vector2Int(1, 1), GroundState.Snow), Is.True);
            Assert.That(system.SetBiomeId(new Vector2Int(1, 1), "snowstorm"), Is.True);

            bool found = system.TryGetTerrainInfo(new Vector3(1.25f, 0f, 1.25f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Snow));
            Assert.That(info.IsWater, Is.False);
            Assert.That(info.BiomeId, Is.EqualTo("snowstorm"));
            Assert.That(map.GroundStates.GetCell(1, 1), Is.EqualTo(GroundState.Snow));
            Assert.That(map.GetBiomeId(1, 1), Is.EqualTo("snowstorm"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_ReturnsCliffAndFrozenLakeFlags()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(2.5f, 0f, 2.5f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Water));
            Assert.That(info.IsWater, Is.True);
            Assert.That(info.IsCliffFace, Is.True);
            Assert.That(info.IsFrozenLake, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static MapData CreateTestMap()
    {
        var height = new HeightMap(4, 4, 1f);
        var ground = new GroundStateGrid(4, 4, 1f);
        for (int z = 0; z < 4; z++)
        {
            for (int x = 0; x < 4; x++)
            {
                height.SetHeight(x, z, x + z * 4);
            }
        }

        ground.SetCell(1, 1, GroundState.Swamp);
        ground.SetCell(0, 3, GroundState.Water);
        ground.SetCell(2, 2, GroundState.Water);
        height.CliffFaces.MarkCliff(2, 2);

        var map = new MapData(height, ground, 123);
        map.AddForestRegion(new ForestRegion(new Vector2(1.25f, 1.25f), 0.75f, 0f, 0.1f));
        map.AddLake(new LakeRegion(new Vector2(2.5f, 2.5f), 1f, 0f, isFrozen: true, waterTaggedRadius: 1f));
        return map;
    }
}
