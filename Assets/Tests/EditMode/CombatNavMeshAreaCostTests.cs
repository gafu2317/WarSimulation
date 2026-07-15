using System.Reflection;
using NUnit.Framework;

public sealed class CombatNavMeshAreaCostTests
{
    [Test]
    public void CalculateAreaCostScale_ScalesMinimumCostToOneWithoutChangingRatios()
    {
        var costs = new[]
        {
            new CombatNavMeshBuilder.NavMeshAreaCost("Forest", 0.5f),
            new CombatNavMeshBuilder.NavMeshAreaCost("Walkable", 1f),
            new CombatNavMeshBuilder.NavMeshAreaCost("Swamp", 1.5f),
        };

        float scale = CalculateAreaCostScale(costs);

        Assert.That(costs[0].Cost * scale, Is.EqualTo(1f));
        Assert.That(costs[1].Cost * scale, Is.EqualTo(2f));
        Assert.That(costs[2].Cost * scale, Is.EqualTo(3f));
        Assert.That(
            costs[2].Cost * scale / (costs[0].Cost * scale),
            Is.EqualTo(costs[2].Cost / costs[0].Cost));
    }

    [Test]
    public void CalculateAreaCostScale_KeepsCostsWhenMinimumIsAlreadyOne()
    {
        var costs = new[]
        {
            new CombatNavMeshBuilder.NavMeshAreaCost("Walkable", 1f),
            new CombatNavMeshBuilder.NavMeshAreaCost("River", 20f),
        };

        Assert.That(CalculateAreaCostScale(costs), Is.EqualTo(1f));
    }

    private static float CalculateAreaCostScale(CombatNavMeshBuilder.NavMeshAreaCost[] costs)
    {
        MethodInfo method = typeof(CombatNavMeshBuilder).GetMethod(
            "CalculateAreaCostScale",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (float)method.Invoke(null, new object[] { costs });
    }
}
