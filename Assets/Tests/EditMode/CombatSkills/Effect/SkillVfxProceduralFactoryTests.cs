using System;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillVfxProceduralFactoryTests
{
    [Test]
    public void AllSkills_HaveFiniteAnimatedGeometryAndOneSharedMaterial()
    {
        var parent = new GameObject("Vfx test");
        try
        {
            var material = Resources.Load<Material>("Combat/Vfx/StylizedSkill");
            Assert.That(material, Is.Not.Null);
            foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
            {
                if (id == SkillId.None) continue;
                Assert.That(SkillVfxProceduralFactory.TryCreate(id, Vector3.zero, Vector3.right * 3,
                    Vector3.forward * 2, parent.transform, out var root, out var lifetime), Is.True);
                var fx = root.GetComponent<SkillVfxEffect>();
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                fx.RenderAt(.2f);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), id.ToString());
                Assert.That(mesh.vertexCount, Is.LessThan(4096), id.ToString());
                Vector3[] before = mesh.vertices;
                fx.RenderAt(.4f);
                Assert.That(System.Linq.Enumerable.SequenceEqual(mesh.vertices, before), Is.False, id.ToString());
                foreach (var v in mesh.vertices)
                    Assert.That(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), Is.True, id.ToString());
                Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(lifetime, Is.GreaterThan(0));
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Test]
    public void RepeatedPlayback_ReusesObjectsAndHasABoundedConcurrentLimit()
    {
        var host = new GameObject("VFX pool test");
        try
        {
            var player = host.AddComponent<SkillVfxPlayer>();
            for (int i = 0; i < 100; i++)
            {
                player.TryPlay(SkillId.Wand_Bolt, Vector3.zero, Vector3.right, null, out _);
                player.ClearAll();
            }
            Assert.That(host.GetComponentsInChildren<SkillVfxEffect>(true).Length, Is.EqualTo(1));
            for (int i = 0; i < 120; i++) player.TryPlay(SkillId.Wand_Bolt, Vector3.zero, Vector3.right, null, out _);
            Assert.That(player.ActiveCount, Is.EqualTo(96));
            Assert.That(player.DroppedCount, Is.EqualTo(24));
            player.ClearAll();
            Assert.That(player.ActiveCount, Is.Zero);
            Assert.That(player.PooledCount, Is.EqualTo(96));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void BoundStatus_FollowsTranslationAndEndsWhenRemoved()
    {
        var actorGo = new GameObject("Actor");
        var fxGo = new GameObject("VFX");
        try
        {
            var actor = actorGo.AddComponent<Character>(); actor.Health.Initialize(30);
            actor.StatusEffects.ApplyStealth(5, "test", actor);
            var fx = fxGo.AddComponent<SkillVfxEffect>();
            fx.Prepare(SkillId.Grimoire_Stealth, Vector3.zero, Vector3.zero, Vector3.zero, SkillVfxEffect.Phase.Impact);
            fx.Bind(actor, actor, "test"); fx.Tick(.1f);
            var mesh = fxGo.GetComponentInChildren<MeshFilter>().sharedMesh;
            float before = mesh.bounds.center.x;
            actor.transform.position = Vector3.right * 7;
            fx.Tick(0);
            Assert.That(mesh.bounds.center.x - before, Is.EqualTo(7).Within(.001));
            actor.StatusEffects.ClearEffect("test");
            fx.Tick(.01f);
            Assert.That(fx.Tick(.26f), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(fxGo); UnityEngine.Object.DestroyImmediate(actorGo); }
    }

    [Test]
    public void CancelledCast_LeavesNoVisualAndNoEffectOutcomeDoesNotSpawn()
    {
        var actorGo = new GameObject("Actor"); var host = new GameObject("Player");
        try
        {
            var actor = actorGo.AddComponent<Character>(); var player = host.AddComponent<SkillVfxPlayer>();
            var action = new CombatSkillActionInfo(7, actor, CombatSkillFactory.Create(SkillId.Wand_ArcaneBlast), SkillExecutionContext.ForSelf(actor), 0);
            player.PlayCast(action);
            Assert.That(player.ActiveCount, Is.EqualTo(1));
            player.CancelCast(new CombatSkillActionResult(action, CombatSkillActionOutcome.Cancelled, Array.Empty<CombatActionEffect>()));
            Assert.That(player.ActiveCount, Is.Zero);
            player.PlayAction(new CombatSkillActionResult(action, CombatSkillActionOutcome.NoEffect, Array.Empty<CombatActionEffect>()));
            Assert.That(player.ActiveCount, Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(actorGo); }
    }
    [Test]
    public void GodsHand_CastVisualUsesTheGameplayCastDuration()
    {
        var root = new GameObject("Cast");
        try
        {
            var fx = root.AddComponent<SkillVfxEffect>();
            float duration = CombatSkillFactory.Create(SkillId.Wand_GodsHand).CastTimeSeconds;
            fx.Prepare(SkillId.Wand_GodsHand, Vector3.zero, Vector3.right, Vector3.right, SkillVfxEffect.Phase.Cast, duration);
            Assert.That(fx.Lifetime, Is.EqualTo(duration));
            Assert.That(fx.Tick(duration - .01f), Is.True);
            Assert.That(fx.Tick(.02f), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void OverlappingHealingFields_BindToSeparateZonesAndEndIndependently()
    {
        var actorGo = new GameObject("Owner"); var host = new GameObject("Player");
        var firstGo = new GameObject("First zone"); var secondGo = new GameObject("Second zone");
        try
        {
            var actor = actorGo.AddComponent<Character>(); actor.Health.Initialize(30);
            var player = host.AddComponent<SkillVfxPlayer>();
            var action = new CombatSkillActionInfo(9, actor, CombatSkillFactory.Create(SkillId.Rosary_HealingArea), SkillExecutionContext.ForPoint(Vector3.zero), 0);
            var result = new CombatSkillActionResult(action, CombatSkillActionOutcome.Completed, Array.Empty<CombatActionEffect>());
            var first = firstGo.AddComponent<RosaryHealingAreaZone>(); first.Initialize(actor, 3, 2, 5, 1);
            player.PlayAction(result);
            var second = secondGo.AddComponent<RosaryHealingAreaZone>(); second.Initialize(actor, 3, 2, 5, 1);
            player.PlayAction(result);
            var effects = host.GetComponentsInChildren<SkillVfxEffect>();
            Assert.That(effects.Length, Is.EqualTo(2));
            Assert.That(effects[0].BoundZone, Is.Not.SameAs(effects[1].BoundZone));
            first.CancelImmediate();
            int living = 0;
            foreach (var fx in effects) { fx.Tick(.01f); if (fx.Tick(.3f)) living++; }
            Assert.That(living, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(actorGo);
            UnityEngine.Object.DestroyImmediate(firstGo); UnityEngine.Object.DestroyImmediate(secondGo);
        }
    }

}
