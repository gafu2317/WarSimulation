using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatMagicStoneSystemTests
{
    [Test]
    public void TakeDamage_ReducesMagicStoneHP()
    {
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            CombatEditModeTestUtil.SetPrivateField(system, "_mainStoneMaxHP", 100);

            MapData map = CreateMapWithMainStones();
            system.Initialize(map);

            Assert.That(system.TakeDamage(1, 30), Is.EqualTo(30));
            Assert.That(system.TryGetHP(1, out int hp), Is.True);
            Assert.That(hp, Is.EqualTo(70));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void EnemyMainStoneDestroyed_EndsBattleWithVictory()
    {
        GameObject flowGo = new GameObject("BattleFlow");
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            CombatEditModeTestUtil.SetPrivateField(system, "_mainStoneMaxHP", 50);

            CombatBattleFlow flow = flowGo.AddComponent<CombatBattleFlow>();
            CombatEditModeTestUtil.WireBattleFlow(flow, system);
            CombatEditModeTestUtil.SetPrivateField(flow, "_state", CombatBattleState.Running);

            MapData map = CreateMapWithMainStones();
            system.Initialize(map);
            system.TakeDamage(1, 50);

            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Victory));
        }
        finally
        {
            Object.DestroyImmediate(flowGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void MainStoneDestroyed_WhileWaitingToStart_DoesNotChangeBattleState()
    {
        GameObject flowGo = new GameObject("BattleFlow");
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            CombatEditModeTestUtil.SetPrivateField(system, "_mainStoneMaxHP", 50);

            CombatBattleFlow flow = flowGo.AddComponent<CombatBattleFlow>();
            CombatEditModeTestUtil.WireBattleFlow(flow, system);

            MapData map = CreateMapWithMainStones();
            system.Initialize(map);
            system.TakeDamage(1, 50);

            Assert.That(flow.State, Is.EqualTo(CombatBattleState.WaitingToStart));
        }
        finally
        {
            Object.DestroyImmediate(flowGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void OwnMainStoneDestroyed_EndsBattleWithDefeat()
    {
        GameObject flowGo = new GameObject("BattleFlow");
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            CombatEditModeTestUtil.SetPrivateField(system, "_mainStoneMaxHP", 50);

            CombatBattleFlow flow = flowGo.AddComponent<CombatBattleFlow>();
            CombatEditModeTestUtil.WireBattleFlow(flow, system);
            CombatEditModeTestUtil.SetPrivateField(flow, "_state", CombatBattleState.Running);

            MapData map = CreateMapWithMainStones();
            system.Initialize(map);
            system.TakeDamage(0, 50);

            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Defeat));
        }
        finally
        {
            Object.DestroyImmediate(flowGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void SubStoneDestroyed_KeepsBattleRunning()
    {
        GameObject flowGo = new GameObject("BattleFlow");
        GameObject systemGo = new GameObject("MagicStoneSystem");
        try
        {
            CombatMagicStoneSystem system = systemGo.AddComponent<CombatMagicStoneSystem>();
            CombatEditModeTestUtil.SetPrivateField(system, "_subStoneMaxHP", 40);

            CombatBattleFlow flow = flowGo.AddComponent<CombatBattleFlow>();
            CombatEditModeTestUtil.WireBattleFlow(flow, system);
            CombatEditModeTestUtil.SetPrivateField(flow, "_state", CombatBattleState.Running);

            MapData map = CreateMapWithMainAndSubStones();
            system.Initialize(map);
            system.TakeDamage(2, 40);

            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Running));
            Assert.That(system.IsDestroyed(FeatureType.OwnSubStone), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(flowGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    private static MapData CreateMapWithMainStones()
    {
        MapData map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(3f, 0f, 3f)));
        return map;
    }

    private static MapData CreateMapWithMainAndSubStones()
    {
        MapData map = CreateMapWithMainStones();
        map.AddFeature(new PlacedFeature(FeatureType.OwnSubStone, new Vector3(2f, 0f, 1f)));
        return map;
    }
}
