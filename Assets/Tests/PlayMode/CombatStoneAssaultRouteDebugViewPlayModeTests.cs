using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;

public sealed class CombatStoneAssaultRouteDebugViewPlayModeTests
{
    [UnityTest]
    public IEnumerator CurrentMapChange_RefreshesVisibleAssaultRoutes()
    {
        GameObject systemObject = new GameObject("CombatMapSystem");
        GameObject viewObject = new GameObject("CombatStoneAssaultRouteDebugView");
        try
        {
            CombatMapSystem mapSystem = systemObject.AddComponent<CombatMapSystem>();
            mapSystem.enabled = false;
            CombatStoneAssaultRouteDebugView view = viewObject.AddComponent<CombatStoneAssaultRouteDebugView>();
            mapSystem.SetCurrentMap(CreateMapWithRoutes(2, 10f));

            CombatPlaytestDebugSettings.SetShowAssaultRoutes(true);
            yield return null;

            LineRenderer firstRoute = FindRouteLine(view, 1);
            LineRenderer secondRoute = FindRouteLine(view, 2);
            Assert.That(firstRoute.enabled, Is.True);
            Assert.That(secondRoute.enabled, Is.True);
            Assert.That(firstRoute.GetPosition(0), Is.EqualTo(new Vector3(10f, 0.15f, 0f)));

            mapSystem.SetCurrentMap(CreateMapWithRoutes(1, 30f));
            yield return null;

            Assert.That(firstRoute.enabled, Is.True);
            Assert.That(firstRoute.GetPosition(0), Is.EqualTo(new Vector3(30f, 0.15f, 0f)));
            Assert.That(secondRoute.enabled, Is.False);
            Assert.That(secondRoute.positionCount, Is.EqualTo(0));
        }
        finally
        {
            CombatPlaytestDebugSettings.SetShowAssaultRoutes(false);
            CombatAssaultRouteCache.Invalidate();
            Object.Destroy(systemObject);
            Object.Destroy(viewObject);
        }
    }

    private static MapData CreateMapWithRoutes(int routeCount, float startX)
    {
        var map = new MapData(
            new HeightMap(8, 8, 1f),
            new GroundStateGrid(8, 8, 1f),
            seed: 1);
        for (int i = 0; i < routeCount; i++)
        {
            float x = startX + i * 5f;
            map.AddAssaultRoute(new AssaultRoute(
                $"route-{i}",
                $"Route {i}",
                new[] { new Vector3(x, 0f, 0f), new Vector3(x, 0f, 3f) }));
        }

        return map;
    }

    private static LineRenderer FindRouteLine(CombatStoneAssaultRouteDebugView view, int routeNumber)
    {
        return view.transform
            .Find($"GeneratedStoneAssaultRoutes/Route{routeNumber}")
            .GetComponent<LineRenderer>();
    }
}
