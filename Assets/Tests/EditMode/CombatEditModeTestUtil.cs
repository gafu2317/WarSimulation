using System.Reflection;
using NUnit.Framework;
using UnityEngine;

internal static class CombatEditModeTestUtil
{
    public static void SetPrivateField(object target, string fieldName, object value)
    {
        Assert.That(target, Is.Not.Null, "SetPrivateField target must not be null.");
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field ??= target.GetType().BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    public static void WireMapSystem(CombatCharacterSystem system, CombatMapSystem mapSystem)
    {
        SetPrivateField(system, "_mapSystem", mapSystem);
    }

    public static void WireVision(CombatVision vision, CombatCharacterSystem system)
    {
        SetPrivateField(vision, "_characterSystem", system);
    }

    public static void WireCollector(
        CombatAiContextCollector collector,
        CombatCharacterSystem system,
        CombatMapSystem mapSystem)
    {
        SetPrivateField(collector, "_characterSystem", system);
        SetPrivateField(collector, "_mapSystem", mapSystem);
    }

    public static void WirePersonality(
        PlainPersonality personality,
        CombatCharacterSystem system,
        CombatMapSystem mapSystem)
    {
        SetPrivateField(personality, "_characterSystem", system);
        SetPrivateField(personality, "_mapSystem", mapSystem);
    }

    public static void WireBattleFlow(CombatBattleFlow flow, CombatMagicStoneSystem stoneSystem)
    {
        flow.SetMagicStoneSystem(stoneSystem);
    }

    public static PlainPersonality EnsurePlainPersonality(GameObject go)
    {
        Assert.That(go, Is.Not.Null);
        PlainPersonality personality = go.GetComponent<PlainPersonality>();
        if (personality == null)
        {
            personality = go.AddComponent<PlainPersonality>();
        }

        return personality;
    }
}
