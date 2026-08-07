using System.Collections.Generic;
using UnityEngine;

public static class SkillVfxProceduralFactory
{
    private const float VisualScale = 1.3f;
    private static readonly Color Sword = new(1f, 0.12f, 0.08f, 1f);
    private static readonly Color Shield = new(0.12f, 0.48f, 1f, 1f);
    // 黄・紫・オレンジは粒子シェーダ上で寄りやすいので、意図色を強調する。
    private static readonly Color Wand = new(1f, 0.95f, 0.05f, 1f);
    private static readonly Color Grimoire = new(0.82f, 0.18f, 1f, 1f);
    private static readonly Color Bible = new(1f, 0.72f, 0.05f, 1f);
    private static readonly Color Rosary = new(0.12f, 1f, 0.38f, 1f);

    private static readonly Dictionary<Color32, Material> ParticleMaterials = new();
    private static readonly Dictionary<Color32, Material> LineMaterials = new();

    public static GameObject CreateSacrificeSelf(Vector3 position, Transform parent, out float lifetime)
    {
        GameObject root = new("Vfx_Rosary_SacrificeThunder_Self");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        AddBurst(root.transform, Rosary, 30, 0.45f, 0.12f, 0.6f, 3f);
        lifetime = 0.9f;
        return root;
    }

    public static GameObject CreateSacrificeBolt(
        Vector3 from,
        Vector3 position,
        Transform parent,
        out float lifetime)
    {
        GameObject root = new("Vfx_Rosary_SacrificeThunder_Bolt");
        root.transform.SetParent(parent, false);
        AddLightning(root.transform, from + Vector3.up, position + Vector3.up, Rosary, 7);
        AddBurstAt(root.transform, position + Vector3.up, Rosary, 34, 0.45f, 0.15f, 0.55f, 3.5f);
        lifetime = 0.9f;
        return root;
    }

