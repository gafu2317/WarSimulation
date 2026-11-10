using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CountryExpeditionMapBuilder
{
    [MenuItem("WarSim/Country/Update Expedition Map")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before changing the world map.");
        var data = AssetDatabase.LoadAssetAtPath<CountryWorldMapData>("Assets/Data/Country/CountryWorldMap.asset");
        if (data != null)
        {
            Apply(data); data.Validate(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        }
        CountryExpeditionBuilder.Build();
    }

    internal static void Apply(CountryWorldMapData data)
    {
        if (data.Nations.Length != 4) throw new ArgumentException("The initial world map requires four nations.");
        var coast = Coastline();
        var centers = new[] {
            new Vector2(0.253525f,0.726954f), new Vector2(0.377084f,0.816113f), new Vector2(0.567386f,0.714583f), new Vector2(0.753205f,0.739494f),
            new Vector2(0.254199f,0.595080f), new Vector2(0.447364f,0.575290f), new Vector2(0.618347f,0.581342f), new Vector2(0.849167f,0.571671f),
            new Vector2(0.181864f,0.399582f), new Vector2(0.411342f,0.390849f), new Vector2(0.589600f,0.410400f), new Vector2(0.784940f,0.357693f),
            new Vector2(0.274491f,0.247866f), new Vector2(0.443148f,0.284293f), new Vector2(0.575943f,0.312676f), new Vector2(0.749369f,0.237200f),
            new Vector2(0.328898f,0.704316f), new Vector2(0.500032f,0.745973f), new Vector2(0.169253f,0.620940f), new Vector2(0.710769f,0.635416f),
            new Vector2(0.746210f,0.473615f), new Vector2(0.852380f,0.403911f), new Vector2(0.328177f,0.279989f), new Vector2(0.499921f,0.354144f),
            new Vector2(0.681745f,0.194712f),
        };
        int[] capitals = { 5,1,7,13 };
        for (int pass = 0; pass < 3; pass++)
        {
            var cells = Enumerable.Range(0,centers.Length).Select(id => Polygon(coast,centers,id)).ToArray();
            for (int id = 0; id < centers.Length; id++)
            {
                if (capitals.Contains(id)) continue;
                var center = Centroid(cells[id]);
                if (CountryWorldMapData.Contains(cells[id],center)) centers[id] = center;
            }
        }
        int[] owners = { 1,1,1,2,1,0,2,2,1,3,2,2,3,3,3,3,1,1,1,2,2,2,3,3,3 };
        string[] names = { "北冠領","白峰領","北森領","東冠領","西丘領","王都領","東丘領","琥珀領","西森領","翠野領","東森領","河口領","南森領","南丘領","川辺領","南端領","西北領","高森領","西浜領","東高領","中河領","東浜領","南谷領","南央領","南浜領" };
        data.HomeRegion = 5;
        data.Regions = centers.Select((point, i) => new CountryWorldRegion {
            Id = $"region-{i:00}", Name = names[i], InitialOwner = owners[i], Center = point, Polygon = Polygon(coast, centers, i)
        }).ToArray();
        data.Coastline = coast;
        for (int i = 0; i < capitals.Length; i++) data.Nations[i].CapitalRegion = capitals[i];
    }

    private static Vector2[] Coastline()
    {
        var points = new[] {
            new Vector2(.16f,.79f), new Vector2(.22f,.82f), new Vector2(.25f,.91f), new Vector2(.31f,.88f),
            new Vector2(.33f,.94f), new Vector2(.40f,.90f), new Vector2(.47f,.87f), new Vector2(.52f,.89f),
            new Vector2(.57f,.83f), new Vector2(.62f,.78f), new Vector2(.62f,.72f), new Vector2(.72f,.87f),
            new Vector2(.79f,.95f), new Vector2(.82f,.90f), new Vector2(.85f,.80f), new Vector2(.89f,.72f),
            new Vector2(.94f,.68f), new Vector2(.95f,.60f), new Vector2(.92f,.52f), new Vector2(.94f,.45f),
            new Vector2(.90f,.39f), new Vector2(.91f,.33f), new Vector2(.86f,.25f), new Vector2(.82f,.18f),
            new Vector2(.75f,.13f), new Vector2(.68f,.16f), new Vector2(.63f,.23f), new Vector2(.59f,.25f),
            new Vector2(.51f,.26f), new Vector2(.43f,.22f), new Vector2(.37f,.17f), new Vector2(.28f,.15f),
            new Vector2(.20f,.17f), new Vector2(.15f,.24f), new Vector2(.13f,.31f), new Vector2(.08f,.37f),
            new Vector2(.06f,.46f), new Vector2(.08f,.54f), new Vector2(.10f,.58f), new Vector2(.09f,.64f),
            new Vector2(.14f,.68f), new Vector2(.12f,.73f)
        };
        var coast = new List<Vector2>();
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[(i + points.Length - 1) % points.Length]; var b = points[i];
            var c = points[(i + 1) % points.Length]; var d = points[(i + 2) % points.Length];
            for (int step = 0; step < 4; step++)
            {
                float t = step / 4f;
                coast.Add(.5f * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t));
            }
        }
        return coast.ToArray();
    }

    private static Vector2 Centroid(Vector2[] polygon)
    {
        float area = 0; var center = Vector2.zero;
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i]; var b = polygon[(i+1)%polygon.Length];
            float cross = a.x*b.y-b.x*a.y;
            area += cross; center += (a+b)*cross;
        }
        return center/(3*area);
    }

    private static Vector2[] Polygon(Vector2[] coast, Vector2[] centers, int id)
    {
        var polygon = coast.ToList();
        for (int other = 0; other < centers.Length; other++)
        {
            if (other == id) continue;
            var normal = centers[other] - centers[id]; var middle = (centers[other] + centers[id]) * .5f;
            var clipped = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                float da = Vector2.Dot(a - middle, normal), db = Vector2.Dot(b - middle, normal);
                if (da <= 0) clipped.Add(a);
                if ((da <= 0) != (db <= 0)) clipped.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            polygon = clipped;
        }
        return polygon.ToArray();
    }
}
