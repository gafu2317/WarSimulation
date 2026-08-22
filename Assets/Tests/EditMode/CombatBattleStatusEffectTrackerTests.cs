using NUnit.Framework;
using UnityEngine;

public sealed class CombatBattleStatusEffectTrackerTests
{
    [Test]
    public void Tracker_RecordsInitialBuffActivationTargetAndDuration()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character target = CreateCharacter(targetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, target);

            tracker.Record(CreateChange(target, source, "Buff", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Complete(3f);

            CombatBattleSupportSummary summary = tracker.GetSupportSummary(source);
            Assert.That(summary.BuffActivationCount, Is.EqualTo(1));
            Assert.That(summary.BuffTargetCount, Is.EqualTo(1));
            Assert.That(summary.BuffDurationSeconds, Is.EqualTo(3f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Tracker_RefreshAddsActivationWithoutOverlappingDurationOrTarget()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character target = CreateCharacter(targetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, target);

            tracker.Record(CreateChange(target, source, "Buff", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Record(CreateChange(target, source, "Buff", CombatStatusEffectChangeKind.Refreshed, 1.25f, 5f), 3f);
            tracker.Complete(7f);

            CombatBattleStatusEffectResult result = tracker.GetEffectResults(source)[0];
            Assert.That(result.ActivationCount, Is.EqualTo(2));
            Assert.That(result.TargetCount, Is.EqualTo(1));
            Assert.That(result.DurationSeconds, Is.EqualTo(7f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Tracker_SumsUniqueTargetsAndPerTargetDurations()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject firstTargetObject = new GameObject("FirstTarget");
        GameObject secondTargetObject = new GameObject("SecondTarget");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character firstTarget = CreateCharacter(firstTargetObject, CombatTeam.Enemy);
            Character secondTarget = CreateCharacter(secondTargetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, firstTarget, secondTarget);

            tracker.Record(CreateChange(firstTarget, source, "BuffA", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Record(CreateChange(secondTarget, source, "BuffB", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Complete(5f);

            CombatBattleSupportSummary characterSummary = tracker.GetSupportSummary(source);
            CombatBattleSupportSummary teamSummary = tracker.GetTeamSupportSummary(CombatTeam.Ally);
            Assert.That(characterSummary.BuffTargetCount, Is.EqualTo(2));
            Assert.That(characterSummary.BuffDurationSeconds, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(teamSummary.BuffTargetCount, Is.EqualTo(2));
            Assert.That(teamSummary.BuffDurationSeconds, Is.EqualTo(10f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(firstTargetObject);
            Object.DestroyImmediate(secondTargetObject);
        }
    }

    [Test]
    public void Tracker_StopsDurationAtRemovalAndAtExpiration()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject removedTargetObject = new GameObject("RemovedTarget");
        GameObject expiredTargetObject = new GameObject("ExpiredTarget");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character removedTarget = CreateCharacter(removedTargetObject, CombatTeam.Enemy);
            Character expiredTarget = CreateCharacter(expiredTargetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, removedTarget, expiredTarget);

            tracker.Record(CreateChange(removedTarget, source, "RemovedBuff", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Record(CreateChange(removedTarget, source, "RemovedBuff", CombatStatusEffectChangeKind.Removed, 1.25f, 0f), 2f);
            tracker.Record(CreateChange(expiredTarget, source, "ExpiredBuff", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Complete(8f);

            CombatBattleStatusEffectResult result = tracker.GetEffectResults(source)[0];
            Assert.That(result.ActivationCount, Is.EqualTo(2));
            Assert.That(result.TargetCount, Is.EqualTo(2));
            Assert.That(result.DurationSeconds, Is.EqualTo(7f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(removedTargetObject);
            Object.DestroyImmediate(expiredTargetObject);
        }
    }

    [Test]
    public void Tracker_SeparatesBuffAndDebuffForTheSameStat()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character target = CreateCharacter(targetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, target);

            tracker.Record(CreateChange(target, source, "Buff", CombatStatusEffectChangeKind.Applied, 1.25f, 4f), 0f);
            tracker.Record(CreateChange(target, source, "Debuff", CombatStatusEffectChangeKind.Applied, 0.7f, 2f), 0f);
            tracker.Complete(4f);

            CombatBattleSupportSummary summary = tracker.GetSupportSummary(source);
            Assert.That(summary.BuffActivationCount, Is.EqualTo(1));
            Assert.That(summary.BuffTargetCount, Is.EqualTo(1));
            Assert.That(summary.BuffDurationSeconds, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(summary.DebuffActivationCount, Is.EqualTo(1));
            Assert.That(summary.DebuffTargetCount, Is.EqualTo(1));
            Assert.That(summary.DebuffDurationSeconds, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(tracker.GetEffectResults(source), Has.Count.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Tracker_IgnoresUnsupportedMultiplierTypeAndUnknownSource()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject unknownSourceObject = new GameObject("UnknownSource");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character unknownSource = CreateCharacter(unknownSourceObject, CombatTeam.Ally);
            Character target = CreateCharacter(targetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, target);

            tracker.Record(CreateChange(target, source, "Neutral", CombatStatusEffectChangeKind.Applied, 1f, 5f), 0f);
            tracker.Record(CreateChange(target, source, "Root", CombatStatusEffectChangeKind.Applied, 1.25f, 5f, CombatStatusEffects.EffectType.Root), 0f);
            tracker.Record(CreateChange(target, unknownSource, "Unknown", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Complete(5f);

            Assert.That(tracker.GetEffectResults(source), Is.Empty);
            Assert.That(tracker.GetSupportSummary(source).BuffActivationCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(unknownSourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Tracker_ClearRemovesEffectsFromThePreviousBattle()
    {
        GameObject sourceObject = new GameObject("Source");
        GameObject targetObject = new GameObject("Target");
        try
        {
            Character source = CreateCharacter(sourceObject, CombatTeam.Ally);
            Character target = CreateCharacter(targetObject, CombatTeam.Enemy);
            var tracker = CreateTracker(source, target);
            tracker.Record(CreateChange(target, source, "Buff", CombatStatusEffectChangeKind.Applied, 1.25f, 5f), 0f);
            tracker.Clear();

            tracker.Begin(new[] { source }, new[] { target }, 10f);
            tracker.Complete(12f);

            Assert.That(tracker.GetEffectResults(source), Is.Empty);
            Assert.That(tracker.GetSupportSummary(source).BuffActivationCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    private static CombatBattleStatusEffectTracker CreateTracker(
        Character source,
        params Character[] additionalTargets)
    {
        var enemies = new Character[additionalTargets.Length];
        for (int i = 0; i < additionalTargets.Length; i++)
        {
            enemies[i] = additionalTargets[i];
        }

        var tracker = new CombatBattleStatusEffectTracker();
        tracker.Begin(new[] { source }, enemies, 0f);
        return tracker;
    }

    private static CombatStatusEffectChange CreateChange(
        Character target,
        Character source,
        string key,
        CombatStatusEffectChangeKind kind,
        float multiplier,
        float remainingSeconds,
        CombatStatusEffects.EffectType type = CombatStatusEffects.EffectType.StatModifier)
    {
        return new CombatStatusEffectChange(
            target,
            key,
            type,
            kind,
            new CombatEffectSource(source, SkillId.None, null),
            0,
            CombatStatusEffects.StatKind.STR,
            multiplier,
            remainingSeconds);
    }

    private static Character CreateCharacter(GameObject target, CombatTeam team)
    {
        Character character = target.AddComponent<Character>();
        character.SetTeam(team);
        return character;
    }
}