    public static bool TryCreate(
        SkillId skillId,
        Vector3 self,
        Vector3? target,
        Vector3? point,
        Transform parent,
        out GameObject root,
        out float lifetime)
    {
        Vector3 targetPosition = target ?? self;
        Vector3 pointPosition = point ?? targetPosition;
        root = new GameObject($"Vfx_{skillId}");
        root.transform.SetParent(parent, false);
        lifetime = 1f;

        switch (skillId)
        {
            case SkillId.Sword_Slash:
                root.transform.position = Midpoint(self, targetPosition, 1.1f);
                AddArc(root.transform, Sword, self, targetPosition, 1.4f);
                AddBurst(root.transform, Sword, 18, 0.3f, 0.12f, 0.45f, 3.2f);
                lifetime = 0.45f;
                break;
            case SkillId.Shield_Slash:
                root.transform.position = targetPosition + Vector3.up;
                AddBurst(root.transform, Shield, 24, 0.32f, 0.16f, 0.55f, 2.6f);
                AddRing(root.transform, Shield, 0.65f, 18, 0.35f);
                lifetime = 0.55f;
                break;
            case SkillId.Wand_Bolt:
                AddProjectile(root.transform, self + Vector3.up * 1.2f, targetPosition + Vector3.up, Wand, 0.16f);
                AddBurstAt(root.transform, targetPosition + Vector3.up, Wand, 22, 0.35f, 0.14f, 0.45f, 2.8f);
                lifetime = 0.55f;
                break;
            case SkillId.Wand_ArcaneBlast:
                AddProjectile(root.transform, self + Vector3.up * 1.3f, targetPosition + Vector3.up, Wand, 0.28f);
                AddBurstAt(root.transform, targetPosition + Vector3.up, Wand, 44, 0.55f, 0.22f, 0.65f, 4.2f);
                AddRingAt(root.transform, targetPosition, Wand, 1.2f, 26, 0.5f);
                lifetime = 0.8f;
                break;
            case SkillId.Wand_AreaBlast:
                root.transform.position = pointPosition;
                AddRing(root.transform, Wand, 2.6f, 44, 0.75f);
                AddBurst(root.transform, Wand, 52, 0.7f, 0.18f, 0.75f, 5f);
                lifetime = 1f;
                break;
            case SkillId.Wand_GodsHand:
                root.transform.position = targetPosition;
                AddColumn(root.transform, Wand, 1.8f, 5.5f, 80, 0.95f);
                AddRing(root.transform, Wand, 2.2f, 46, 0.8f);
                AddBurst(root.transform, Wand, 60, 0.8f, 0.25f, 0.8f, 5.5f);
                lifetime = 1.25f;
                break;
            case SkillId.Grimoire_Bolt:
                AddProjectile(root.transform, self + Vector3.up, targetPosition + Vector3.up, Grimoire, 0.14f);
                AddBurstAt(root.transform, targetPosition + Vector3.up, Grimoire, 18, 0.3f, 0.12f, 0.4f, 2.4f);
                lifetime = 0.5f;
                break;
            case SkillId.Grimoire_StrDebuff:
                CreateDebuff(root.transform, targetPosition, Grimoire, 0);
                lifetime = 5f;
                break;
            case SkillId.StatDebuff_INT:
                CreateDebuff(root.transform, targetPosition, Grimoire, 1);
                lifetime = 5f;
                break;
            case SkillId.StatDebuff_FAI:
                CreateDebuff(root.transform, targetPosition, Grimoire, 2);
                lifetime = 5f;
                break;
            case SkillId.StatDebuff_AGI:
                CreateDebuff(root.transform, targetPosition, Grimoire, 3);
                lifetime = 5f;
                break;
            case SkillId.Grimoire_Bind:
                root.transform.position = targetPosition;
                AddOrbit(root.transform, Grimoire, 1f, 28, 3f, 0.12f);
                AddColumn(root.transform, Grimoire, 0.8f, 2.2f, 18, 3f);
                lifetime = 3f;
                break;
            case SkillId.Grimoire_Poison:
                root.transform.position = targetPosition;
                AddRising(root.transform, Grimoire, 0.8f, 22, 5f, 0.16f);
                AddRing(root.transform, Grimoire, 0.8f, 18, 0.7f);
                lifetime = 5f;
                break;
            case SkillId.Grimoire_Stealth:
                root.transform.position = self;
                AddOrbit(root.transform, Grimoire, 0.8f, 26, 5f, 0.1f);
                AddRising(root.transform, Grimoire, 0.65f, 20, 5f, 0.22f);
                lifetime = 5f;
                break;
            case SkillId.Bible_Smite:
                root.transform.position = targetPosition;
                AddColumn(root.transform, Bible, 0.45f, 3.2f, 32, 0.55f);
                AddBurst(root.transform, Bible, 24, 0.38f, 0.12f, 0.45f, 3f);
                lifetime = 0.7f;
                break;
            case SkillId.Bible_StrBuff:
                CreateBuff(root.transform, targetPosition, Bible, 0);
                lifetime = 1.1f;
                break;
            case SkillId.Bible_IntBuff:
                CreateBuff(root.transform, targetPosition, Bible, 1);
                lifetime = 1.1f;
                break;
            case SkillId.Bible_FaiBuff:
                CreateBuff(root.transform, targetPosition, Bible, 2);
                lifetime = 1.1f;
                break;
            case SkillId.Bible_AgiBuff:
                CreateBuff(root.transform, targetPosition, Bible, 3);
                lifetime = 1.1f;
                break;
            case SkillId.Bible_Invulnerable:
                root.transform.position = self;
                AddOrbit(root.transform, Bible, 1.2f, 38, 3f, 0.15f);
                AddRing(root.transform, Bible, 1.3f, 32, 0.7f);
                lifetime = 3f;
                break;
            case SkillId.Bible_Gotsume:
                root.transform.position = targetPosition;
                AddOrbit(root.transform, Bible, 1f, 24, 5f, 0.18f);
                AddBurst(root.transform, Bible, 20, 0.35f, 0.1f, 0.5f, 1.3f);
                lifetime = 5f;
                break;
            case SkillId.Bible_CarryRush:
                AddProjectile(root.transform, self + Vector3.up * 0.7f, targetPosition + Vector3.up * 0.7f, Bible, 0.22f);
                AddRisingAt(root.transform, self, Bible, 0.9f, 32, 4f, 0.13f);
                lifetime = 4f;
                break;
            case SkillId.Rosary_Strike:
                root.transform.position = targetPosition + Vector3.up;
                AddBurst(root.transform, Rosary, 20, 0.3f, 0.14f, 0.45f, 2.8f);
                AddArc(root.transform, Rosary, self, targetPosition, 0.8f);
                lifetime = 0.5f;
                break;
            case SkillId.Rosary_DistantHeal:
                AddProjectile(root.transform, self + Vector3.up, targetPosition + Vector3.up, Rosary, 0.13f);
                CreateHeal(root.transform, targetPosition, 0.75f, 0.65f);
                lifetime = 0.8f;
                break;
            case SkillId.Rosary_CloseHeal:
                CreateHeal(root.transform, targetPosition, 1.2f, 0.85f);
                AddColumnAt(root.transform, targetPosition, Rosary, 0.7f, 2.5f, 28, 0.8f);
                lifetime = 1f;
                break;
            case SkillId.Rosary_Regeneration:
                root.transform.position = targetPosition;
                AddRising(root.transform, Rosary, 0.9f, 28, 5f, 0.14f);
                AddOrbit(root.transform, Rosary, 0.75f, 18, 5f, 0.1f);
                lifetime = 5f;
                break;
            case SkillId.Rosary_HealingArea:
                root.transform.position = pointPosition;
                AddOrbit(root.transform, Rosary, 2.8f, 46, 5f, 0.12f);
                AddRising(root.transform, Rosary, 2.4f, 40, 5f, 0.12f);
                lifetime = 5f;
                break;
            case SkillId.Rosary_SacrificeThunder:
                root.transform.position = self;
                AddBurst(root.transform, Rosary, 30, 0.45f, 0.12f, 0.6f, 3f);
                AddLightning(root.transform, self + Vector3.up, targetPosition + Vector3.up, Rosary, 7);
                AddBurstAt(root.transform, targetPosition + Vector3.up, Rosary, 34, 0.45f, 0.15f, 0.55f, 3.5f);
                lifetime = 0.9f;
                break;
            case SkillId.Shield_ShoulderGuard:
                root.transform.position = targetPosition;
                AddOrbit(root.transform, Shield, 1f, 28, 5f, 0.13f);
                AddProjectile(root.transform, self + Vector3.up, targetPosition + Vector3.up, Shield, 0.1f);
                lifetime = 5f;
                break;
            default:
                Object.Destroy(root);
                root = null;
                lifetime = 0f;
                return false;
        }

        AddRangedCastLine(skillId, self, target, point, root.transform);
        return true;
    }

