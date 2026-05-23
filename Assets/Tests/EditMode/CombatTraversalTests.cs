using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatTraversalTests
{
    [Test]
    public void CanStandAt_RejectsOutOfBoundsButAllowsMapTerrainDetails()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateFlatMap();
            map.Height.CliffFaces.MarkCliff(1, 1);
            system.SetCurrentMap(map);

            Assert.That(system.CanStandAt(new Vector3(-1f, 0f, 0f)), Is.False);
            Assert.That(system.CanStandAt(new Vector3(1.25f, 0f, 1.25f)), Is.True);
            Assert.That(system.CanStandAt(new Vector3(2.25f, 0f, 2.25f)), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CanStandAt_AllowsSteepSlopeBecauseNavMeshOwnsWalkability()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            var height = new HeightMap(4, 4, 1f);
            var ground = new GroundStateGrid(4, 4, 1f);
            for (int z = 0; z < 4; z++)
            {
                for (int x = 0; x < 4; x++)
                {
                    height.SetHeight(x, z, x * 10f);
                }
            }

            system.SetCurrentMap(new MapData(height, ground, seed: 1));

            Assert.That(system.CanStandAt(new Vector3(1.5f, 0f, 1.5f)), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CanStandAt_AllowsWaterEvenWhenUnderlyingTerrainIsSteep()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            var height = new HeightMap(4, 4, 1f);
            var ground = new GroundStateGrid(4, 4, 1f);
            for (int z = 0; z < 4; z++)
            {
                for (int x = 0; x < 4; x++)
                {
                    height.SetHeight(x, z, x * 10f);
                    ground.SetCell(x, z, GroundState.Water);
                }
            }

            system.SetCurrentMap(new MapData(height, ground, seed: 1));

            Assert.That(system.CanStandAt(new Vector3(1.5f, 0f, 1.5f)), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTraversalInfo_ReturnsGroundStateSpeedMultipliers()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateFlatMap();
            map.GroundStates.SetCell(0, 0, GroundState.Normal);
            map.GroundStates.SetCell(1, 0, GroundState.Snow);
            map.GroundStates.SetCell(2, 0, GroundState.Swamp);
            map.GroundStates.SetCell(3, 0, GroundState.Water);
            map.GroundStates.SetCell(0, 1, GroundState.Water);
            map.AddLake(new LakeRegion(new Vector2(0.5f, 1.5f), 0.75f, 0f, isFrozen: true, waterTaggedRadius: 0.75f));
            system.SetCurrentMap(map);

            AssertSpeed(system, new Vector3(0.5f, 0f, 0.5f), 1f);
            AssertSpeed(system, new Vector3(1.5f, 0f, 0.5f), 0.75f);
            AssertSpeed(system, new Vector3(2.5f, 0f, 0.5f), 0.6f);
            AssertSpeed(system, new Vector3(3.5f, 0f, 0.5f), 0.25f);
            AssertSpeed(system, new Vector3(0.5f, 0f, 1.5f), 0.9f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTraversalInfo_UsesCurrentGroundState()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateFlatMap();
            system.SetCurrentMap(map);

            Assert.That(system.SetGroundState(new Vector2Int(1, 1), GroundState.Swamp), Is.True);
            AssertSpeed(system, new Vector3(1.25f, 0f, 1.25f), 0.6f);

            Assert.That(system.SetGroundState(new Vector2Int(1, 1), GroundState.Snow), Is.True);
            AssertSpeed(system, new Vector3(1.25f, 0f, 1.25f), 0.75f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void AssertSpeed(CombatMapSystem system, Vector3 position, float expected)
    {
        Assert.That(system.TryGetTraversalInfo(position, out TerrainTraversalInfo info), Is.True);
        Assert.That(info.CanStand, Is.True);
        Assert.That(info.MoveSpeedMultiplier, Is.EqualTo(expected).Within(0.001f));
    }

    private static MapData CreateFlatMap()
    {
        return new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
    }
}
