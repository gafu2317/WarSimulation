using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatCharacterSystemTests
{
    [Test]
    public void TryRelocateCharactersNearMainStones_MovesTeamsNearOwnStoneWithSpacing()
    {
        GameObject mapObject = new GameObject("MapSystem");
        GameObject characterSystemObject = new GameObject("CharacterSystem");
        GameObject allyObjectA = new GameObject("AllyA");
        GameObject allyObjectB = new GameObject("AllyB");
        GameObject enemyObjectA = new GameObject("EnemyA");
        GameObject enemyObjectB = new GameObject("EnemyB");

        try
        {
            CombatMapSystem mapSystem = mapObject.AddComponent<CombatMapSystem>();
            mapSystem.SetCurrentMap(CreateStoneTestMap());

            CombatCharacterSystem characterSystem = characterSystemObject.AddComponent<CombatCharacterSystem>();
            CombatEditModeTestUtil.WireMapSystem(characterSystem, mapSystem);

            Character allyA = allyObjectA.AddComponent<Character>();
            Character allyB = allyObjectB.AddComponent<Character>();
            Character enemyA = enemyObjectA.AddComponent<Character>();
            Character enemyB = enemyObjectB.AddComponent<Character>();
            allyA.SetTeam(CombatTeam.Ally);
            allyB.SetTeam(CombatTeam.Ally);
            enemyA.SetTeam(CombatTeam.Enemy);
            enemyB.SetTeam(CombatTeam.Enemy);
            characterSystem.AllyCharacters.Add(allyA);
            characterSystem.AllyCharacters.Add(allyB);
            characterSystem.EnemyCharacters.Add(enemyA);
            characterSystem.EnemyCharacters.Add(enemyB);

            bool moved = characterSystem.TryRelocateCharactersNearMainStones();

            Assert.That(moved, Is.True);
            Assert.That(HorizontalDistance(allyA.transform.position, new Vector3(2.5f, 0f, 1.5f)), Is.LessThan(3.5f));
            Assert.That(HorizontalDistance(allyB.transform.position, new Vector3(2.5f, 0f, 1.5f)), Is.LessThan(3.5f));
            Assert.That(HorizontalDistance(enemyA.transform.position, new Vector3(5.5f, 0f, 6.5f)), Is.LessThan(3.5f));
            Assert.That(HorizontalDistance(enemyB.transform.position, new Vector3(5.5f, 0f, 6.5f)), Is.LessThan(3.5f));

            Assert.That(HorizontalDistance(allyA.transform.position, allyB.transform.position), Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(HorizontalDistance(enemyA.transform.position, enemyB.transform.position), Is.GreaterThanOrEqualTo(1.5f));

            AssertValidTerrain(mapSystem, allyA.transform.position);
            AssertValidTerrain(mapSystem, allyB.transform.position);
            AssertValidTerrain(mapSystem, enemyA.transform.position);
            AssertValidTerrain(mapSystem, enemyB.transform.position);
        }
        finally
        {
            Object.DestroyImmediate(enemyObjectB);
            Object.DestroyImmediate(enemyObjectA);
            Object.DestroyImmediate(allyObjectB);
            Object.DestroyImmediate(allyObjectA);
            Object.DestroyImmediate(characterSystemObject);
            Object.DestroyImmediate(mapObject);
        }
    }

    private static MapData CreateStoneTestMap()
    {
        var height = new HeightMap(8, 8, 1f);
        var ground = new GroundStateGrid(8, 8, 1f);

        for (int z = 0; z < 8; z++)
        {
            for (int x = 0; x < 8; x++)
            {
                height.SetHeight(x, z, 0f);
            }
        }

        ground.SetCell(0, 0, GroundState.Water);
        height.CliffFaces.MarkCliff(1, 0);
        ground.SetCell(0, 7, GroundState.Water);
        height.CliffFaces.MarkCliff(1, 7);

        var map = new MapData(height, ground, seed: 7);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(2.5f, 0f, 1.5f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(5.5f, 0f, 6.5f)));
        return map;
    }

    private static void AssertValidTerrain(CombatMapSystem mapSystem, Vector3 position)
    {
        TerrainInfo terrain = mapSystem.GetTerrainInfo(position);
        Assert.That(terrain.GroundState, Is.Not.EqualTo(GroundState.Water));
        Assert.That(terrain.IsCliffFace, Is.False);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
