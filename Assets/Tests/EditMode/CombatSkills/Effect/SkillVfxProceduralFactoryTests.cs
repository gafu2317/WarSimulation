using System;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillVfxProceduralFactoryTests
{
    [Test]
    public void ResourcesCatalog_IsAvailableToRuntimeBridge()
    {
        SkillVfxCatalog catalog = Resources.Load<SkillVfxCatalog>("Combat/Vfx/SkillVfxCatalog");
        Assert.That(catalog, Is.Not.Null);
    }

    [Test]
    public void TryCreate_CoversEverySkillId()
    {
        GameObject parent = new GameObject("VfxTestRoot");
        try
        {
            foreach (SkillId skillId in Enum.GetValues(typeof(SkillId)))
            {
                if (skillId == SkillId.None) continue;

                bool created = SkillVfxProceduralFactory.TryCreate(
                    skillId,
                    Vector3.zero,
                    Vector3.right * 3f,
                    Vector3.forward * 2f,
                    parent.transform,
                    out GameObject root,
                    out float lifetime);

                Assert.That(created, Is.True, $"{skillId} is missing a VFX definition.");
                Assert.That(root, Is.Not.Null, skillId.ToString());
                Assert.That(root.transform.childCount, Is.GreaterThan(0), skillId.ToString());
                Assert.That(lifetime, Is.GreaterThan(0f), skillId.ToString());
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void TryCreate_RangedPointSkill_DrawsWeaponColorLineToFallbackTarget()
    {
        GameObject parent = new GameObject("VfxTestRoot");
        Vector3 self = Vector3.zero;
        Vector3 target = Vector3.right * 10f;
        try
        {
            SkillVfxProceduralFactory.TryCreate(
                SkillId.Wand_AreaBlast,
                self,
                target,
                null,
                parent.transform,
                out GameObject root,
                out _);

            LineRenderer line = root.GetComponentInChildren<LineRenderer>();

            Assert.That(line, Is.Not.Null);
            Assert.That(line.GetPosition(0), Is.EqualTo(self + Vector3.up));
            Assert.That(line.GetPosition(line.positionCount - 1), Is.EqualTo(target + Vector3.up));
            Assert.That(line.startColor, Is.EqualTo(new Color(1f, 0.95f, 0.05f, 1f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void PlayAction_DoesNotShowNoEffectOutcome()
    {
        GameObject actorGo = new GameObject("Actor");
        GameObject playerGo = new GameObject("Player");
        try
        {
            Character actor = actorGo.AddComponent<Character>();
            SkillBase skill = CombatSkillFactory.Create(SkillId.Sword_Slash);
            var action = new CombatSkillActionInfo(
                1,
                actor,
                skill,
                SkillExecutionContext.ForSelf(actor),
                0);
            var result = new CombatSkillActionResult(
                action,
                CombatSkillActionOutcome.NoEffect,
                Array.Empty<CombatActionEffect>());
            SkillVfxPlayer player = playerGo.AddComponent<SkillVfxPlayer>();

            player.PlayAction(result);

            Assert.That(playerGo.transform.childCount, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(actorGo);
        }
    }
}
