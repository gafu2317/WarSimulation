using UnityEngine;

public static partial class SkillVfxArt
{
    private static void Curse(SkillVfxMesh m, SkillId id, Vector3 foot, Vector3 p, float t, Color c)
    {
        float appear = In(t, .18f), pop = RevealScale(t);
        switch (id)
        {
            case SkillId.Grimoire_Bolt:
                float fade = Out(t, .05f, .75f);
                for (int side = -1; side <= 1; side += 2)
                    m.Crescent(p, m.Right * side, m.Up, .95f - .55f * Ease(t, .28f), .085f * fade,
                        t * -240, 155, A(c, fade));
                Glint(m, p, 1.0f, t - .16f, c);
                LightBand(m, p, .55f + .8f * Ease(t, .3f), .85f, -t * 180, 290, A(c, fade));
                Sprite(m, SkillVfxShape.Sigil, p, .3f + Ease(t, .3f) * .65f, .7f,
                    -t * 110, A(c, fade), 1 - fade);
                Sprite(m, SkillVfxShape.Smoke, p, .65f, .6f, t * 70, A(Dark, fade * .4f), 1 - fade);
                break;
            case SkillId.Grimoire_StrDebuff:
                float sag = Ease(t, .7f);
                LightBand(m, p, 1.1f, .55f, -t * 65, 270, A(c, appear * .8f));
                Sparks(m, p + m.Up * .7f, t - .12f, 1.25f, -90, 65, c, 4);
                BrokenMotif(m, SkillVfxShape.Gauntlet, p + m.Up * (1.4f - sag * .45f), 1.0f * pop,
                    sag * .55f, A(c, appear * .8f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float drop = Mathf.Repeat(t * .45f, 1);
                    Vector3 q = p + m.Right * side * .65f - m.Up * drop * .7f;
                    Ribbon(m, p + m.Right * side * .4f, q - m.Up * .35f,
                        side * .45f, .075f, A(c, appear * Mathf.Sin(drop * Mathf.PI) * .7f));
                    m.Chevron(q, .22f, A(c, appear * Mathf.Sin(drop * Mathf.PI)), true);
                    Sprite(m, SkillVfxShape.Smoke, q - m.Up * .2f, .27f, .35f, side * 20,
                        A(Dark, appear * Mathf.Sin(drop * Mathf.PI) * .35f), drop * .65f);
                }
                break;
            case SkillId.StatDebuff_INT:
                float jitter = Mathf.Sin(t * 11) * .06f;
                Vector3 book = p + m.Up * 1.2f + m.Right * jitter;
                BrokenMotif(m, SkillVfxShape.Tome, book, 1.1f * pop, Ease(t, .25f) * (.65f + .2f * Mathf.Sin(t * 4)), A(c, appear * .85f));
                LightBand(m, book, 1.35f, .65f, Mathf.Sin(t * 3) * 90, 210, A(c, appear * .65f));
                Glint(m, book, 1.15f, t - .08f, c);
                for (int side = -1; side <= 1; side += 2)
                    m.Crescent(book, m.Right * side, m.Up, .86f, .055f,
                        Mathf.Sin(t * 8) * 30 + side * 80, 95, A(c, appear * .6f));
                for (int i = 0; i < 3; i++)
                {
                    float u = Mathf.Repeat(t * .8f + i / 3f, 1);
                    Vector3 q = book + m.Right * Mathf.Sin(i * 4 + t * 7) * .45f + m.Up * (.3f + u * .4f);
                    m.Stroke(q, q + m.Right * .18f + m.Up * Mathf.Sin(t * 9 + i) * .2f, .04f,
                        A(c, appear * Mathf.Sin(u * Mathf.PI)));
                }
                break;
            case SkillId.StatDebuff_FAI:
                Vector3 head = p + m.Up * 1.35f;
                if (t < .8f)
                {
                    float eclipse = Mathf.Sin(Mathf.Clamp01(t / .8f) * Mathf.PI);
                    Bead(m, head + m.Right * (.7f - Ease(t, .65f) * 1.1f), .6f * eclipse, A(Dark, eclipse * .8f));
                    Sparks(m, head, t - .22f, 1.1f, -90, 160, c, 5);
                }
                Halo(m, head, 1.2f, A(c, appear), .7f * Ease(t, .3f) + .12f * Mathf.Sin(t * 2));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 shard = head + m.Right * side * .65f - m.Up * (.15f + .1f * Mathf.Sin(t * 2));
                    m.Crescent(shard, m.Right, m.Up * .55f, .72f, .2f, side * 75 - t * 20, 115, A(c, appear * .95f));
                    Glint(m, shard, .55f, Mathf.Repeat(t + (side + 1) * .2f, 1.4f), c);
                }
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Smoke, head + m.Right * side * .6f - m.Up * .35f,
                        .65f, .45f, side * t * 15, A(c, appear * .4f));
                for (int i = 0; i < 2; i++)
                {
                    float fall = Mathf.Repeat(t * .3f + i * .5f, 1);
                    m.Crescent(head + m.Right * (i == 0 ? -.45f : .45f) - m.Up * fall * .85f,
                        m.Right, m.Up * .32f, .25f, .075f, i * 120 - fall * 90, 90,
                        A(c, appear * Mathf.Sin(fall * Mathf.PI) * .55f));
                }
                break;
            case SkillId.StatDebuff_AGI:
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 ankle = foot - Vector3.Cross(m.Right, m.Up) * .8f + m.Right * side * .38f + m.Up * .32f;
                    Glint(m, ankle, .5f, t - .12f, c);
                    float clamp = 1 - Ease(t, .27f);
                    m.Ring(ankle, .35f + clamp * .45f, .13f, A(c, appear * .9f), 0, 360, false, 20);
                    ChainBetween(m, ankle, ankle + m.Right * side * .9f - m.Up * .15f, .18f, A(c, appear * .85f));
                }
                Vector3 tether = foot - Vector3.Cross(m.Right, m.Up) * .8f + m.Up * .25f;
                LightBand(m, tether + m.Up * .12f, 1.4f, .35f, -t * 30, 310, A(c, appear * .7f));
                m.Crescent(tether, m.Right, m.Up * .28f, .88f, .065f, 180, 180, A(c, appear * .55f));
                ChainBetween(m, tether - m.Right * .38f, tether + m.Right * .38f,
                    .12f, A(c, appear * .7f));
                break;
            case SkillId.Grimoire_Bind:
                float slack = 1 - Ease(t, .2f);
                LightBand(m, p, 1.25f, 1.3f, t * -60, 240, A(c, appear * .65f));
                float clink = Mathf.Sin(t * 42) * Out(t, .18f, .48f) * .035f;
                Glint(m, p, 1, t - .13f, c);
                Sparks(m, p, t - .13f, 1.15f, 90, 310, c, 5);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 a = p + m.Right * side * (.85f + slack * 1.3f) + m.Up * (.8f + clink);
                    Vector3 b = p - m.Right * side * (.85f + slack * 1.3f) - m.Up * .65f;
                    ChainBetween(m, a, b, .32f, A(c, appear * .95f));
                }
                if (t < .6f)
                    for (int side = -1; side <= 1; side += 2)
                        Sprite(m, SkillVfxShape.Sigil, p + m.Right * side * .72f, .35f, .35f,
                            side * t * 180, A(c, appear * Out(t, .25f, .6f) * .6f));
                ChainBetween(m, p - m.Right * 1.0f, p + m.Right * 1.0f, .24f, A(c, appear * .85f));
                break;
            case SkillId.Grimoire_Poison:
                float seep = Ease(t, .55f);
                Vector3 poisonBase = foot - Vector3.Cross(m.Right, m.Up) * .5f + m.Up * .1f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float curl = Mathf.Repeat(t * .28f + (side + 1) * .25f, 1);
                    Vector3 q = poisonBase + m.Right * side * (.45f + curl * .35f);
                    Sprite(m, SkillVfxShape.Smoke, q + m.Up * (.25f + curl * .55f), .95f, .55f,
                        side * curl * 35, A(c, appear * Mathf.Sin(curl * Mathf.PI) * .65f), curl * .55f);
                    Ribbon(m, q, p + m.Right * side * .35f, side * .35f, .065f,
                        A(c, appear * Mathf.Sin(curl * Mathf.PI) * .6f));
                }
                m.Crescent(poisonBase, m.Right, m.Up * .25f, 1.35f * seep, .15f,
                    t * -20, 300, A(c, appear * .65f));
                LightBand(m, p - m.Up * .5f, 1.15f, .6f, -t * 90, 240, A(c, appear * .7f));
                Sprite(m, SkillVfxShape.Skull, p + m.Up * (1.1f + .06f * Mathf.Sin(t * 2)),
                    .85f * pop, .85f * pop, 0, A(c, appear * (.7f + .12f * Mathf.Sin(t * 2))));
                for (int i = 0; i < 3; i++)
                {
                    float u = Mathf.Repeat(t * .32f + i * .31f, 1), alpha = Mathf.Sin(u * Mathf.PI) * appear;
                    Vector3 q = foot + m.Right * Mathf.Sin(i * 4 + u) * .6f + m.Up * (.2f + u * 1.1f);
                    Sprite(m, SkillVfxShape.Smoke, q, .3f + u * .12f, .28f, -u * 60, A(c, alpha * .3f), u * .65f);
                    Bead(m, q, .08f + u * .05f, A(c, alpha * .8f));
                    Sprite(m, SkillVfxShape.Petal, q - m.Up * .1f, .09f, .18f, 180, A(c, alpha * .7f));
                }
                break;
            case SkillId.Grimoire_Stealth:
                float vanish = Out(t, .4f, 1.25f);
                if (t < 1.25f)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sweep = Ease(t, .7f);
                        m.Crescent(p, m.Right * side, m.Up * 1.6f, .8f + .85f * (1 - sweep),
                            .07f * vanish, -100 + sweep * 120, 165, A(c, vanish * .85f));
                        Sprite(m, SkillVfxShape.Smoke, p + m.Right * side * (1 - sweep), .95f, 1.7f,
                            side * -20, A(c, vanish * .5f), sweep * .8f);
                    }
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Smoke, p + m.Right * side * vanish * .65f, .6f, 1,
                        side * 30, A(c, vanish * .4f), 1 - vanish);
                float trace = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 1.6f)), 8) * .15f;
                m.Crescent(p, m.Right, m.Up * 1.5f, .7f, .025f, 220, 65, A(c, trace));
                break;
        }
    }
}
