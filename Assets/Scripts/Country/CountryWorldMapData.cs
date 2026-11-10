using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Country/World Map")]
public sealed class CountryWorldMapData : ScriptableObject
{
    public Sprite Castle, Flag, HomeIcon;
    public Vector2[] Coastline = Array.Empty<Vector2>();
    public int HomeRegion;
    [Min(1)] public int BattleCPCost = 20;
    [Min(1)] public int RaidMinDays = 20, RaidMaxDays = 30;
    public CountryWorldNation[] Nations = Array.Empty<CountryWorldNation>();
    public CountryWorldRegion[] Regions = Array.Empty<CountryWorldRegion>();
    public int Capital(int nation) => nation == 0 ? HomeRegion : Nations[nation].CapitalRegion >= 0 ? Nations[nation].CapitalRegion : Array.FindIndex(Regions, region => region.InitialOwner == nation);

    public int TravelDays(int region)
    {
        Vector2 home = Regions[HomeRegion].Center;
        var distances = Regions.Where((_, i) => i != HomeRegion).Select(item => Vector2.Distance(home, item.Center)).ToArray();
        float near = distances.Min(), far = distances.Max();
        float distance = Vector2.Distance(home, Regions[region].Center);
        return far <= near ? 10 : Math.Clamp((int)Math.Round(10 + 20 * (distance - near) / (far - near), MidpointRounding.AwayFromZero), 10, 30);
    }

    public bool Adjacent(int first, int second)
    {
        var a = Regions[first].Polygon; var b = Regions[second].Polygon;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                if ((a[i] - b[(j + 1) % b.Length]).sqrMagnitude < .00000001f &&
                    (a[(i + 1) % a.Length] - b[j]).sqrMagnitude < .00000001f) return true;
        return false;
    }

    public static bool Contains(Vector2[] polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    public void Validate()
    {
        if (BattleCPCost < 1 || RaidMinDays < 1 || RaidMaxDays < RaidMinDays)
            throw new ArgumentException("Battle CP cost and raid intervals must be positive.");
        if (Nations.Length < 2 || HomeRegion < 0 || HomeRegion >= Regions.Length || Regions[HomeRegion].InitialOwner != 0)
            throw new ArgumentException("World map requires a player home region and at least two nations.");
        if (Regions.Select(region => region.Id).Any(string.IsNullOrWhiteSpace) || Regions.Select(region => region.Id).Distinct().Count() != Regions.Length)
            throw new ArgumentException("World region IDs must be distinct.");
        foreach (var region in Regions)
            if (region.InitialOwner < 0 || region.InitialOwner >= Nations.Length || region.Polygon.Length < 3 || !Contains(region.Polygon, region.Center))
                throw new ArgumentException("World region ownership, polygon or center is invalid.");
        if (Coastline.Length > 0)
        {
            CountryWorldMapGeometry.Triangulate(Coastline);
            for (int i = 0; i < Regions.Length; i++)
            {
                var polygon = Regions[i].Polygon;
                CountryWorldMapGeometry.Triangulate(polygon);
                if (!Contains(Coastline, Regions[i].Center)) throw new ArgumentException("Territory center must be on the continent.");
            }
        }
    }
}

[Serializable]
public sealed class CountryWorldNation
{
    public string Name;
    public Color Color;
    public int CapitalRegion = -1;
}

[Serializable]
public sealed class CountryWorldRegion
{
    public string Id, Name;
    public int InitialOwner;
    public Vector2 Center;
    public Vector2[] Polygon;
}
