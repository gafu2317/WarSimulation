using System.Collections.Generic;
using UnityEngine;

public enum CombatSkillDebugMarkerShape
{
    Cube,
    Sphere,
    Cylinder,
}

public enum CombatSkillDebugMarkerTarget
{
    Self,
    PrimaryTarget,
    ResolvedTargets,
    TargetPoint,
}

public readonly struct CombatSkillDebugMarkerSpec
{
    public CombatSkillDebugMarkerSpec(
        CombatSkillDebugMarkerShape shape,
        CombatSkillDebugMarkerTarget target,
        Color color,
        float durationSeconds,
        Vector3 scale,
        float verticalOffset = 0f,
        bool usePointRadius = false,
        float pointRadius = 0f)
    {
        Shape = shape;
        Target = target;
        Color = color;
        DurationSeconds = durationSeconds;
        Scale = scale;
        VerticalOffset = verticalOffset;
        UsePointRadius = usePointRadius;
        PointRadius = pointRadius;
    }

    public CombatSkillDebugMarkerShape Shape { get; }
    public CombatSkillDebugMarkerTarget Target { get; }
    public Color Color { get; }
    public float DurationSeconds { get; }
    public Vector3 Scale { get; }
    public float VerticalOffset { get; }
    public bool UsePointRadius { get; }
    public float PointRadius { get; }
}

public static class CombatSkillDebugVisualCatalog
{
    private static readonly Color SwordColor = new Color(1f, 0.15f, 0.15f, 0.55f);
    private static readonly Color ShieldColor = new Color(0.2f, 0.45f, 1f, 0.55f);
    private static readonly Color WandColor = new Color(1f, 0.9f, 0.15f, 0.55f);
    private static readonly Color BibleColor = new Color(1f, 0.55f, 0.1f, 0.55f);
    private static readonly Color GrimoireColor = new Color(0.7f, 0.2f, 1f, 0.55f);
    private static readonly Color RosaryColor = new Color(0.15f, 1f, 0.3f, 0.55f);

    private static readonly Vector3 NormalCubeScale = new Vector3(0.6f, 0.6f, 0.6f);
    private static readonly Vector3 LargeCubeScale = new Vector3(0.9f, 0.9f, 0.9f);
    private static readonly Vector3 SmallCubeScale = new Vector3(0.35f, 0.35f, 0.35f);
    private static readonly Vector3 PointRingBaseScale = new Vector3(1f, 0.08f, 1f);

