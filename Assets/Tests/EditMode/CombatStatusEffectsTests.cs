using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatStatusEffectsTests
{
    [Test]
    public void CombatStatusEffects_ReportsBindAndPoisonSources()
    {
        GameObject targetGo = new GameObject("Target");
        GameObject sourceGo = new GameObject("Source");
        var reportedTypes = new System.Collections.Generic.List<CombatStatusEffects.EffectType>();
        Character reportedSource = null;
        void Handle(Character target, CombatStatusEffects.EffectType type, Character source)
        {
            reportedTypes.Add(type);
            reportedSource = source;
        }

        CombatStatusEffectEvents.Applied += Handle;
        try
        {
            Character target = targetGo.AddComponent<Character>();
            Character source = sourceGo.AddComponent<Character>();

            target.StatusEffects.ApplyBind(3f, source: source);
            target.StatusEffects.ApplyPoison(4, 5f, 1f, source: source);

            Assert.That(reportedTypes, Is.EqualTo(new[]
            {
                CombatStatusEffects.EffectType.Bind,
                CombatStatusEffects.EffectType.Poison,
            }));
            Assert.That(reportedSource, Is.SameAs(source));
        }
        finally
        {
            CombatStatusEffectEvents.Applied -= Handle;
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(sourceGo);
        }
    }

    [Test]
    public void CombatStatusEffects_ReportsLifecycleAndKeepsPeriodicDamageSource()
    {
        GameObject targetGo = new GameObject("Target");
        GameObject sourceGo = new GameObject("Source");
        var changes = new System.Collections.Generic.List<CombatStatusEffectChange>();
        CombatDamageEvent reportedDamage = default;
        void HandleChange(CombatStatusEffectChange change) => changes.Add(change);
        void HandleDamage(CombatDamageEvent damage) => reportedDamage = damage;

        CombatStatusEffectEvents.Changed += HandleChange;
        CombatDamageEvents.Resolved += HandleDamage;
        try
        {
            Character target = targetGo.AddComponent<Character>();
            Character source = sourceGo.AddComponent<Character>();
            target.Health.Initialize(30);
            SkillBase skill = new IdentifiedSkill(new GrimoirePoisonSkill(), SkillId.Grimoire_Poison);
            CombatSkillActionInfo action = CombatSkillActionEvents.Start(
                source,
                skill,
                SkillExecutionContext.ForTarget(target));

            CombatSkillActionEvents.Execute(action, () =>
            {
                target.StatusEffects.ApplyPoison(4, 5f, 1f, "Poison", source);
                target.StatusEffects.ApplyPoison(4, 5f, 1f, "Poison", source);
                target.StatusEffects.ApplyBind(2f, "Bind", source);
                target.StatusEffects.ClearEffect("Bind");
            });
            ForceAllPeriodicEffectsReadyNow(target.StatusEffects);
            target.StatusEffects.GetActiveEffectSnapshots();
            ExpireAllEffects(target.StatusEffects);
            target.StatusEffects.GetActiveEffectSnapshots();

            Assert.That(changes.Exists(change =>
                change.Kind == CombatStatusEffectChangeKind.Applied &&
                change.Source.Character == source &&
                change.Source.SkillId == SkillId.Grimoire_Poison), Is.True);
            Assert.That(changes.Exists(change =>
                change.Kind == CombatStatusEffectChangeKind.Tick &&
                change.Amount == 4), Is.True);
            Assert.That(changes.Exists(change =>
                change.Kind == CombatStatusEffectChangeKind.Refreshed), Is.True);
            Assert.That(changes.Exists(change =>
                change.Kind == CombatStatusEffectChangeKind.Removed), Is.True);
            Assert.That(changes.Exists(change =>
                change.Kind == CombatStatusEffectChangeKind.Expired), Is.True);
            Assert.That(reportedDamage.Target, Is.SameAs(target));
            Assert.That(reportedDamage.Source.Character, Is.SameAs(source));
            Assert.That(reportedDamage.Source.SkillId, Is.EqualTo(SkillId.Grimoire_Poison));
        }
        finally
        {
            CombatStatusEffectEvents.Changed -= HandleChange;
            CombatDamageEvents.Resolved -= HandleDamage;
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(sourceGo);
        }
    }

    [Test]
    public void CombatStatusEffects_ApplyReducesMultiplier()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f);

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(character.STRBuff, Is.EqualTo(0.7f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_UpdatesSameKeyEffectInsteadOfStacking()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, StatDebuffSkill.GetEffectKey(CombatStatusEffects.StatKind.STR));
            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, StatDebuffSkill.GetEffectKey(CombatStatusEffects.StatKind.STR));

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.7f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_AllowsDifferentKeysOnSameStat()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f, "DebuffA");
            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.8f, 5f, "DebuffB");

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(0.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_HasActiveEffectAndRemainingSeconds()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(
                CombatStatusEffects.StatKind.FAI,
                1.2f,
                6f,
                StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI));

            Assert.That(statusEffects.HasActiveEffect(StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI)), Is.True);
            Assert.That(statusEffects.GetRemainingSeconds(StatBuffSkill.GetEffectKey(CombatStatusEffects.StatKind.FAI)), Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_ExpiredEffectIsRemoved()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.Apply(CombatStatusEffects.StatKind.STR, 0.7f, 5f);
            ExpireAllEffects(statusEffects);

            Assert.That(statusEffects.GetMultiplier(CombatStatusEffects.StatKind.STR), Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_InvulnerableAndRootAreTracked()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.ApplyInvulnerable(4f);
            statusEffects.ApplyRoot(3f);

            Assert.That(statusEffects.IsInvulnerable, Is.True);
            Assert.That(statusEffects.IsRooted, Is.True);
            Assert.That(statusEffects.HasActiveEffect(CombatStatusEffects.EffectType.Invulnerable), Is.True);
            Assert.That(statusEffects.HasActiveEffect(CombatStatusEffects.EffectType.Root), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_StealthIsTracked()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            CombatStatusEffects statusEffects = characterGo.AddComponent<Character>().StatusEffects;

            statusEffects.ApplyStealth(4f);

            Assert.That(statusEffects.IsStealthed, Is.True);
            Assert.That(statusEffects.HasActiveEffect(CombatStatusEffects.EffectType.Stealth), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_PoisonTicksDamage()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(maxHP: 30);
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.ApplyPoison(damagePerTick: 4, durationSeconds: 5f, tickIntervalSeconds: 1f);
            ForceAllPeriodicEffectsReadyNow(statusEffects);
            statusEffects.GetActiveEffectSnapshots();

            Assert.That(character.Health.HP, Is.EqualTo(26));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_HealOverTimeTicksHealing()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(maxHP: 30, currentHP: 10);
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.ApplyHealOverTime(healPerTick: 5, durationSeconds: 5f, tickIntervalSeconds: 1f);
            ForceAllPeriodicEffectsReadyNow(statusEffects);
            statusEffects.GetActiveEffectSnapshots();

            Assert.That(character.Health.HP, Is.EqualTo(15));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_PoisonTicksDamageWhenUpdateRunsAfterExpiry()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(maxHP: 30);
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.ApplyPoison(damagePerTick: 4, durationSeconds: 5f, tickIntervalSeconds: 1f);
            ForcePeriodicEffectTiming(statusEffects, nextTickAt: Time.time - 0.01f, expiresAt: Time.time - 0.005f);
            statusEffects.GetActiveEffectSnapshots();

            Assert.That(character.Health.HP, Is.EqualTo(26));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CombatStatusEffects_HealOverTimeTicksHealingWhenUpdateRunsAfterExpiry()
    {
        GameObject characterGo = new GameObject("Character");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.Health.Initialize(maxHP: 30, currentHP: 10);
            CombatStatusEffects statusEffects = character.StatusEffects;

            statusEffects.ApplyHealOverTime(healPerTick: 5, durationSeconds: 5f, tickIntervalSeconds: 1f);
            ForcePeriodicEffectTiming(statusEffects, nextTickAt: Time.time - 0.01f, expiresAt: Time.time - 0.005f);
            statusEffects.GetActiveEffectSnapshots();

            Assert.That(character.Health.HP, Is.EqualTo(15));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    private static void ExpireAllEffects(CombatStatusEffects statusEffects)
    {
        FieldInfo effectsField = typeof(CombatStatusEffects).GetField(
            "_effects",
            BindingFlags.NonPublic | BindingFlags.Instance);
        object effectsList = effectsField?.GetValue(statusEffects);
        if (effectsList == null) return;

        System.Collections.IList list = (System.Collections.IList)effectsList;
        for (int i = 0; i < list.Count; i++)
        {
            object effect = list[i];
            FieldInfo expiresAtField = effect.GetType().GetField(
                "ExpiresAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            expiresAtField?.SetValue(effect, -1f);
            list[i] = effect;
        }
    }

    private static void ForceAllPeriodicEffectsReadyNow(CombatStatusEffects statusEffects)
    {
        FieldInfo effectsField = typeof(CombatStatusEffects).GetField(
            "_effects",
            BindingFlags.NonPublic | BindingFlags.Instance);
        object effectsList = effectsField?.GetValue(statusEffects);
        if (effectsList == null) return;

        System.Collections.IList list = (System.Collections.IList)effectsList;
        for (int i = 0; i < list.Count; i++)
        {
            object effect = list[i];
            FieldInfo nextTickAtField = effect.GetType().GetField(
                "NextTickAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            FieldInfo expiresAtField = effect.GetType().GetField(
                "ExpiresAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            nextTickAtField?.SetValue(effect, Time.time);
            expiresAtField?.SetValue(effect, Time.time + 0.01f);
            list[i] = effect;
        }
    }

    private static void ForcePeriodicEffectTiming(CombatStatusEffects statusEffects, float nextTickAt, float expiresAt)
    {
        FieldInfo effectsField = typeof(CombatStatusEffects).GetField(
            "_effects",
            BindingFlags.NonPublic | BindingFlags.Instance);
        object effectsList = effectsField?.GetValue(statusEffects);
        if (effectsList == null) return;

        System.Collections.IList list = (System.Collections.IList)effectsList;
        for (int i = 0; i < list.Count; i++)
        {
            object effect = list[i];
            FieldInfo nextTickAtField = effect.GetType().GetField(
                "NextTickAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            FieldInfo expiresAtField = effect.GetType().GetField(
                "ExpiresAt",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            nextTickAtField?.SetValue(effect, nextTickAt);
            expiresAtField?.SetValue(effect, expiresAt);
            list[i] = effect;
        }
    }
}