    private static void CreateBuff(Transform root, Vector3 position, Color color, int variant)
    {
        root.position = position;
        AddColumn(root, color, 0.8f + variant * 0.05f, 2.4f, 32, 0.8f);
        AddRing(root, color, 0.9f, 26, 0.65f);
        AddRising(root, color, 0.75f, 18 + variant * 3, 0.9f, 0.1f);
    }

    private static void CreateDebuff(Transform root, Vector3 position, Color color, int variant)
    {
        root.position = position;
        AddRising(root, color, 0.75f + variant * 0.04f, 22, 5f, 0.18f);
        AddOrbit(root, color, 0.85f, 18 + variant * 2, 5f, 0.1f);
    }

    private static void CreateHeal(Transform root, Vector3 position, float radius, float duration)
    {
        root.position = position;
        AddRising(root, Rosary, radius, 30, duration, 0.13f);
        AddRing(root, Rosary, radius, 28, duration * 0.8f);
    }

    private static void AddRangedCastLine(
        SkillId skillId,
        Vector3 self,
        Vector3? target,
        Vector3? point,
        Transform root)
    {
        SkillBase skill = CombatSkillFactory.Create(skillId);
        if (skill == null ||
            skill.TargetKind == SkillTargetKind.None ||
            skill.TargetKind == SkillTargetKind.Self ||
            (!float.IsPositiveInfinity(skill.MaxRange) && skill.MaxRange <= 5f) ||
            HasBuiltInCastLine(skillId))
        {
            return;
        }

        Vector3? destination =
            skill.TargetKind == SkillTargetKind.Point || skill.TargetKind == SkillTargetKind.Area
                ? point ?? target
                : target;
        if (!destination.HasValue || (destination.Value - self).sqrMagnitude < 0.25f) return;

        GameObject castLine = AddLine(
            root,
            self + Vector3.up,
            destination.Value + Vector3.up,
            ResolveWeaponColor(skillId),
            0.12f,
            2);
        if (Application.isPlaying) Object.Destroy(castLine, 0.55f);
    }

