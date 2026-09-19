using UnityEngine;

public enum SkillVfxShape
{
    Slash, Petal, Impact, Smoke, Sigil, Feather, Ray, Thorn, Lightning, Orb, Hand, Shield,
    Chain, Tome, Gauntlet, Skull, WindBlade, FlameTongue, BlastLobe, EnergyFilament,
    LightningBranch, SlashSmear, ExplosionCore, ExplosionBillow, ExplosionFlameFront,
    WindSpiral, FireCracks
}

public sealed class SkillVfxAtlas : ScriptableObject
{
    public Material Material;
    public Rect[] Regions;
    private static SkillVfxAtlas _shared;
    public static SkillVfxAtlas Shared
    {
        get { if (_shared == null) _shared = Resources.Load<SkillVfxAtlas>("Combat/Vfx/Art/Atlas"); return _shared; }
    }

    public static void Stamp(SkillVfxMesh mesh, SkillVfxShape shape, Vector3 center,
        Vector3 right, Vector3 up, Color color, float dissolve = 0)
    {
        if (color.a <= .002f || dissolve >= 1 || right.sqrMagnitude < .000001f || up.sqrMagnitude < .000001f) return;
        mesh.TextureQuad(center, right, up, color, Mathf.Clamp01(dissolve), Shared.Regions[(int)shape],
            shape == SkillVfxShape.Petal ? .24f : .09f);
    }

    public static void Ground(SkillVfxMesh mesh, SkillVfxShape shape, Vector3 center,
        float radius, float angle, Color color, float dissolve = 0)
    {
        if (color.a <= .002f || radius <= .001f || dissolve >= 1) return;
        Rect region = Shared.Regions[(int)shape];
        Vector3 x = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        Vector3 y = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
        const int steps = 4;
        for (int j = 0; j < steps; j++)
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)steps, v = j / (float)steps, d = 1f / steps;
                Vector3 a = center + x * (u * 2 - 1) + y * (v * 2 - 1);
                Vector3 b = a + x * d * 2, c = b + y * d * 2, e = a + y * d * 2;
                Rect uv = new(region.x + region.width * u, region.y + region.height * v, region.width * d, region.height * d);
                mesh.TextureQuad(mesh.Surface(a), mesh.Surface(b), mesh.Surface(c), mesh.Surface(e), color, dissolve, uv, .09f);
            }
    }
}
