using UnityEngine;

public static partial class SkillVfxArt
{
    private static void Rosary(SkillVfxMesh m, SkillId id, Vector3 self, Vector3 foot, Vector3 point,
        Vector3 p, float t, float radius, Color c)
    {
        if (id is SkillId.Rosary_DistantHeal or SkillId.Rosary_CloseHeal or
            SkillId.Rosary_Regeneration or SkillId.Rosary_HealingArea)
            HealingSparkles(m, id == SkillId.Rosary_HealingArea ? point + Vector3.up * .25f : p, t,
                id == SkillId.Rosary_HealingArea ? radius : 1.25f);
        switch (id)
        {
            case SkillId.Rosary_Strike:
                Vector3 normal = Vector3.Cross(m.Right, m.Up).normalized;
                Vector3 origin = self + Vector3.up + m.Up * .12f - normal * 1.2f;
                Vector3 beam = p + m.Up * .12f - normal * .4f - origin;
                float fire = Ease(t, .1f), beamFade = Out(t, .18f, .62f);
                Vector3 end = origin + beam * fire;
                Vector3 beamDirection = end - origin;
                Vector3 beamDirectionNormal = beamDirection.sqrMagnitude > .001f ? beamDirection.normalized : m.Right;
                Vector3 beamSide = Vector3.Cross(beamDirectionNormal, normal).normalized;
                if (beamSide.sqrMagnitude < .001f) beamSide = m.Up;
                Vector3 outer = beamSide * .3f, inner = beamSide * .085f;
                m.Quad(origin - outer, origin + outer, end + outer * .18f, end - outer * .18f,
                    A(c, beamFade * .72f));
                m.Quad(origin - inner, origin + inner, end + inner * .3f, end - inner * .3f,
                    A(Color.white, beamFade));
                Vector3 beamCenter = (origin + end) * .5f;
                SkillVfxAtlas.Stamp(m, SkillVfxShape.EnergyFilament, beamCenter,
                    beamDirection * .5f, beamSide * .13f, A(Color.white, beamFade));
                Glint(m, origin, .72f, t, c);
                Glint(m, p, 1.5f, t - .05f, c);
                Sparks(m, p, t - .06f, 1.45f, 60, 210, c, 4);
                Wave(m, foot, .25f + fire * .9f, .08f * beamFade, A(c, beamFade));
                break;
            case SkillId.Rosary_DistantHeal:
                float f = Out(t, .2f, 1.2f), receive = Ease(t, .45f);
                Glint(m, p, 1.2f, t - .1f, c);
                Heart(m, p + m.Up * receive * .35f, .8f * (1 - receive * .25f), A(c, f));
                for (int i = 0; i < 3; i++)
                {
                    float u = Mathf.Clamp01((t - .15f - i * .09f) / .7f);
                    Heart(m, p + m.Right * (i - 1) * .42f + m.Up * (u * 1.2f),
                        .22f, A(c, Mathf.Sin(u * Mathf.PI) * .9f));
                }
                for (int side = -1; side <= 1; side += 2)
                    m.Crescent(p, m.Right * side, m.Up, .35f + receive * .45f, .045f,
                        -70, 140, A(c, f * .6f));
                break;
            case SkillId.Rosary_CloseHeal: Heal(m, foot, t, c); break;
            case SkillId.Rosary_Regeneration:
                float appear = In(t), cycle = Mathf.Repeat(t * .35f, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    float u = Mathf.Repeat(t * .35f + (side + 1) * .25f, 1);
                    Vector3 root = foot - Vector3.Cross(m.Right, m.Up) * .6f + m.Right * side * .65f + m.Up * .15f;
                    Sprout(m, root, .9f, .4f + .6f * Mathf.Sin(u * Mathf.PI), A(c, appear * .65f));
                    Ribbon(m, root, p + m.Up * .65f, side * .75f, .12f,
                        A(c, appear * Mathf.Sin(u * Mathf.PI) * .55f));
                }
                LightBand(m, p - m.Up * .35f, 1.2f, .45f, t * 85, 260, A(c, appear * .8f));
                Glint(m, p + m.Up * .8f, 1.0f, t - .2f, c);
                Sprout(m, p + m.Up * .45f, 1.25f, .6f + .4f * Ease(cycle, .65f), A(c, appear * .9f));
                Heart(m, p + m.Up * (1.4f + cycle * .8f), .3f, A(c, appear * Mathf.Sin(cycle * Mathf.PI)));
                break;
            case SkillId.Rosary_HealingArea:
                float grow = In(t, .35f);
                if (t < .85f) Wave(m, point, radius * Ease(t, .55f), .11f,
                    A(c, Out(t, .3f, .85f)), true);
                Wave(m, point, radius, .11f, A(c, grow * .8f), true);
                for (int i = 0; i < 2; i++)
                {
                    float u = Mathf.Repeat(t * .22f + i * .5f, 1);
                    Wave(m, point, radius * u, .08f, A(c, grow * Mathf.Sin(u * Mathf.PI) * .65f), true);
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3 + t * .06f;
                    Vector3 q = m.Surface(point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * .87f);
                    Heart(m, q + Vector3.up * (1.0f + .25f * Mathf.Sin(t * 1.5f + i)), .24f, A(c, grow * .85f));
                    float breathe = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 1.2f + i)), 3);
                    Sprite(m, SkillVfxShape.Ray, q + Vector3.up * .8f, .3f, 1.3f,
                        180, A(c, grow * breathe * .65f));
                    Sprout(m, q + Vector3.up * .1f, .85f, .8f + .2f * Mathf.Sin(t * 1.5f + i), A(c, grow * .7f));
                }
                break;
            case SkillId.Rosary_SacrificeThunder:
                bool sacrifice = (self - foot).sqrMagnitude < .1f;
                float flash = Mathf.Max(Out(t, .02f, .2f), In(t - .16f, .02f) * Out(t, .2f, .5f));
                if (sacrifice)
                {
                    float collapse = 1 - Ease(t, .3f);
                    Halo(m, p + m.Up * (1.3f - (1 - collapse) * .6f), .8f, A(Gold, collapse), 1 - collapse);
                    Sprite(m, SkillVfxShape.Smoke, p, .9f, 1.2f, 0, A(Dark, Out(t, .3f, 1.15f) * .45f), In(t - .3f, .8f));
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2)
                        Sprite(m, SkillVfxShape.Lightning, foot + m.Up * 1.8f + m.Right * side * .65f,
                            1.25f, 2.9f, side * 16, A(c, flash));
                    Glint(m, p, 2.7f, t, c);
                    Sparks(m, p, t - .16f, 3.1f, 90, 300, c, 9);
                    Sprite(m, SkillVfxShape.Lightning, foot + Vector3.up * 3.5f, t < .17f ? 2.2f : 2.75f, 4.2f,
                        t < .17f ? -3 : 4, A(White, flash));
                    Sprite(m, SkillVfxShape.Ray, foot + Vector3.up * 2.7f, .3f, 2.9f, 0,
                        A(c, Out(t, .2f, 1.35f) * .85f), In(t - .5f, .8f));
                    float crackle = t < .18f ? 0 :
                        (.65f + .35f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 31)), 4)) * Out(t, .42f, 1.5f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = t * (5 + i) + i * 1.7f;
                        Vector3 q = p + m.Right * Mathf.Cos(a) * (.75f + i * .13f) +
                            m.Up * Mathf.Sin(a * 1.3f) * (.58f + i * .1f);
                        Sprite(m, SkillVfxShape.LightningBranch, q, .82f, .96f,
                            i * 57 + t * (82 + i * 7), A(i % 2 == 0 ? White : c, crackle));
                    }
                }
                SkillVfxAtlas.Ground(m, SkillVfxShape.Impact, foot, .5f + Ease(t, .35f) * 1.5f,
                    0, A(sacrifice ? Gold : c, Out(t, .12f, .8f)), In(t - .2f, .6f));
                break;
        }
    }

    private static void Heal(SkillVfxMesh m, Vector3 foot, float t, Color c)
    {
        float open = In(t, .26f), spring = Mathf.Sin(Mathf.Clamp01(t / .5f) * Mathf.PI) * .23f;
        float fade = Out(t, .65f, 1.55f), rise = In(t - .55f, 1.05f) * .8f;
        Vector3 basePoint = foot + Vector3.up * .15f;
        for (int j = 0; j < 5; j++)
        {
            int i = j == 0 ? -2 : j == 1 ? 2 : j == 2 ? -1 : j == 3 ? 1 : 0;
            float angle = i * (.16f + open * .34f + spring);
            Vector3 up = m.Up * Mathf.Cos(angle) + m.Right * Mathf.Sin(angle);
            Vector3 right = m.Right * Mathf.Cos(angle) - m.Up * Mathf.Sin(angle);
            float length = (2.5f + .8f * open) * (1 - .12f * Mathf.Abs(i));
            Vector3 root = basePoint + m.Right * i * .12f * open + m.Up * rise;
            Color petal = A(Color.Lerp(c, White, j == 4 ? .5f : .2f), (j == 4 ? .28f : .72f) * open * fade);
            SkillVfxAtlas.Stamp(m, SkillVfxShape.Petal, root + up * length * .49f,
                right * (.28f + .3f * open) * 1.5f, up * length * .72f, petal, (1 - fade) * .7f);
        }
        LightBand(m, basePoint + m.Up * .35f, 1.65f * open, .6f, t * 130, 300, A(c, open * fade * .8f));
        Glint(m, basePoint + m.Up * 1.3f, 1.2f, t - .16f, c);
        for (int side = -1; side <= 1; side += 2)
        {
            float u = Ease(t, .7f);
            Vector3 a = basePoint + m.Right * side * .95f;
            Vector3 b = basePoint + m.Right * side * .25f + m.Up * (2.4f * u);
            Ribbon(m, a, b, side * .7f, .16f, A(c, open * Out(t, .5f, 1.25f) * .8f));
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 petal = basePoint + m.Right * side * (1.1f * open) + m.Up * .25f;
            Sprite(m, SkillVfxShape.Petal, petal, .65f, 1.1f, side * -65,
                A(c, open * fade * .4f), (1 - fade) * .7f);
            Glint(m, basePoint + m.Right * side * .95f + m.Up * 1.2f, .4f, t - .3f, c);
        }
        float wave = Mathf.Clamp01(t / .8f);
        Wave(m, foot, .3f + wave * 1.3f, .06f * (1 - wave), A(c, (1 - wave) * .8f));
        for (int i = 0; i < 4; i++)
        {
            float u = Mathf.Clamp01((t - .16f - i * .09f) / .95f), alpha = Mathf.Sin(u * Mathf.PI);
            Vector3 p = basePoint + m.Right * Mathf.Sin(i * 2.4f) * (.55f + .25f * u) + m.Up * (.25f + u * 2.2f);
            Orb(m, p, .07f * alpha, A(White, alpha));
        }
    }

    private static void HealingSparkles(SkillVfxMesh m, Vector3 center, float t, float radius)
    {
        for (int i = 0; i < 5; i++)
        {
            float cycle = Mathf.Repeat(t * .72f + i * .19f, 1);
            float a = i * 2.4f + t * .45f;
            Vector3 q = center + m.Right * Mathf.Sin(a) * radius * (.35f + cycle * .45f) +
                m.Up * (.15f + cycle * 1.55f);
            float shine = Mathf.Sin(cycle * Mathf.PI);
            ConcaveSparkle(m, q, .12f + shine * .12f, .26f + shine * .22f,
                i * 41, A(Color.white, shine));
        }
    }

    private static void ConcaveSparkle(SkillVfxMesh m, Vector3 center, float width, float height,
        float angle, Color color)
    {
        float rotation = angle * Mathf.Deg2Rad;
        Vector3 Point(int index)
        {
            float a = rotation + index * Mathf.PI * .25f;
            float inset = index % 2 == 0 ? 1f : .22f;
            return center + m.Right * Mathf.Cos(a) * width * inset +
                m.Up * Mathf.Sin(a) * height * inset;
        }
        Vector3 previous = Point(7);
        for (int i = 0; i < 8; i++)
        {
            Vector3 next = Point(i);
            m.Triangle(center, previous, next, color);
            previous = next;
        }
    }
}