    private static bool HasBuiltInCastLine(SkillId skillId)
    {
        return skillId == SkillId.Wand_Bolt ||
            skillId == SkillId.Wand_ArcaneBlast ||
            skillId == SkillId.Grimoire_Bolt ||
            skillId == SkillId.Rosary_DistantHeal ||
            skillId == SkillId.Rosary_SacrificeThunder ||
            skillId == SkillId.Shield_ShoulderGuard ||
            skillId == SkillId.Bible_CarryRush;
    }

    private static Color ResolveWeaponColor(SkillId skillId)
    {
        return skillId switch
        {
            SkillId.Sword_Slash => Sword,
            SkillId.Shield_Slash or SkillId.Shield_ShoulderGuard => Shield,
            SkillId.Wand_Bolt or
                SkillId.Wand_ArcaneBlast or
                SkillId.Wand_AreaBlast or
                SkillId.Wand_GodsHand => Wand,
            SkillId.Grimoire_Bolt or
                SkillId.Grimoire_StrDebuff or
                SkillId.StatDebuff_INT or
                SkillId.StatDebuff_FAI or
                SkillId.StatDebuff_AGI or
                SkillId.Grimoire_Bind or
                SkillId.Grimoire_Poison or
                SkillId.Grimoire_Stealth => Grimoire,
            SkillId.Bible_Smite or
                SkillId.Bible_StrBuff or
                SkillId.Bible_IntBuff or
                SkillId.Bible_FaiBuff or
                SkillId.Bible_AgiBuff or
                SkillId.Bible_Invulnerable or
                SkillId.Bible_Gotsume or
                SkillId.Bible_CarryRush => Bible,
            _ => Rosary,
        };
    }

    private static void AddProjectile(Transform parent, Vector3 from, Vector3 to, Color color, float width)
    {
        GameObject castLine = AddLine(parent, from, to, color, width, 2);
        if (Application.isPlaying) Object.Destroy(castLine, 0.55f);
        Vector3 midpoint = (from + to) * 0.5f;
        AddBurstAt(parent, midpoint, color, 12, 0.3f, width * 0.8f, 0.45f, 0.3f);
    }

