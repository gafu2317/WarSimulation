using NUnit.Framework;
using UnityEditor;
using WarSimulation.Combat.Map;

public sealed class WebPlaytestBuildTests
{
    [Test]
    public void CountryStartsTheBuildAndAllPlayableBakedMapsAreIncluded()
    {
        string[] scenes = CombatPlaytestWebGLPlayer.GetBuildScenePaths();
        Assert.That(scenes[0], Is.EqualTo("Assets/Scenes/Country.unity"));
        Assert.That(scenes[1], Is.EqualTo("Assets/Scenes/GafuTest.unity"));
        Assert.That(scenes, Is.Unique);
        foreach (string guid in AssetDatabase.FindAssets("t:AuthoredMapDefinition", new[] { "Assets/Data/Map" }))
        {
            var map = AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (CombatMapAvailability.Evaluate(map, false).CanStartBattle)
                Assert.That(scenes, Does.Contain(map.BakedRuntimeScenePath), map.name);
        }
    }
}
