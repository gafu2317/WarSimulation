using UnityEngine;

public static partial class SkillVfxArt
{
    private static void Blessing(SkillVfxMesh m, SkillId id, Vector3 self, Vector3 foot, Vector3 p, float t, Color c)
    {
        float appear = In(t, .16f), pulse = .92f + .08f * Mathf.Sin(t * 3), pop = RevealScale(t);
        switch (id)
        {
            case SkillId.Bible_Smite:
                float fade = Out(t, .04f, .6f);
                Glint(m, p, 1.1f, t, c);
                Sparks(m, p, t, .95f, 90, 260, c, 4);
                Sprite(m, SkillVfxShape.Ray, foot + Vector3.up * 2.1f, .32f, 2.2f, 0, A(White, fade), 1 - fade);
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Ray, p + m.Right * side * .45f + m.Up * .25f,
                        .08f, 1.1f, side * -18, A(c, Out(t, .08f, .42f) * .65f));
                LightBand(m, p, 1.25f * Ease(t, .2f), .65f + .2f * Ease(t, .2f),
                    20, 320, A(c, fade));
                m.Stroke(p - m.Up * .7f, p + m.Up * .9f, .12f * fade, A(c, fade));
                m.Stroke(p - m.Right * .5f + m.Up * .2f, p + m.Right * .5f + m.Up * .2f, .1f * fade, A(White, fade));
                break;
            case SkillId.Bible_StrBuff:
                Vector3 fist = p + m.Up * (.65f + .5f * Ease(t, .28f));
                Sprite(m, SkillVfxShape.Gauntlet, fist, 1.0f * pulse * pop, 1.0f * pulse * pop, 0, A(c, appear * .95f));
                Glint(m, fist, .8f, t - .08f, c);
                Sparks(m, p, t - .06f, 2.15f, 90, 80, c, 6);
                for (int side = -1; side <= 1; side += 2)
                    LightBand(m, p + m.Right * side * .55f, .58f, 1.2f,
                        side * 90 + t * 100, 175, A(c, appear * .85f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float u = Mathf.Repeat(t * .8f, 1);
                    Vector3 arm = p + m.Right * side * .63f;
                    m.Crescent(arm, m.Right, m.Up * .55f, .27f + .06f * pulse, .055f,
                        15, 300, A(c, appear * .8f));
                    Ribbon(m, arm - m.Up * .5f, arm + m.Up * .35f, side * .2f, .09f, A(c, appear * .7f));
                    m.Chevron(arm + m.Up * u * .5f, .18f, A(White, appear * Mathf.Sin(u * Mathf.PI)));
                }
                break;
            case SkillId.Bible_FaiBuff:
                Vector3 halo = p + m.Up * 1.4f;
                Halo(m, halo, 1.0f * pop, A(c, appear * pulse));
                LightBand(m, halo, 1.15f, .35f, t * 55, 270, A(c, appear * .7f));
                for (int beam = -1; beam <= 1; beam++)
                    Sprite(m, SkillVfxShape.Ray, halo - m.Up * .95f + m.Right * beam * .65f,
                        .25f, 1.15f, beam * 14, A(c, appear * .32f));
                Glint(m, halo, 1.25f, t - .12f, c);
                if (t < .7f) Halo(m, halo, 1.0f + .5f * Ease(t, .7f), A(c, Out(t, .14f, .7f) * .4f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 ray = halo + m.Right * side * .48f - m.Up * .7f;
                    Sprite(m, SkillVfxShape.Ray, ray, .11f, .75f, side * 15, A(c, appear * pulse * .3f));
                    float orbit = t * .8f + (side + 1) * Mathf.PI * .5f;
                    Glint(m, halo + m.Right * Mathf.Cos(orbit) * .63f + m.Up * Mathf.Sin(orbit) * .2f,
                        .3f, Mathf.Repeat(t + (side + 1) * .3f, 1.8f), c);
                }
                Sprite(m, SkillVfxShape.Ray, halo - m.Up * .7f, .48f, .85f, 0, A(c, appear * .24f));
                break;
            case SkillId.Bible_IntBuff:
                Vector3 book = p + m.Up * (1.2f + .06f * Mathf.Sin(t * 1.6f));
                Sprite(m, SkillVfxShape.Tome, book, 1.14f * pop, .86f * pop, 0, A(c, appear * .95f));
                Glint(m, book, .65f, t - .16f, c);
                if (t < .6f)
                    for (int side = -1; side <= 1; side += 2)
                        m.Crescent(book, m.Right * side, m.Up, .8f + .25f * (1 - pop), .065f,
                            -70, 140, A(c, appear * Out(t, .25f, .6f)));
                LightBand(m, book - m.Up * .15f, 1.38f, .5f, -t * 65, 275, A(c, appear * .75f));
                m.Crescent(book - m.Up * .05f, m.Right, m.Up * .35f, 1.0f, .04f,
                    t * 25, 280, A(c, appear * .5f));
                for (int i = 0; i < 3; i++)
                {
                    float u = Mathf.Repeat(t * .35f + i / 3f, 1);
                    Vector3 q = book + m.Up * (.35f + u * .55f);
                    m.Stroke(q - m.Right * (.3f - i * .06f), q + m.Right * (.3f - i * .06f),
                        .035f, A(White, appear * Mathf.Sin(u * Mathf.PI) * .8f));
                }
                break;
            case SkillId.Bible_AgiBuff:
                for (int side = -1; side <= 1; side += 2)
                {
                    float u = Mathf.Repeat(t * 1.15f + (side + 1) * .25f, 1);
                    Vector3 q = foot - Vector3.Cross(m.Right, m.Up) * .8f + m.Right * side * .55f + m.Up * (.35f + .2f * Mathf.Sin(u * Mathf.PI));
                    for (int feather = 0; feather < 3; feather++)
                    {
                        float a = (25 + feather * 23) * Mathf.Deg2Rad;
                        Vector3 up = m.Up * Mathf.Sin(a) + m.Right * side * Mathf.Cos(a);
                        Vector3 right = m.Right * Mathf.Sin(a) - m.Up * side * Mathf.Cos(a);
                        SkillVfxAtlas.Stamp(m, SkillVfxShape.Feather, q + up * .18f,
                            right * .29f, up * (.98f - feather * .09f), A(c, appear * .85f));
                    }
                    Sprite(m, SkillVfxShape.Feather, q - m.Right * side * .35f - m.Up * .1f,
                        .5f, .2f, side * 15, A(c, appear * (1 - u) * .4f), u * .7f);
                    LightBand(m, q, .95f, .38f, side * t * 230, 195, A(c, appear * .75f));
                    Glint(m, q, .7f, t - .14f, c);
                    m.Crescent(q, m.Right * side, m.Up, .5f + u * .4f, .06f, 150, 100,
                        A(White, appear * (1 - u) * .65f));
                }
                break;
            case SkillId.Bible_Invulnerable:
                float close = Ease(t, .25f);
                Glint(m, p + m.Up * 1.15f, .8f, t - .18f, c);
                // Thin front rim and low-opacity interior keep the protected character readable.
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2 / 32, b = (i + 1) * Mathf.PI * 2 / 32;
                    Vector3 va = m.Right * Mathf.Cos(a) * 1.05f + m.Up * Mathf.Sin(a) * 1.12f;
                    Vector3 vb = m.Right * Mathf.Cos(b) * 1.05f + m.Up * Mathf.Sin(b) * 1.12f;
                    Vector3 center = p + m.Up * .18f;
                    m.Triangle(center, center + va * close, center + vb * close, A(c, appear * .055f));
                    if (i / 32f <= close) m.Stroke(center + va, center + vb, .065f, A(c, appear * .85f));
                }
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Shield, p + m.Right * side * (1.2f + .5f * (1 - close)),
                        .6f, .84f, side * -12, A(c, appear * .9f));
                LightBand(m, p + m.Up * .25f, 1.35f, 1.48f, t * 55, 240, A(c, appear * .7f));
                float guardPhase = t * .85f;
                Vector3 guardLight = p + m.Up * .18f + m.Right * Mathf.Cos(guardPhase) * 1.05f + m.Up * Mathf.Sin(guardPhase) * 1.12f;
                Glint(m, guardLight, .4f, Mathf.Repeat(t, 1.3f), c);
                m.Crescent(p + m.Up * .18f, m.Right, m.Up * 1.07f, 1.13f, .025f,
                    guardPhase * Mathf.Rad2Deg, 125, A(c, appear * .55f));
                Wave(m, foot, 1.05f, .055f, A(c, appear * .6f));
                break;
            case SkillId.Bible_Gotsume:
                Sparks(m, p, t - .08f, 1.4f, 90, 310, c, 6);
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Thorn, p + m.Right * side * .35f, 1.45f * pop, 1.55f * pop,
                        side == 1 ? 180 : 0, A(c, appear * .9f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 tip = p + m.Right * side * .95f + m.Up * .55f;
                    Glint(m, tip, .42f, Mathf.Repeat(t + (side + 1) * .28f, 1.7f), c);
                }
                LightBand(m, p, 1.4f, .7f, -t * 80, 230, A(c, appear * .55f));
                float gleam = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 4)), 6);
                Sprite(m, SkillVfxShape.Thorn, p, 1.05f, 1.25f, 0, A(White, appear * gleam * .45f));
                break;
            case SkillId.Bible_CarryRush:
                Vector3 dir = Vector3.ProjectOnPlane(foot - self, Vector3.up).normalized;
                if (dir.sqrMagnitude < .1f) dir = m.Right;
                Vector3 across = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 basePoint = foot + Vector3.up * .3f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 lane = basePoint + across * side * .48f;
                    Sprite(m, SkillVfxShape.Feather, lane - dir * .8f + m.Up * .2f,
                        1.2f, .3f, Mathf.Atan2(Vector3.Dot(dir, m.Up), Vector3.Dot(dir, m.Right)) * Mathf.Rad2Deg - 35,
                        A(c, appear * .4f));
                    Ribbon(m, lane - dir * 3.1f, lane + dir * .75f, side * .38f, .23f, A(c, appear * .6f));
                    Ribbon(m, lane - dir * 2.8f - m.Up * .15f, lane + dir * .45f + m.Up * .5f,
                        side * .45f, .11f, A(White, appear * .55f));
                    float u = Mathf.Repeat(t * 1.7f + (side + 1) * .15f, 1);
                    Vector3 tip = lane + dir * (-1.8f + u * 2.4f);
                    m.Stroke(tip - dir * .85f, tip, .045f, A(White, appear * Mathf.Sin(u * Mathf.PI) * .7f), 0);
                    m.Stroke(tip - dir * .4f + across * .18f, tip, .09f, A(White, appear * Mathf.Sin(u * Mathf.PI)));
                    m.Stroke(tip, tip - dir * .4f - across * .18f, .09f, A(White, appear * Mathf.Sin(u * Mathf.PI)));
                }
                if ((foot - self).sqrMagnitude > .2f)
                    Ribbon(m, self + Vector3.up * .3f, basePoint, .25f, .04f, A(c, appear * .4f));
                break;
        }
    }
}