    private static void AddLightning(Transform parent, Vector3 from, Vector3 to, Color color, int segments)
    {
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 jitter = i == 0 || i == segments
                ? Vector3.zero
                : new Vector3(Mathf.Sin(i * 12.91f), 0f, Mathf.Cos(i * 8.17f)) * 0.22f * VisualScale;
            points[i] = Vector3.Lerp(from, to, t) + jitter;
        }
        AddLine(parent, points, color, 0.16f);
        AddLine(parent, points, new Color(color.r, color.g, color.b, 0.45f), 0.34f);
    }

    private static void AddArc(Transform parent, Color color, Vector3 from, Vector3 to, float height)
    {
        const int segments = 8;
        height *= VisualScale;
        var points = new Vector3[segments + 1];
        Vector3 center = Midpoint(from, to, 1f);
        Vector3 forward = (to - from).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-70f, 70f, i / (float)segments) * Mathf.Deg2Rad;
            points[i] = center + right * Mathf.Sin(angle) * height + Vector3.up * Mathf.Cos(angle) * height;
        }
        AddLine(parent, points, color, 0.18f);
    }

    private static GameObject AddLine(
        Transform parent,
        Vector3 from,
        Vector3 to,
        Color color,
        float width,
        int segments)
    {
        var points = new Vector3[Mathf.Max(2, segments)];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = Vector3.Lerp(from, to, i / (float)(points.Length - 1));
        }
        return AddLine(parent, points, color, width);
    }

    private static GameObject AddLine(Transform parent, Vector3[] points, Color color, float width)
    {
        GameObject go = new("Line");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width * VisualScale;
        line.endWidth = width * 0.15f * VisualScale;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.sharedMaterial = GetLineMaterial(color);
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.05f);
        return go;
    }

    private static void AddRing(Transform parent, Color color, float radius, int count, float lifetime)
    {
        ParticleSystem system = CreateParticles(parent, color, count, lifetime, 0.12f, 0f);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * VisualScale;
        shape.radiusThickness = 0f;
        system.Emit(count);
    }

    private static void AddRingAt(Transform parent, Vector3 position, Color color, float radius, int count, float lifetime)
    {
        Transform child = CreateAnchor(parent, position);
        AddRing(child, color, radius, count, lifetime);
    }

    private static void AddOrbit(Transform parent, Color color, float radius, int count, float lifetime, float size)
    {
        ParticleSystem system = CreateParticles(parent, color, count, 0.9f, size, 0f);
        var main = system.main;
        main.loop = true;
        main.duration = lifetime;
        var emission = system.emission;
        emission.rateOverTime = count / Mathf.Max(0.1f, lifetime);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * VisualScale;
        shape.radiusThickness = 0.15f;
        system.Play();
    }

    private static void AddRising(Transform parent, Color color, float radius, int count, float duration, float size)
    {
        ParticleSystem system = CreateParticles(parent, color, count, 0.9f, size, 0.65f);
        var main = system.main;
        main.loop = duration > 1.2f;
        main.duration = Mathf.Max(0.1f, duration);
        var emission = system.emission;
        emission.rateOverTime = count / Mathf.Max(0.2f, duration);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, radius * 2f, 1.6f) * VisualScale;
        system.Play();
        if (!main.loop) system.Emit(count);
    }

    private static void AddRisingAt(
        Transform parent, Vector3 position, Color color, float radius, int count, float duration, float size)
    {
        Transform child = CreateAnchor(parent, position);
        AddRising(child, color, radius, count, duration, size);
    }

    private static void AddColumn(
        Transform parent, Color color, float radius, float height, int count, float lifetime)
    {
        ParticleSystem system = CreateParticles(
            parent,
            color,
            count,
            lifetime,
            0.15f,
            height * 0.45f);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, radius * 2f, height) * VisualScale;
        system.Emit(count);
    }

    private static void AddColumnAt(
        Transform parent, Vector3 position, Color color, float radius, float height, int count, float lifetime)
    {
        Transform child = CreateAnchor(parent, position);
        AddColumn(child, color, radius, height, count, lifetime);
    }

    private static void AddBurst(
        Transform parent, Color color, int count, float lifetime, float size, float sizeMax, float speed)
    {
        ParticleSystem system = CreateParticles(parent, color, count, lifetime, size, speed);
        var main = system.main;
        main.startSize = new ParticleSystem.MinMaxCurve(size * VisualScale, sizeMax * VisualScale);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f * VisualScale;
        system.Emit(count);
    }

    private static void AddBurstAt(
        Transform parent, Vector3 position, Color color, int count, float lifetime, float size, float sizeMax, float speed)
    {
        Transform child = CreateAnchor(parent, position);
        AddBurst(child, color, count, lifetime, size, sizeMax, speed);
    }

    private static ParticleSystem CreateParticles(
        Transform parent, Color color, int maxParticles, float lifetime, float size, float speed)
    {
        GameObject go = new("Particles");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSize = size * VisualScale;
        main.startSpeed = speed * VisualScale;
        main.startColor = color;
        main.maxParticles = Mathf.Max(8, maxParticles * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = FadeGradient(color);

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetParticleMaterial(color);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return system;
    }

    private static Transform CreateAnchor(Transform parent, Vector3 worldPosition)
    {
        GameObject go = new("Anchor");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        return go.transform;
    }

    private static Gradient FadeGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 0.2f),
                new GradientColorKey(color, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.12f),
                new GradientAlphaKey(color.a, 0.72f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Material GetParticleMaterial(Color color)
    {
        Color32 key = color;
        if (ParticleMaterials.TryGetValue(key, out Material material)) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("No compatible shader found for procedural skill VFX particles.");
            return null;
        }
        material = new Material(shader) { name = $"RuntimeVfxParticle_{ColorUtility.ToHtmlStringRGBA(color)}" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        ParticleMaterials[key] = material;
        return material;
    }

    private static Material GetLineMaterial(Color color)
    {
        Color32 key = color;
        if (LineMaterials.TryGetValue(key, out Material material)) return material;

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("No compatible shader found for procedural skill VFX lines.");
            return null;
        }
        material = new Material(shader) { name = $"RuntimeVfxLine_{ColorUtility.ToHtmlStringRGBA(color)}" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        LineMaterials[key] = material;
        return material;
    }

    private static Vector3 Midpoint(Vector3 a, Vector3 b, float height)
    {
        return (a + b) * 0.5f + Vector3.up * height;
    }
}
