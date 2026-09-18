using UnityEngine;

public static partial class SkillVfxArt
{
    private static void QuickSlash(SkillVfxMesh m, Vector3 p, Vector3 forward, float t, Color c)
    {
        float strike = Ease(t, .055f);
        float fade = Out(t, .08f, .36f);
        float side = Vector3.Dot(forward, m.Right) < 0 ? -1 : 1;
        float angle = Mathf.Lerp(-58f, 38f, strike) * Mathf.Deg2Rad;
        Vector3 x = (m.Right * Mathf.Cos(angle) + m.Up * Mathf.Sin(angle)) * side;
        Vector3 y = -m.Right * Mathf.Sin(angle) + m.Up * Mathf.Cos(angle);
        Vector3 center = p + x * .25f;
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center, x * 1.7f, y * .88f,
            A(c, fade * .9f), 1 - fade);
        m.Crescent(center, x, y, 1.75f, .11f * fade, 72, 145, A(White, fade));
        Sprite(m, SkillVfxShape.Impact, p, .22f + strike * .35f, .22f + strike * .35f,
            0, A(White, Out(t, .02f, .12f)));
    }

    private static void StrongSlash(SkillVfxMesh m, Vector3 p, Vector3 forward, float t, Color c)
    {
        Slash(m, p, forward, t, c);
        float impact = Out(t, .06f, .3f);
        float side = Vector3.Dot(forward, m.Right) < 0 ? -1 : 1;
        m.Crescent(p, m.Right * side, m.Up, 2.7f * Ease(t, .12f), .24f * impact,
            70, 150, A(White, impact * .8f));
        Wave(m, p, 1.1f * Ease(t, .2f), .08f * impact, A(c, impact * .55f));
    }

    private static void Slash(SkillVfxMesh m, Vector3 p, Vector3 forward, float t, Color c)
    {
        float strike = Ease(t, .085f), tail = 1 - Out(t, .11f, .52f);
        float side = Vector3.Dot(forward, m.Right) < 0 ? -1 : 1;
        float a = Mathf.Lerp(-78, 25, strike) * Mathf.Deg2Rad;
        Vector3 x = (m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a)) * side;
        Vector3 y = -m.Right * Mathf.Sin(a) + m.Up * Mathf.Cos(a);
        Vector3 center = p + x * .35f;
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center - x * .12f - y * .1f,
            x * 1.95f, y * 1.3f, A(c, (1 - tail) * .28f), tail * .65f);
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center, x * (1.8f + strike * 1.05f),
            y * (1.55f - tail * .35f), A(c, (1 - tail) * .9f), tail * .92f);
        m.Crescent(center, x, y, 2.35f, .19f * (1 - tail), 88, 160, A(White, 1 - tail));
        m.Crescent(center - y * .18f, x, y, 2.2f, .04f * (1 - tail), 65, 150, A(c, (1 - tail) * .55f));
        if (t > .07f)
            SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center - y * .3f,
                x * (1.85f + tail * .5f), y * .92f, A(c, Out(t, .13f, .42f) * .3f), In(t - .12f, .3f));
        Sparks(m, p, t, 2.2f, side > 0 ? 25 : 155, 70, White, 4);
        Sprite(m, SkillVfxShape.Impact, p, .3f + strike * .5f, .3f + strike * .5f, 15,
            A(White, Out(t, .025f, .17f)), tail);
    }

    private static void ShieldStrike(SkillVfxMesh m, Vector3 p, Vector3 foot, Vector3 forward, float t, Color c)
    {
        float hit = Ease(t, .085f), fade = Out(t, .12f, .65f);
        Glint(m, p, .85f, t - .04f, c);
        Sparks(m, p, t - .04f, 2.0f, 90, 190, c, 5);
        for (int side = -1; side <= 1; side += 2)
            m.Crescent(p, m.Right * side, m.Up, .55f + hit * 1.5f, .16f * fade,
                -65, 130, A(c, Out(t, .13f, .5f) * .8f));
        Vector3 shield = p - forward * (1 - hit) * .65f;
        Sprite(m, SkillVfxShape.Shield, shield, 1.08f, 1.3f, -8 * (1 - hit),
            A(c, Out(t, .12f, .4f) * .95f), In(t - .2f, .2f));
        Sprite(m, SkillVfxShape.Impact, p + forward * .35f, .3f + hit * .9f, .25f + hit * .35f,
            0, A(White, Out(t, .08f, .32f)), 1 - fade);
        for (int i = -1; i <= 1; i += 2)
            Sprite(m, SkillVfxShape.Smoke, foot + m.Right * i * hit + Vector3.up * .2f,
                .6f, .35f, i * 20, A(c, fade * .33f), 1 - fade);
    }

    private static void ShoulderGuard(SkillVfxMesh m, Vector3 self, Vector3 foot, float t, Color c)
    {
        float appear = In(t, .22f), arrive = Ease(t, .32f);
        Vector3 p = foot + Vector3.up * 1.05f - Vector3.Cross(m.Right, m.Up) * .8f;
        float side = Vector3.Dot(self - foot, m.Right) < 0 ? -1 : 1;
        Vector3 shield = Vector3.Lerp(self + Vector3.up, p + m.Right * side * .63f, arrive);
        Vector3 link = self + Vector3.up * .6f;
        Ribbon(m, link, shield, .5f, .13f, A(c, appear * .55f));
        float flow = Mathf.Repeat(t * .65f, 1);
        Glint(m, Vector3.Lerp(link, shield, flow), .38f, Mathf.Repeat(t, 1.5f), c);
        if (t < .65f)
            Sprite(m, SkillVfxShape.Shield, shield - m.Right * side * .2f, 1.02f, 1.24f,
                side * -8, A(c, appear * Out(t, .18f, .65f) * .22f));
        LightBand(m, shield, 1.05f, 1.4f, -t * 75, 230, A(c, appear * .75f));
        Glint(m, shield + m.Up * .5f, .75f, t - .22f, c);
        Sprite(m, SkillVfxShape.Shield, shield, 1.0f, 1.35f, side * -8, A(c, appear * .85f));
        m.Crescent(shield, m.Right, m.Up * 1.2f, .7f, .035f, 15, 150,
            A(White, appear * (.4f + .15f * Mathf.Sin(t * 2))));
    }

    private static void IronWall(SkillVfxMesh m, Vector3 foot, Vector3 p, float t, Color c)
    {
        float appear = In(t, .2f);
        float pulse = .95f + .05f * Mathf.Sin(t * 4f);
        Sprite(m, SkillVfxShape.Shield, p + m.Up * .15f, 1.25f * pulse, 1.5f * pulse,
            0, A(c, appear * .9f));
        for (int side = -1; side <= 1; side += 2)
            Sprite(m, SkillVfxShape.Shield, p + m.Right * side * .85f + m.Up * .1f,
                .55f, .78f, side * -12f, A(c, appear * .6f));
        Wave(m, foot, 1.25f + .12f * Mathf.Sin(t * 3f), .07f, A(c, appear * .7f));
        LightBand(m, p, 1.1f, 1.25f, t * 35f, 240, A(White, appear * .4f));
    }

    private static void Taunt(SkillVfxMesh m, Vector3 foot, Vector3 p, float t, Color c)
    {
        float appear = In(t, .12f);
        float pulse = .9f + .1f * Mathf.Sin(t * 5f);
        Halo(m, p + m.Up * .15f, 1.25f * pulse, A(c, appear * .8f));
        Sprite(m, SkillVfxShape.Shield, p + m.Up * .15f, 1.05f, 1.3f,
            0, A(c, appear * .9f));
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f + .25f;
            Vector3 offset = m.Right * Mathf.Cos(angle) * 1.25f + m.Up * Mathf.Sin(angle) * 1.25f;
            Sprite(m, SkillVfxShape.Ray, p + offset, .12f, .55f, angle * Mathf.Rad2Deg,
                A(c, appear * .8f));
        }
        Wave(m, foot, 1.4f * pulse, .1f, A(c, appear * .65f));
    }

    private static void BoltHit(SkillVfxMesh m, Vector3 p, float t, Color c)
    {
        float e = Ease(t, .07f), f = Out(t, .06f, .55f);
        Glint(m, p, 1.05f, t, c);
        Sparks(m, p, t, 1.3f, 35, 110, c, 3);
        Sprite(m, SkillVfxShape.Impact, p, .2f + e * .85f, .2f + e * .85f, 25, A(c, f), 1 - f);
        m.Crescent(p, m.Right, m.Up, .2f + e * .65f, .045f, t * 220, 250, A(c, f * .7f));
        Orb(m, p, .18f * (1 - e) + .04f, A(White, Out(t, .02f, .2f)));
    }

    private static void ArcaneBlast(SkillVfxMesh m, Vector3 p, Vector3 foot, float t, Color c)
    {
        Glint(m, p, 1.8f, t, c);
        Sparks(m, p, t - .06f, 3.0f, 90, 320, c, 6);
        float expand = Ease(Mathf.Max(0, t - .06f), .24f), fade = Out(t, .3f, 1.6f);
        Sprite(m, SkillVfxShape.Impact, p, .4f + expand * 2.45f, .65f + expand * 1.85f,
            -15 + t * 20, A(c, fade), 1 - fade);
        Orb(m, p, .6f * Out(t, .03f, .2f), A(White, Out(t, .08f, .3f)));
        for (int i = 0; i < 3; i++)
            m.Crescent(p, m.Right, m.Up * .7f, .55f + expand * (1.8f + i * .3f),
                .2f * fade, i * 120 + t * 150, 105, A(i == 0 ? White : c, fade * .8f));
        if (t > .14f)
            Sprite(m, SkillVfxShape.Impact, p, .65f + expand * 2.3f, .65f + expand * 1.6f,
                12, A(c, Out(t, .3f, .95f) * .2f), In(t - .3f, .65f));
        Wave(m, foot, .3f + expand * 1.8f, .09f * fade, A(White, fade * .45f));
    }

    private static void AreaBlast(SkillVfxMesh m, Vector3 point, float t, float radius, Color c)
    {
        Glint(m, point + Vector3.up * .8f, 1.7f, t, c);
        Sparks(m, point + Vector3.up * .3f, t - .06f, radius * .8f, 90, 145, c, 6);
        float expand = Ease(t, .36f), fade = Out(t, .25f, 1.6f);
        Sprite(m, SkillVfxShape.Impact, point + Vector3.up * (.6f + .6f * expand) - Vector3.Cross(m.Right, m.Up) * .75f,
            radius * expand, .65f + expand * 1.2f, -20, A(c, fade * .85f), 1 - fade);
        SkillVfxAtlas.Ground(m, SkillVfxShape.Impact, point, radius * expand, .2f, A(c, fade * .85f), 1 - fade);
        Wave(m, point, radius * expand, .12f * fade, A(White, fade * .8f), true);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2 / 5;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius * .5f * expand;
            Vector3 basePoint = m.Surface(point + offset);
            float height = Mathf.Sin(Mathf.Clamp01(t / 1.5f) * Mathf.PI) * 2.0f;
            Glint(m, basePoint + Vector3.up * .25f, .65f, t - .12f - i * .04f, c);
            Sprite(m, SkillVfxShape.Ray, basePoint + Vector3.up * height, .45f, 1.4f,
                -i * 12, A(i % 2 == 0 ? White : c, fade * .55f), 1 - fade);
            if (i < 3) Sprite(m, SkillVfxShape.Smoke, basePoint + Vector3.up * .3f,
                .45f + .3f * expand, .45f, i * 90, A(c, fade * .3f), 1 - fade);
        }
    }

    private static void Fist(SkillVfxMesh m, Vector3 contact, float scale, Color c)
    {
        // The fist's lower knuckles sit at UV y≈0.16. Keep the fist rigid at contact.
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Hand, contact + m.Up * (.68f * scale) - Vector3.Cross(m.Right, m.Up) * .9f,
            m.Right * scale, m.Up * scale, c);
    }

    private static void GodsHand(SkillVfxMesh m, Vector3 foot, float t)
    {
        float fade = Out(t, .2f, 1.15f);
        Glint(m, foot + Vector3.up * .2f, 1.6f, t, Gold);
        Sparks(m, foot + Vector3.up * .3f, t, 2.6f, 90, 155, Gold, 6);
        Sprite(m, SkillVfxShape.Impact, foot + Vector3.up * .24f - Vector3.Cross(m.Right, m.Up) * .85f,
            2.3f * Ease(t, .12f), .5f, 0, A(White, Out(t, .05f, .26f)));
        Halo(m, foot + m.Up * 4.65f, 1.35f, A(Gold, Out(t, .08f, .7f) * .8f));
        Fist(m, foot + Vector3.up * .15f, 2.6f, A(White, fade * .9f));
        Sprite(m, SkillVfxShape.Ray, foot + Vector3.up * 3, 1.5f, 3.2f, 0, A(Gold, fade * .3f), 1 - fade);
        float expand = Ease(t, .24f);
        LightBand(m, foot + m.Up * .35f, 3.4f * expand, .85f, 15, 330, A(Gold, fade * .9f));
        if (t > .1f) Wave(m, foot, .3f + 2.7f * Ease(t - .1f, .5f), .07f,
            A(Gold, Out(t, .2f, .75f) * .7f));
        SkillVfxAtlas.Ground(m, SkillVfxShape.Impact, foot, 2.6f * expand, 0, A(Gold, fade), 1 - fade);
        Wave(m, foot, .2f + 2.8f * expand, .13f * fade, A(White, fade));
        for (int i = -1; i <= 1; i += 2)
            Sprite(m, SkillVfxShape.Smoke, foot + m.Right * i * expand * 2.3f,
                1.2f, .5f, i * 10, A(Gold, fade * .5f), 1 - fade);
    }
}
