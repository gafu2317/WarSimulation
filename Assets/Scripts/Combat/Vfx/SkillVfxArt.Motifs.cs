using UnityEngine;

public static partial class SkillVfxArt
{
    private static float RevealScale(float t)
    {
        float u = Mathf.Clamp01(t / .32f) - 1;
        return 1 + 3.6f * u * u * u + 2.6f * u * u;
    }

    private static void Glint(SkillVfxMesh m, Vector3 p, float size, float t, Color c)
    {
        if (t < 0 || t > .6f) return;
        float f = Out(t, .1f, .6f), reach = size * (.4f + Ease(t, .08f) * .6f);
        Sprite(m, SkillVfxShape.Ray, p, reach * .34f, reach * 1.25f, 0, A(c, f));
        Sprite(m, SkillVfxShape.Ray, p, reach * .13f, reach * .7f, 90, A(White, f));
    }

    private static void Sparks(SkillVfxMesh m, Vector3 p, float t, float radius, float angle, float spread, Color c, int count = 5)
    {
        if (t < 0 || t > .7f) return;
        float travel = Ease(t, .7f), fade = Out(t, .16f, .7f);
        for (int i = 0; i < count; i++)
        {
            float a = (angle + spread * (i / (float)Mathf.Max(1, count - 1) - .5f)) * Mathf.Deg2Rad;
            Vector3 d = m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a);
            float length = radius * (.75f + .25f * Mathf.Sin(i * 5.7f));
            Vector3 tip = p + d * length * travel;
            m.Stroke(tip - d * length * .34f * fade, tip, .105f * fade, A(c, fade), 0);
        }
    }

    private static void LightBand(SkillVfxMesh m, Vector3 center, float width, float height,
        float angle, float span, Color c)
    {
        if (width <= .05f || height <= 0 || c.a <= .002f) return;
        m.Crescent(center, m.Right, m.Up * (height / width), width, .16f,
            angle, span, A(c, c.a * .65f));
        m.Crescent(center, m.Right, m.Up * (height / width), width - .045f, .045f,
            angle, span, A(Color.Lerp(c, White, .6f), c.a));
    }

    private static void Bead(SkillVfxMesh m, Vector3 p, float size, Color c)
    {
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2 / 16, b = (i + 1) * Mathf.PI * 2 / 16;
            Vector3 x = (m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a)) * size;
            Vector3 y = (m.Right * Mathf.Cos(b) + m.Up * Mathf.Sin(b)) * size;
            Color shade = c * (.82f + .18f * Mathf.Sin(a)); shade.a = c.a;
            m.Triangle(p - m.Right * size * .2f + m.Up * size * .2f, p + x, p + y, shade);
        }
        m.Crescent(p, m.Right, m.Up, size * .65f, size * .22f, 65, 85, A(White, c.a * .9f));
    }

    private static void ChainBetween(SkillVfxMesh m, Vector3 a, Vector3 b, float thickness, Color c)
    {
        Vector3 d = b - a;
        float angle = Mathf.Atan2(Vector3.Dot(d, m.Up), Vector3.Dot(d, m.Right)) * Mathf.Rad2Deg;
        Sprite(m, SkillVfxShape.Chain, (a + b) * .5f, d.magnitude * .59f, thickness * 2.3f, angle, c);
    }

    private static void BrokenMotif(SkillVfxMesh m, SkillVfxShape shape, Vector3 p, float size, float split, Color c)
    {
        Rect full = SkillVfxAtlas.Shared.Regions[(int)shape];
        for (int side = -1; side <= 1; side += 2)
        {
            float left = side < 0 ? 0 : .52f;
            Rect uv = new(full.x + full.width * left, full.y, full.width * .48f, full.height);
            float angle = side * split * .24f;
            Vector3 x = m.Right * Mathf.Cos(angle) + m.Up * Mathf.Sin(angle);
            Vector3 y = -m.Right * Mathf.Sin(angle) + m.Up * Mathf.Cos(angle);
            Vector3 center = p + m.Right * side * (.52f * size + split * .18f) - m.Up * split * .14f;
            m.TextureQuad(center, x * size * .48f, y * size, c, 0, uv, .09f);
        }
    }

    private static void Halo(SkillVfxMesh m, Vector3 p, float size, Color c, float broken = 0)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 center = p + m.Right * side * broken * .25f - m.Up * broken * .2f;
            float angle = side * broken * .45f;
            Vector3 x = m.Right * Mathf.Cos(angle) + m.Up * Mathf.Sin(angle);
            Vector3 y = -m.Right * Mathf.Sin(angle) + m.Up * Mathf.Cos(angle);
            m.Crescent(center, x, y * .32f, size, .12f, side < 0 ? 95 : -85,
                180 - broken * 22, c);
        }
        if (broken < .01f)
            for (int i = -1; i <= 1; i++)
            {
                Vector3 a = p + m.Right * i * size * .5f + m.Up * size * .48f;
                m.Stroke(a, a + m.Up * size * (i == 0 ? .28f : .18f), .045f, A(c, c.a * .8f));
            }
    }

    private static void Heart(SkillVfxMesh m, Vector3 p, float size, Color c)
    {
        if (size <= .001f || c.a <= .002f) return;
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
            Vector3 v = HeartPoint(m, a) * size, w = HeartPoint(m, b) * size;
            m.Triangle(p, p + v, p + w, Color.Lerp(c, A(White, c.a), i > 16 ? .06f : .25f));
        }
    }

    private static Vector3 HeartPoint(SkillVfxMesh m, float a)
        => m.Right * Mathf.Pow(Mathf.Sin(a), 3) + m.Up *
            ((13 * Mathf.Cos(a) - 5 * Mathf.Cos(2 * a) - 2 * Mathf.Cos(3 * a) - Mathf.Cos(4 * a)) / 16f);

    private static void Sprout(SkillVfxMesh m, Vector3 root, float size, float growth, Color c)
    {
        Vector3 top = root + m.Up * size * growth;
        Ribbon(m, root, top, size * .08f, .025f, c);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 leafUp = (m.Up * .6f + m.Right * side * .8f).normalized;
            Vector3 leafRight = m.Right * .6f - m.Up * side * .8f;
            SkillVfxAtlas.Stamp(m, SkillVfxShape.Petal, top + leafUp * size * .2f * growth,
                leafRight * size * .25f * growth, leafUp * size * .46f * growth, c);
        }
    }
}
