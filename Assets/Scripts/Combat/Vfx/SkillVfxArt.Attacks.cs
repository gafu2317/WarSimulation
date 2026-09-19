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
        SlashBlur(m, center, x, y, t, c, 1.75f);
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center, x * 1.9f, y * .98f,
            A(Color.Lerp(c, White, .28f), fade), 1 - fade);
        Sprite(m, SkillVfxShape.Impact, p, .22f + strike * .35f, .22f + strike * .35f,
            0, A(White, Out(t, .02f, .12f)));
    }

    private static void StrongSlash(SkillVfxMesh m, Vector3 p, Vector3 forward, float t, Color c)
    {
        Slash(m, p, forward, t, c);
        float impact = Out(t, .06f, .3f);
        Wave(m, p, 1.65f * Ease(t, .24f), .16f * impact, A(c, impact * .85f));
    }

    private static void Slash(SkillVfxMesh m, Vector3 p, Vector3 forward, float t, Color c)
    {
        float strike = Ease(t, .085f), visible = Out(t, .11f, .52f);
        float side = Vector3.Dot(forward, m.Right) < 0 ? -1 : 1;
        float a = Mathf.Lerp(-78, 25, strike) * Mathf.Deg2Rad;
        Vector3 x = (m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a)) * side;
        Vector3 y = -m.Right * Mathf.Sin(a) + m.Up * Mathf.Cos(a);
        Vector3 center = p + x * .35f;
        SlashBlur(m, center, x, y, t, c, 2.45f);
        SkillVfxAtlas.Stamp(m, SkillVfxShape.Slash, center,
            x * (2.05f + strike * .85f), y * 1.4f,
            A(Color.Lerp(c, White, .18f), visible), 1 - visible);
        Sparks(m, p, t, 2.2f, side > 0 ? 25 : 155, 70, White, 3);
        Sprite(m, SkillVfxShape.Impact, p, .3f + strike * .5f, .3f + strike * .5f, 15,
            A(White, Out(t, .025f, .17f)), 1 - visible);
    }

    private static void SlashBlur(SkillVfxMesh m, Vector3 center, Vector3 x, Vector3 y,
        float t, Color c, float size)
    {
        float blur = Out(t, .01f, .14f);
        if (blur <= 0) return;
        float sweep = Ease(t, .09f);
        SkillVfxAtlas.Stamp(m, SkillVfxShape.SlashSmear,
            center - x * (.42f - sweep * .22f) - y * .08f,
            x * size * (1.12f - sweep * .12f), y * (1.18f - sweep * .22f),
            A(Color.Lerp(c, White, .28f), blur * .82f), 1 - blur);
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
        float expand = Ease(t, .18f), fade = Out(t, .18f, .72f);
        Sprite(m, SkillVfxShape.WindSpiral, p, .4f + expand * 1.85f, .32f + expand * 1.35f,
            -t * 430, A(c, fade * .9f), 1 - fade);
        Sprite(m, SkillVfxShape.WindBlade, p, 1.1f + expand * .75f, .5f + expand * .18f,
            t * 320, A(Color.Lerp(c, White, .22f), fade), 1 - fade);
        for (int side = -1; side <= 1; side += 2)
            m.Crescent(p + m.Right * side * expand * .45f, m.Right * side, m.Up * .72f,
                .28f + expand * 1.35f, .065f * fade, 18, 145, A(White, fade * .72f));
        Wave(m, p - m.Up * .8f, .25f + expand * 1.6f, .08f * fade, A(c, fade * .72f));
        Sparks(m, p, t - .04f, 1.45f, 90, 275, White, 3);
    }

    private static void ArcaneBlast(SkillVfxMesh m, Vector3 p, Vector3 foot, float t, Color c)
    {
        float flash = Out(t, .025f, .12f);
        Orb(m, p, .12f + Ease(t, .035f) * .72f, A(Color.white, flash));

        float flameAge = t - .018f;
        if (flameAge > 0)
        {
            float expand = Ease(flameAge, .105f);
            float fade = Out(flameAge, .14f, .34f);
            Color fire = new(1f, .31f, .035f, fade);
            Sprite(m, SkillVfxShape.ExplosionFlameFront, p,
                .14f + expand * 1.8f, .14f + expand * 1.6f, -7 + t * 19,
                fire, 1 - fade);
        }

        for (int i = 0; i < 6; i++)
        {
            float age = t - .032f - i * .007f;
            if (age <= 0) continue;
            float travel = Ease(age, .17f), fade = Out(age, .1f, .36f);
            float angle = (i * 57f + i * i * 11f + 8f) * Mathf.Deg2Rad;
            Vector3 direction = m.Right * Mathf.Cos(angle) + m.Up * Mathf.Sin(angle);
            Vector3 q = p + direction * (.22f + travel * (.8f + i % 3 * .13f));
            Sprite(m, SkillVfxShape.FlameTongue, q,
                .13f + i % 2 * .035f, .3f + i % 3 * .075f,
                angle * Mathf.Rad2Deg - 90, A(i % 3 == 0 ? Gold : SwordStrongRed, fade), 1 - fade);
        }

        float pressure = Ease(t - .025f, .15f), pressureFade = Out(t, .08f, .3f);
        m.Ring(foot, .14f + pressure * 1.95f, .14f * pressureFade,
            A(Gold, pressureFade), 0, 360, true, 40);

        float smokeAge = t - .2f;
        if (smokeAge <= 0) return;
        float smokeRise = Ease(smokeAge, .45f), smokeFade = Out(smokeAge, .24f, .72f);
        Color smoke = new(.22f, .16f, .13f, smokeFade * .42f);
        for (int side = -1; side <= 1; side += 2)
            Sprite(m, SkillVfxShape.Smoke,
                p + m.Right * side * (.16f + smokeRise * .22f) + m.Up * smokeRise * .38f,
                .34f + smokeRise * .28f, .3f + smokeRise * .24f, side * 17,
                smoke, 1 - smokeFade);
    }

    private static void AreaBlast(SkillVfxMesh m, Vector3 point, float t, float radius, Color c)
    {
        float ignition = Ease(t, .24f), fieldFade = Out(t, 1.08f, 1.72f);
        SkillVfxAtlas.Ground(m, SkillVfxShape.FireCracks, point, radius * .92f * ignition,
            .12f, A(SwordStrongRed, fieldFade * .82f), 1 - fieldFade);
        m.Ring(point + Vector3.up * .1f, radius * (.12f + ignition * .82f), .13f * fieldFade,
            A(Gold, fieldFade), 30 + t * 55, 315, true, 42, true);
        Wave(m, point, radius * (.18f + ignition * .74f), .055f * fieldFade,
            A(White, fieldFade * .7f), true);
        for (int i = 0; i < 7; i++)
        {
            float age = t - i * .055f;
            if (age <= 0) continue;
            float erupt = Ease(age, .18f), fade = Out(age, .82f, 1.62f);
            float angle = i * 2.17f + .28f;
            float distance = radius * (.18f + (i % 3) * .27f) * ignition;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
            Vector3 basePoint = m.Surface(point + offset);
            float flicker = .82f + .18f * Mathf.Sin(t * (13 + i % 3) + i * 1.8f);
            float height = (.85f + i % 4 * .28f) * flicker * erupt;
            Vector3 center = basePoint + m.Up * (height * .7f + .12f);
            Color flame = i == 0 ? White : i % 2 == 0 ? Gold : c;
            Sprite(m, SkillVfxShape.FlameTongue, center,
                .28f + i % 3 * .075f, height, Mathf.Sin(t * 8 + i) * 11,
                A(flame, fade), 1 - fade);
        }
        for (int i = 0; i < 5; i++)
        {
            float u = Mathf.Repeat(t * (.72f + i % 3 * .08f) + i * .173f, 1);
            float a = i * 2.37f;
            Vector3 q = point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * (.15f + .58f * u) +
                Vector3.up * (.25f + u * 2.35f);
            float ember = Mathf.Sin(u * Mathf.PI) * fieldFade;
            Sprite(m, SkillVfxShape.Ray, q, .055f, .16f + u * .12f,
                i * 29, A(i % 3 == 0 ? White : Gold, ember));
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
        float handFade = Out(t, .04f, .3f);
        float afterglow = Out(t, .12f, .95f);
        Glint(m, foot + Vector3.up * .2f, 1.6f, t, Gold);
        Sparks(m, foot + Vector3.up * .3f, t, 2.6f, 90, 155, Gold, 5);
        Sprite(m, SkillVfxShape.Impact, foot + Vector3.up * .24f - Vector3.Cross(m.Right, m.Up) * .85f,
            2.3f * Ease(t, .12f), .5f, 0, A(White, Out(t, .05f, .26f)));
        Fist(m, foot + Vector3.up * .15f, 2.6f, A(White, handFade * .9f));
        Sprite(m, SkillVfxShape.Ray, foot + Vector3.up * 2.1f, 1.25f, 2.3f, 0,
            A(Gold, afterglow * .22f), 1 - afterglow);
        float expand = Ease(t, .24f);
        LightBand(m, foot + m.Up * .35f, 3.4f * expand, .85f, 15, 330, A(Gold, afterglow * .9f));
        if (t > .1f) Wave(m, foot, .3f + 2.7f * Ease(t - .1f, .5f), .07f,
            A(Gold, Out(t, .2f, .75f) * .7f));
        SkillVfxAtlas.Ground(m, SkillVfxShape.Impact, foot, 2.6f * expand, 0,
            A(Gold, afterglow), 1 - afterglow);
        Wave(m, foot, .2f + 2.8f * expand, .13f * afterglow, A(White, afterglow));
        for (int i = -1; i <= 1; i += 2)
            Sprite(m, SkillVfxShape.Smoke, foot + m.Right * i * expand * 2.3f,
                1.2f, .5f, i * 10, A(Gold, afterglow * .42f), 1 - afterglow);
    }
}
