using System.Collections.Generic;
using UnityEngine;

public static class CombatSkillDebugIndicatorSystem
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const string GeneratedRootName = "GeneratedSkillDebugIndicators";

    private static readonly HashSet<SkillId> WarnedMissingSkills = new();
    private static readonly Dictionary<uint, Material> MaterialCache = new();
    private static Transform _generatedRoot;

    public static void Show(Character self, SkillId skillId, string skillName, SkillExecutionContext context)
    {
        if (self == null) return;

        CombatAiWorldLabel label = self.GetComponent<CombatAiWorldLabel>();
        if (label != null)
        {
            label.ShowSkill(skillName);
        }

        if (!CombatSkillDebugVisualCatalog.TryGetSpecs(skillId, out IReadOnlyList<CombatSkillDebugMarkerSpec> specs))
        {
            WarnMissingSkill(skillId);
            return;
        }

        EnsureGeneratedRoot();
        for (int i = 0; i < specs.Count; i++)
        {
            SpawnMarkers(self, context, specs[i]);
        }
    }

    private static void SpawnMarkers(Character self, SkillExecutionContext context, CombatSkillDebugMarkerSpec spec)
    {
        switch (spec.Target)
        {
            case CombatSkillDebugMarkerTarget.Self:
                SpawnCharacterMarker(self, self.transform.position, spec);
                break;
            case CombatSkillDebugMarkerTarget.PrimaryTarget:
                if (context.PrimaryTarget != null)
                {
                    SpawnCharacterMarker(context.PrimaryTarget, context.PrimaryTarget.transform.position, spec);
                }
                break;
            case CombatSkillDebugMarkerTarget.ResolvedTargets:
                for (int i = 0; i < context.ResolvedTargets.Count; i++)
                {
                    Character target = context.ResolvedTargets[i];
                    if (target == null) continue;
                    SpawnCharacterMarker(target, target.transform.position, spec);
                }
                break;
            case CombatSkillDebugMarkerTarget.TargetPoint:
                if (context.HasTargetPoint)
                {
                    SpawnMarker(context.TargetPoint, spec, CombatSkillDebugMarkerShape.Cylinder);
                }
                break;
        }
    }

    private static void SpawnCharacterMarker(Character target, Vector3 basePosition, CombatSkillDebugMarkerSpec spec)
    {
        CombatSkillDebugMarkerShape shape = target != null && target.Team == CombatTeam.Ally
            ? CombatSkillDebugMarkerShape.Cube
            : CombatSkillDebugMarkerShape.Sphere;
        SpawnMarker(basePosition, spec, shape);
    }

    private static void SpawnMarker(Vector3 basePosition, CombatSkillDebugMarkerSpec spec, CombatSkillDebugMarkerShape shape)
    {
        PrimitiveType primitiveType = shape switch
        {
            CombatSkillDebugMarkerShape.Sphere => PrimitiveType.Sphere,
            CombatSkillDebugMarkerShape.Cylinder => PrimitiveType.Cylinder,
            _ => PrimitiveType.Cube,
        };

        GameObject marker = GameObject.CreatePrimitive(primitiveType);
        marker.name = "SkillDebug_" + shape;
        marker.transform.SetParent(_generatedRoot, worldPositionStays: true);
        marker.transform.position = basePosition + Vector3.up * spec.VerticalOffset;
        marker.transform.localScale = ResolveScale(spec);

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = GetOrCreateMaterial(spec.Color);
        }

        Object.Destroy(marker, Mathf.Max(0.01f, spec.DurationSeconds));
    }

    private static Vector3 ResolveScale(CombatSkillDebugMarkerSpec spec)
    {
        if (!spec.UsePointRadius)
        {
            return spec.Scale;
        }

        float radius = Mathf.Max(0.01f, spec.PointRadius);
        return new Vector3(radius * 2f, spec.Scale.y, radius * 2f);
    }

    private static void EnsureGeneratedRoot()
    {
        if (_generatedRoot != null) return;

        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing != null)
        {
            _generatedRoot = existing.transform;
            return;
        }

        var root = new GameObject(GeneratedRootName);
        _generatedRoot = root.transform;
    }

    private static Material GetOrCreateMaterial(Color color)
    {
        uint key = ColorKey(color);
        if (MaterialCache.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        var material = new Material(shader)
        {
            name = "SkillDebugMaterial_" + key
        };
        material.color = color;
        MaterialCache[key] = material;
        return material;
    }

    private static uint ColorKey(Color color)
    {
        Color32 c = color;
        return ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
    }

    private static void WarnMissingSkill(SkillId skillId)
    {
        if (!WarnedMissingSkills.Add(skillId)) return;
        Debug.LogWarning($"[{nameof(CombatSkillDebugIndicatorSystem)}] Missing debug visual spec for {skillId}.");
    }
#else
    public static void Show(Character self, SkillId skillId, string skillName, SkillExecutionContext context)
    {
    }
#endif
}
