using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CountryWorldMapTests
{
    private CountryWorldMapData _map;
    private static readonly Vector2[] Coast = {
        new(.1f,.1f), new(.9f,.1f), new(.9f,.9f), new(.6f,.9f), new(.5f,.7f), new(.4f,.9f), new(.1f,.9f)
    };
    [SetUp] public void SetUp()
    {
        _map = ScriptableObject.CreateInstance<CountryWorldMapData>();
        _map.Nations = Enumerable.Range(0, 4).Select(i => new CountryWorldNation { Name = $"Nation{i}", CapitalRegion = i }).ToArray();
        _map.Regions = Enumerable.Range(0, 4).Select(i => {
            float x = i % 2 * .5f, y = i / 2 * .5f;
            return new CountryWorldRegion { Id = $"r{i}", InitialOwner = i, Center = new Vector2(x+.25f,y+.25f),
                Polygon = new[] { new Vector2(x,y),new Vector2(x+.5f,y),new Vector2(x+.5f,y+.5f),new Vector2(x,y+.5f) } };
        }).ToArray();
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(_map);
    [TestCase(-1, 24, 3)] [TestCase(101, 24, 3)]
    [TestCase(5, -1, 3)] [TestCase(5, 121, 3)] [TestCase(5, 24, 0)]
    public void InvalidEnemyGrowthSettingsAreRejected(int rate, int months, int interval)
    {
        _map.Validate();
        _map.EnemyMonthlyGrowthPercent = rate;
        _map.EnemyGrowthMonths = months;
        _map.EnemySkillIntervalMonths = interval;
        Assert.Throws<ArgumentException>(_map.Validate);
    }

    [Test] public void CapitalLocationsAreSeparateFromTheTerritories()
    {
        _map.Nations[0].CapitalRegion = 3;
        Assert.That(_map.Capital(0), Is.Zero); Assert.That(_map.Capital(1), Is.EqualTo(1));
        _map.Nations[1].CapitalRegion = -1;
        Assert.That(_map.Capital(1), Is.EqualTo(1));
    }
    [Test] public void DisplayCoastlineDoesNotChangeTravelTimesOrLogicalAdjacency()
    {
        var days=Enumerable.Range(1,3).Select(_map.TravelDays).ToArray(); _map.Coastline=Coast;
        Assert.That(Enumerable.Range(1,3).Select(_map.TravelDays),Is.EqualTo(days));
        Assert.That(_map.Adjacent(0,1),Is.True); Assert.That(_map.Adjacent(0,3),Is.False);
    }
    [Test] public void ConcaveCoastTriangulationPreservesAreaWithoutFillingTheBay()
    {
        var triangles=CountryWorldMapGeometry.Triangulate(Coast); float area=0;
        Assert.That(triangles.Length,Is.EqualTo((Coast.Length-2)*3));
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=Coast[triangles[i]];var b=Coast[triangles[i+1]];var c=Coast[triangles[i+2]];
            Assert.That(CountryWorldMapData.Contains(Coast,(a+b+c)/3),Is.True);
            area+=Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5f;
        }
        Assert.That(area,Is.EqualTo(.62f).Within(.00001f));
    }
    [Test] public void DegeneratePolygonsAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>CountryWorldMapGeometry.Triangulate(new[] { Vector2.zero,Vector2.one }));
        Assert.Throws<ArgumentException>(()=>CountryWorldMapGeometry.Triangulate(new[] { Vector2.zero,Vector2.one,Vector2.one*2 }));
    }

    [Test] public void AuthoredCapitalsBelongToTheirNations()
    {
        var map = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        map.Validate();
        for (int nation=0;nation<map.Nations.Length;nation++)
            Assert.That(map.Regions[map.Capital(nation)].InitialOwner, Is.EqualTo(nation));
    }

    [Test] public void AuthoredTerritoriesCoverTheContinentWithoutHolesSeaOrOverlap()
    {
        var map = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        var polygons = Enumerable.Range(0,map.Regions.Length).Select(i => map.Regions[i].Polygon).ToArray();
        Assert.That(polygons.Sum(Area), Is.EqualTo(Area(map.Coastline)).Within(.00001f));
        for (int x=0;x<37;x++)
            for (int y=0;y<37;y++)
            {
                var point = new Vector2((x+.371f)/37, (y+.619f)/37);
                Assert.That(polygons.Count(polygon => CountryWorldMapData.Contains(polygon,point)), Is.EqualTo(CountryWorldMapData.Contains(map.Coastline,point) ? 1 : 0), point.ToString());
            }
    }

    [Test] public void AuthoredWorldAndEachNationsStartingTerritoryAreConnected()
    {
        var map = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        foreach (int nation in Enumerable.Range(-1, map.Nations.Length + 1))
        {
            var regions = Enumerable.Range(0,map.Regions.Length).Where(id => nation < 0 || map.Regions[id].InitialOwner == nation).ToArray();
            Assert.That(regions, Is.Not.Empty, $"Nation {nation}");
            var visited = new HashSet<int> { regions[0] }; var queue = new Queue<int>(); queue.Enqueue(regions[0]);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in regions.Where(id => !visited.Contains(id) && map.Adjacent(current,id)))
                { visited.Add(next); queue.Enqueue(next); }
            }
            Assert.That(visited.Count, Is.EqualTo(regions.Length), $"Nation {nation}");
        }
    }
    private static float Area(Vector2[] polygon)
    {
        float area = 0;
        for (int i=0;i<polygon.Length;i++) { var a=polygon[i];var b=polygon[(i+1)%polygon.Length];area+=a.x*b.y-b.x*a.y; }
        return Mathf.Abs(area)*.5f;
    }
}
