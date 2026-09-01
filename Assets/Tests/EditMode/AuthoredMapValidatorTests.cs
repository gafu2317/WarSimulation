using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class AuthoredMapValidatorTests
{
    [Test]
    public void Validate_DetectsMissingConfigAndShortRiver()
    {
        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        try
        {
            List<AuthoredMapValidationIssue> missingConfig = AuthoredMapValidator.Validate(definition);
            Assert.That(AuthoredMapValidator.HasErrors(missingConfig), Is.True);

            MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
            SetPrivateField(config, "_worldSize", 20f);
            SetPrivateField(config, "_cellsPerSide", 20);
            definition.SharedConfig = config;
            definition.Rivers.Add(new AuthoredRiverPlacement
            {
                ControlPoints = new List<Vector2> { new Vector2(1f, 1f) },
            });
            definition.Mountains.Add(new AuthoredMountainPlacement
            {
                Shape = null,
                Center = new Vector2(100f, 100f),
            });

            List<AuthoredMapValidationIssue> issues = AuthoredMapValidator.Validate(definition);
            Assert.That(issues.Exists(i => i.Message.Contains("River[0]")), Is.True);
            Assert.That(issues.Exists(i => i.Message.Contains("Mountain[0]")), Is.True);
            Assert.That(issues.Exists(i => i.IsError && i.Message.Contains("Mountain[0]")), Is.True);
            Object.DestroyImmediate(config);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Validate_WarnsWhenMainStonesMissing()
    {
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        try
        {
            SetPrivateField(config, "_worldSize", 20f);
            SetPrivateField(config, "_cellsPerSide", 20);
            SetPrivateField(config, "_mainStonesPerSide", 1);
            definition.SharedConfig = config;

            List<AuthoredMapValidationIssue> issues = AuthoredMapValidator.Validate(definition);
            Assert.That(AuthoredMapValidator.HasErrors(issues), Is.False);
            Assert.That(issues.Exists(i => !i.IsError && i.Message.Contains("Own main")), Is.True);
            Assert.That(issues.Exists(i => !i.IsError && i.Message.Contains("Enemy main")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void ValidateRejectsDuplicateRouteIdsAndOutsideWaypoints()
    {
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        try
        {
            SetPrivateField(config, "_worldSize", 20f);
            SetPrivateField(config, "_cellsPerSide", 20);
            definition.SharedConfig = config;
            definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                "same", "First", AuthoredAssaultRouteSource.Manual));
            definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                "same",
                "Second",
                AuthoredAssaultRouteSource.Manual,
                new[] { new Vector2(21f, 1f) }));

            List<AuthoredMapValidationIssue> issues = AuthoredMapValidator.Validate(definition);

            Assert.That(issues.Exists(i => i.IsError && i.Message.Contains("duplicated")), Is.True);
            Assert.That(issues.Exists(i => i.IsError && i.Message.Contains("outside")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void ValidateRejectsFixedFeatureOutsidePlayableBounds()
    {
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        try
        {
            SetPrivateField(config, "_worldSize", 20f);
            SetPrivateField(config, "_cellsPerSide", 20);
            SetPrivateField(config, "_placementRadii", new FeaturePlacementRadii { Rock = 1f });
            definition.SharedConfig = config;
            definition.HasFixedFeaturePlacements = true;
            definition.Rocks.Add(new AuthoredPointFeaturePlacement { Center = Vector2.zero });

            List<AuthoredMapValidationIssue> issues = AuthoredMapValidator.Validate(definition);

            Assert.That(issues.Exists(i => i.IsError && i.Message.Contains("Rock[0]")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
        }
    }

    private static void SetPrivateField<T>(Object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }
}
