using System;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillVfxProceduralFactoryTests
{
    [Test]
    public void Mesh_ReusedTexturedQuadsPreserveTrianglesUvAndOpacity()
    {
        var builder = new SkillVfxMesh { Opacity = .5f };
        try
        {
            for (int frame = 0; frame < 2; frame++)
            {
                builder.Clear();
                if (frame == 0) builder.Triangle(Vector3.zero, Vector3.up, Vector3.right, Color.white);
                builder.TextureQuad(Vector3.zero, Vector3.right, Vector3.one, Vector3.up,
                    Color.white, .3f, new Rect(.2f, .4f, .5f, .25f), .09f);
                builder.Upload();
                var mesh = builder.Mesh;
                int start = frame == 0 ? 3 : 0;
                Assert.That(mesh.vertexCount, Is.EqualTo(start + 4));
                var indices = mesh.triangles;
                var vertices = mesh.vertices;
                var colors = mesh.colors32;
                var uv = new System.Collections.Generic.List<Vector4>();
                mesh.GetUVs(0, uv);
                var expectedPositions = new[] { Vector3.zero, Vector3.right, Vector3.one,
                    Vector3.zero, Vector3.one, Vector3.up };
                var expectedUv = new[] { new Vector2(.2f, .4f), new Vector2(.7f, .4f), new Vector2(.7f, .65f),
                    new Vector2(.2f, .4f), new Vector2(.7f, .65f), new Vector2(.2f, .65f) };
                Assert.That(indices.Length, Is.EqualTo(start + 6));
                for (int corner = 0; corner < 6; corner++)
                {
                    int index = indices[start + corner];
                    Assert.That(vertices[index], Is.EqualTo(expectedPositions[corner]));
                    Assert.That(Vector2.Distance(new Vector2(uv[index].x, uv[index].y), expectedUv[corner]), Is.LessThan(.000001f));
                    Assert.That(uv[index].z, Is.EqualTo(1.09f).Within(.000001f));
                    Assert.That(uv[index].w, Is.EqualTo(.3f));
                    Assert.That(colors[index], Is.EqualTo((Color32)new Color(1, 1, 1, .5f)));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(builder.Mesh); }
    }

    [Test]
    public void SupportColors_AreSharedWithinEachCategory()
    {
        foreach (var id in new[] { SkillId.Shield_ShoulderGuard, SkillId.Bible_StrBuff, SkillId.Bible_IntBuff,
            SkillId.Bible_FaiBuff, SkillId.Bible_AgiBuff, SkillId.Bible_Invulnerable, SkillId.Bible_Gotsume,
            SkillId.Grimoire_Stealth, SkillId.Shield_IronWall })
            Assert.That(SkillVfxArt.ColorFor(id), Is.EqualTo(SkillVfxArt.ColorFor(SkillId.Bible_StrBuff)), id.ToString());
        foreach (var id in new[] { SkillId.Grimoire_StrDebuff, SkillId.StatDebuff_INT, SkillId.StatDebuff_FAI,
            SkillId.StatDebuff_AGI, SkillId.Grimoire_Bind, SkillId.Grimoire_Poison, SkillId.Shield_Taunt })
            Assert.That(SkillVfxArt.ColorFor(id), Is.EqualTo(SkillVfxArt.ColorFor(SkillId.Grimoire_Poison)), id.ToString());
        foreach (var id in new[] { SkillId.Rosary_DistantHeal, SkillId.Rosary_CloseHeal,
            SkillId.Rosary_Regeneration, SkillId.Rosary_HealingArea })
            Assert.That(SkillVfxArt.ColorFor(id), Is.EqualTo(SkillVfxArt.ColorFor(SkillId.Rosary_CloseHeal)), id.ToString());
    }

    [Test]
    public void SwordTechniques_UseDistinctColors()
    {
        var colors = new[] { SkillVfxArt.ColorFor(SkillId.Sword_Slash),
            SkillVfxArt.ColorFor(SkillId.Sword_QuickSlash), SkillVfxArt.ColorFor(SkillId.Sword_StrongSlash) };
        Assert.That(colors[0], Is.Not.EqualTo(colors[1]));
        Assert.That(colors[0], Is.Not.EqualTo(colors[2]));
        Assert.That(colors[1], Is.Not.EqualTo(colors[2]));
    }

    [Test]
    public void AutoPlay_VisitsEverySkillWithoutWaitingAfterVisualEnd()
    {
        var host = new GameObject("Auto playback test");
        try
        {
            var player = host.AddComponent<SkillVfxPlayer>();
            var viewer = host.AddComponent<SkillVfxViewer>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(SkillVfxViewer).GetField("_player", flags).SetValue(viewer, player);
            var advance = typeof(SkillVfxViewer).GetMethod("AdvanceAutoPlay", flags);
            viewer.ToggleAutoPlay();
            var seen = new System.Collections.Generic.HashSet<SkillId>();
            SkillId first = host.GetComponentInChildren<SkillVfxEffect>().Skill;
            for (int i = 0; i < Enum.GetValues(typeof(SkillId)).Length - 1; i++)
            {
                var effect = host.GetComponentInChildren<SkillVfxEffect>();
                SkillId current = effect.Skill;
                Assert.That(seen.Add(current), Is.True);
                Assert.That(effect.Lifetime, Is.EqualTo(SkillVfxArt.Duration(current) + SkillVfxArt.PreviewLead(current)).Within(.001f));
                advance.Invoke(viewer, null);
                Assert.That(host.GetComponentInChildren<SkillVfxEffect>().Skill, Is.EqualTo(current));
                Assert.That(effect.Tick(effect.Lifetime + .1f), Is.False);
                player.ClearAll();
                advance.Invoke(viewer, null);
                Assert.That(player.ActiveCount, Is.EqualTo(1));
            }
            Assert.That(host.GetComponentInChildren<SkillVfxEffect>().Skill, Is.EqualTo(first));
            viewer.ToggleAutoPlay();
            player.ClearAll();
            advance.Invoke(viewer, null);
            Assert.That(player.ActiveCount, Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void AtlasFrames_StayFiniteThroughOpeningAndDisappearance()
    {
        var root = new GameObject("Prototype test");
        try
        {
            var fx = root.AddComponent<SkillVfxEffect>();
            foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
            {
                if (id == SkillId.None) continue;
                fx.Prepare(id, Vector3.zero, Vector3.right * 2, Vector3.right * 2);
                foreach (float time in new[] { 0f, .025f, .13f, .42f, 1.1f, 1.8f })
                {
                    fx.RenderAt(time);
                    var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                    foreach (Vector3 v in mesh.vertices)
                        Assert.That(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), Is.True, $"{id}: {time}");
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void AllSkills_HaveFiniteAnimatedVisualsAndSharedMaterials()
    {
        var parent = new GameObject("Vfx test");
        try
        {
            var material = SkillVfxAtlas.Shared.Material;
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
                Color32[] beforeColors = mesh.colors32;
                var beforeUv = new System.Collections.Generic.List<Vector4>();
                mesh.GetUVs(0, beforeUv);
                fx.RenderAt(.4f);
                var afterUv = new System.Collections.Generic.List<Vector4>();
                mesh.GetUVs(0, afterUv);
                bool unchanged = System.Linq.Enumerable.SequenceEqual(mesh.vertices, before) &&
                    System.Linq.Enumerable.SequenceEqual(mesh.colors32, beforeColors) &&
                    System.Linq.Enumerable.SequenceEqual(beforeUv, afterUv);
                Assert.That(unchanged, Is.False, id.ToString());
                foreach (var v in mesh.vertices)
                    Assert.That(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), Is.True, id.ToString());
                Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentInChildren<Renderer>().sharedMaterial == material, Is.True, id.ToString());
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
