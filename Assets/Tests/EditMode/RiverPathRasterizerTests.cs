using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class RiverPathRasterizerTests
{
    [Test]
    public void Rasterize_ConnectsControlPointsWithoutGaps()
    {
        var height = new HeightMap(20, 20, 1f);
        var points = new List<Vector2>
        {
            new Vector2(1.2f, 1.2f),
            new Vector2(8.7f, 1.2f),
            new Vector2(8.7f, 10.4f),
        };

        List<Vector2Int> path = RiverPathRasterizer.Rasterize(points, height);

        Assert.That(path.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(path[0], Is.EqualTo(new Vector2Int(1, 1)));
        Assert.That(path[path.Count - 1], Is.EqualTo(new Vector2Int(8, 10)));

        for (int i = 1; i < path.Count; i++)
        {
            int dx = Mathf.Abs(path[i].x - path[i - 1].x);
            int dy = Mathf.Abs(path[i].y - path[i - 1].y);
            Assert.That(dx <= 1 && dy <= 1, Is.True);
            Assert.That(dx + dy, Is.GreaterThan(0));
        }
    }

    [Test]
    public void Rasterize_ReturnsEmptyForInsufficientPoints()
    {
        var height = new HeightMap(8, 8, 1f);
        Assert.That(RiverPathRasterizer.Rasterize(null, height), Is.Empty);
        Assert.That(RiverPathRasterizer.Rasterize(new List<Vector2>(), height), Is.Empty);
    }
}
