using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

public sealed class CombatAiBatchOptimizationTests
{
    [Test]
    public void NavigationQueryCache_ReusesOnlyExactDestinationsWithinTheCurrentTick()
    {
        var cache = new CombatAiNavigationQueryCache();
        Vector3 destination = new(1f, 2f, 3f);
        var path = new NavMeshPath();
        var query = new CombatAiNavigationQuery(true, destination, path);

        cache.BeginTick();
        cache.Store(destination, query);
        cache.StoreRouteRisk(Vector3.zero, destination, 42f);

        Assert.That(cache.TryGet(destination, out CombatAiNavigationQuery cached), Is.True);
        Assert.That(cached.Path, Is.SameAs(path));
        Assert.That(cache.TryGet(destination + new Vector3(0.0001f, 0f, 0f), out _), Is.False);
        Assert.That(cache.TryGetRouteRisk(Vector3.zero, destination, out float risk), Is.True);
        Assert.That(risk, Is.EqualTo(42f));
        Assert.That(cache.TryGetRouteRisk(Vector3.up * 0.0001f, destination, out _), Is.False);

        cache.BeginTick();

        Assert.That(cache.TryGet(destination, out _), Is.False);
        Assert.That(cache.TryGetRouteRisk(Vector3.zero, destination, out _), Is.False);
        Assert.That(cache.Count, Is.Zero);
    }

    [Test]
    public void WorldSnapshot_KeepsCharacterStateCapturedAtBatchStart()
    {
        GameObject characterObject = new("Character");
        try
        {
            Character character = characterObject.AddComponent<Character>();
            character.Health.Initialize(100);
            characterObject.transform.position = new Vector3(2f, 0f, 3f);

            CombatAiWorldSnapshot snapshot = CombatAiWorldSnapshot.Capture(
                new[] { character },
                System.Array.Empty<Character>(),
                null);

            characterObject.transform.position = new Vector3(8f, 0f, 9f);
            character.Health.TakeDamage(25);

            Assert.That(snapshot.TryGetCharacter(character, out CombatAiCharacterSnapshot captured), Is.True);
            Assert.That(captured.Position, Is.EqualTo(new Vector3(2f, 0f, 3f)));
            Assert.That(captured.HP, Is.EqualTo(100));
        }
        finally
        {
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void StaticMapCache_RebuildsWhenTheMapChanges()
    {
        GameObject mapObject = new("MapSystem");
        try
        {
            CombatMapSystem mapSystem = mapObject.AddComponent<CombatMapSystem>();
            MapData firstMap = CreateMap(1);
            MapData secondMap = CreateMap(2);
            mapSystem.SetCurrentMap(firstMap);
            CombatAiStaticMapSnapshot first = CombatAiStaticMapCache.Get(mapSystem);
            Assert.That(CombatAiStaticMapCache.Get(mapSystem), Is.SameAs(first));

            mapSystem.SetCurrentMap(secondMap);

            Assert.That(CombatAiStaticMapCache.Get(mapSystem), Is.Not.SameAs(first));
        }
        finally
        {
            Object.DestroyImmediate(mapObject);
        }
    }

    private static MapData CreateMap(int seed)
    {
        return new MapData(
            new HeightMap(4, 4, 1f),
            new GroundStateGrid(4, 4, 1f),
            seed);
    }
}