    private static readonly Dictionary<SkillId, CombatSkillDebugMarkerSpec[]> SpecsBySkill = new()
    {
        { SkillId.Sword_Slash, PrimaryTargetOnly(SwordColor, 0.6f, NormalCubeScale) },
        { SkillId.Shield_Slash, PrimaryTargetOnly(ShieldColor, 0.6f, NormalCubeScale) },
        { SkillId.Wand_Bolt, PrimaryTargetOnly(WandColor, 0.6f, NormalCubeScale) },
        { SkillId.Wand_ArcaneBlast, PrimaryTargetOnly(WandColor, 0.6f, LargeCubeScale) },
        { SkillId.Wand_GodsHand, PrimaryTargetOnly(WandColor, 0.6f, LargeCubeScale) },
        {
            SkillId.Wand_AreaBlast,
            new[]
            {
                SelfMarker(WandColor, 0.35f, SmallCubeScale),
                PointMarker(WandColor, 0.6f, pointRadius: 2.5f),
                ResolvedTargetsMarker(WandColor, 0.6f, NormalCubeScale),
            }
        },
        { SkillId.Grimoire_Bolt, PrimaryTargetOnly(GrimoireColor, 0.6f, NormalCubeScale) },
        { SkillId.Grimoire_StrDebuff, PrimaryTargetOnly(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.StatDebuff_INT, PrimaryTargetOnly(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.StatDebuff_FAI, PrimaryTargetOnly(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.StatDebuff_AGI, PrimaryTargetOnly(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.Grimoire_Bind, PrimaryTargetOnly(GrimoireColor, 3f, NormalCubeScale) },
        { SkillId.Grimoire_Poison, PrimaryTargetOnly(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.Grimoire_Stealth, SelfTarget(GrimoireColor, 5f, NormalCubeScale) },
        { SkillId.Bible_Smite, PrimaryTargetOnly(BibleColor, 0.6f, NormalCubeScale) },
        { SkillId.Bible_StrBuff, PrimaryTargetOnly(BibleColor, 5f, NormalCubeScale) },
        { SkillId.Bible_IntBuff, PrimaryTargetOnly(BibleColor, 5f, NormalCubeScale) },
        { SkillId.Bible_FaiBuff, PrimaryTargetOnly(BibleColor, 6f, NormalCubeScale) },
        { SkillId.Bible_AgiBuff, PrimaryTargetOnly(BibleColor, 5f, NormalCubeScale) },
        { SkillId.Bible_Invulnerable, SelfTarget(BibleColor, 3f, NormalCubeScale) },
        { SkillId.Bible_Gotsume, PrimaryTargetOnly(BibleColor, 5f, NormalCubeScale) },
        {
            SkillId.Bible_CarryRush,
            new[]
            {
                SelfMarker(BibleColor, 4f, SmallCubeScale),
                PrimaryTargetMarker(BibleColor, 4f, NormalCubeScale),
            }
        },
        { SkillId.Rosary_Strike, PrimaryTargetOnly(RosaryColor, 0.6f, NormalCubeScale) },
        { SkillId.Rosary_DistantHeal, PrimaryTargetOnly(RosaryColor, 0.6f, NormalCubeScale) },
        { SkillId.Rosary_CloseHeal, PrimaryTargetOnly(RosaryColor, 0.6f, NormalCubeScale) },
        { SkillId.Rosary_Regeneration, PrimaryTargetOnly(RosaryColor, 5f, NormalCubeScale) },
        {
            SkillId.Rosary_HealingArea,
            new[]
            {
                SelfMarker(RosaryColor, 0.35f, SmallCubeScale),
                PointMarker(RosaryColor, 5f, pointRadius: 3f),
            }
        },
        {
            SkillId.Rosary_SacrificeThunder,
            new[]
            {
                SelfMarker(RosaryColor, 0.6f, SmallCubeScale),
                ResolvedTargetsMarker(RosaryColor, 0.6f, NormalCubeScale),
            }
        },
        { SkillId.Shield_ShoulderGuard, PrimaryTargetOnly(ShieldColor, 5f, NormalCubeScale) },
    };

    public static bool TryGetSpecs(SkillId skillId, out IReadOnlyList<CombatSkillDebugMarkerSpec> specs)
    {
        if (SpecsBySkill.TryGetValue(skillId, out CombatSkillDebugMarkerSpec[] resolved))
        {
            specs = resolved;
            return true;
        }

        specs = null;
        return false;
    }

    private static CombatSkillDebugMarkerSpec[] PrimaryTargetOnly(Color color, float durationSeconds, Vector3 scale)
    {
        return new[] { PrimaryTargetMarker(color, durationSeconds, scale) };
    }

    private static CombatSkillDebugMarkerSpec[] SelfTarget(Color color, float durationSeconds, Vector3 scale)
    {
        return new[] { SelfMarker(color, durationSeconds, scale) };
    }

    private static CombatSkillDebugMarkerSpec SelfMarker(Color color, float durationSeconds, Vector3 scale)
    {
        return new CombatSkillDebugMarkerSpec(
            CombatSkillDebugMarkerShape.Cube,
            CombatSkillDebugMarkerTarget.Self,
            color,
            durationSeconds,
            scale,
            verticalOffset: 2f);
    }

    private static CombatSkillDebugMarkerSpec PrimaryTargetMarker(Color color, float durationSeconds, Vector3 scale)
    {
        return new CombatSkillDebugMarkerSpec(
            CombatSkillDebugMarkerShape.Cube,
            CombatSkillDebugMarkerTarget.PrimaryTarget,
            color,
            durationSeconds,
            scale,
            verticalOffset: 2f);
    }

    private static CombatSkillDebugMarkerSpec ResolvedTargetsMarker(Color color, float durationSeconds, Vector3 scale)
    {
        return new CombatSkillDebugMarkerSpec(
            CombatSkillDebugMarkerShape.Cube,
            CombatSkillDebugMarkerTarget.ResolvedTargets,
            color,
            durationSeconds,
            scale,
            verticalOffset: 2f);
    }

    private static CombatSkillDebugMarkerSpec PointMarker(Color color, float durationSeconds, float pointRadius)
    {
        return new CombatSkillDebugMarkerSpec(
            CombatSkillDebugMarkerShape.Cylinder,
            CombatSkillDebugMarkerTarget.TargetPoint,
            color,
            durationSeconds,
            PointRingBaseScale,
            verticalOffset: 0.05f,
            usePointRadius: true,
            pointRadius: pointRadius);
    }
}
