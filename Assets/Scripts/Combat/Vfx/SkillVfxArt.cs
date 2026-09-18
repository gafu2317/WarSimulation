using UnityEngine;

public static partial class SkillVfxArt
{
    private static readonly Color White = new(1, .98f, .86f);
    private static readonly Color Dark = new(.18f, .06f, .27f);
    private static readonly Color Gold = new(1, .74f, .24f);

    public static float PreviewLead(SkillId id) => id == SkillId.Wand_GodsHand
        ? CombatSkillFactory.Create(id).CastTimeSeconds : SkillVfxEffect.IsProjectile(id) ? .45f : 0;

    public static float Duration(SkillId id) => id switch
    {
        SkillId.Sword_Slash or SkillId.Sword_QuickSlash => .55f,
        SkillId.Sword_StrongSlash => .85f,
        SkillId.Shield_Slash or SkillId.Rosary_Strike or SkillId.Bible_Smite => .75f,
        SkillId.Wand_Bolt or SkillId.Grimoire_Bolt => .8f,
        SkillId.Wand_ArcaneBlast or SkillId.Wand_AreaBlast or SkillId.Wand_GodsHand => 1.8f,
        SkillId.Rosary_CloseHeal => 1.65f,
        SkillId.Rosary_SacrificeThunder => 1.4f,
        _ => 1.3f
    };

    public static Color ColorFor(SkillId id) => id switch
    {
        SkillId.Sword_Slash or SkillId.Sword_QuickSlash => new(.78f, .88f, 1),
        SkillId.Sword_StrongSlash => new(1f, .78f, .28f),
        SkillId.Shield_Slash => new(.35f, .57f, .78f),
        SkillId.Shield_ShoulderGuard => Gold,
        SkillId.Shield_IronWall => new(.3f, .62f, .95f),
        SkillId.Shield_Taunt => new(1f, .28f, .08f),
        SkillId.Wand_Bolt => new(.25f, .8f, 1),
        SkillId.Wand_ArcaneBlast => new(.58f, .3f, 1),
        SkillId.Wand_AreaBlast => new(1, .34f, .08f),
        SkillId.Wand_GodsHand => new(1, .83f, .42f),
        SkillId.Grimoire_Bolt => new(.65f, .22f, .85f),
        SkillId.Grimoire_StrDebuff or SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or
        SkillId.StatDebuff_AGI or SkillId.Grimoire_Bind or SkillId.Grimoire_Poison => new(.7f, .32f, .9f),
        SkillId.Grimoire_Stealth => Gold,
        SkillId.Bible_StrBuff or SkillId.Bible_IntBuff or SkillId.Bible_FaiBuff or
        SkillId.Bible_AgiBuff or SkillId.Bible_Invulnerable or SkillId.Bible_Gotsume or
        SkillId.Bible_Smite => Gold,
        SkillId.Rosary_Strike => new(.93f, .87f, .7f),
        SkillId.Rosary_DistantHeal or SkillId.Rosary_CloseHeal or SkillId.Rosary_Regeneration or
        SkillId.Rosary_HealingArea => new(.22f, .91f, .59f),
        SkillId.Rosary_SacrificeThunder => new(.77f, .61f, 1),
        _ => White
    };

    public static void Cast(SkillVfxMesh m, SkillId id, Vector3 self, Vector3 target, Vector3 point,
        float progress, float radius)
    {
        float u = Mathf.Clamp01(progress);
        Color c = ColorFor(id);
        if (SkillVfxEffect.IsProjectile(id) && u >= .72f)
        {
            Projectile(m, id, self, target, (u - .72f) / .28f);
            return;
        }
        Vector3 p = self + Vector3.up * 1.2f;
        if (id == SkillId.Wand_GodsHand)
        {
            float descend = Mathf.Pow(Mathf.Clamp01((u - .45f) / .55f), 3);
            Fist(m, target + Vector3.up * (4.5f * (1 - descend) + .15f + .2f * (1 - u)), 2.6f, A(White, u));
            Sprite(m, SkillVfxShape.Ray, target + Vector3.up * 3.5f, 1.4f, 3.5f, 0, A(Gold, u * .2f));
            Wave(m, target, 1.4f, .035f, A(Gold, .45f * u));
            if (u > .72f)
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Ray, target + m.Up * (3.5f + 2 * (1 - descend)) + m.Right * side * 1.05f,
                        .12f, 2.1f, 0, A(Gold, (u - .72f) / .28f * .65f));
            return;
        }
        if (id == SkillId.Wand_AreaBlast || id == SkillId.Rosary_HealingArea)
        {
            Wave(m, point, radius, .035f, A(c, .35f * u), true);
            SkillVfxAtlas.Ground(m, id == SkillId.Wand_AreaBlast ? SkillVfxShape.Impact : SkillVfxShape.Petal,
                point + Vector3.up * .1f, radius * .7f * u, u, A(c, .16f * u));
        }
        bool curse = IsCurse(id);
        if (curse)
            Sprite(m, SkillVfxShape.Sigil, p, .4f + .3f * u, .5f + .3f * u, -u * 75, A(c, u * .75f));
        else
        {
            int count = id == SkillId.Wand_ArcaneBlast ? 4 : 2;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count + u * 4;
                Vector3 offset = (m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a)) * (1 - u) * .8f;
                Sprite(m, SkillVfxShape.Feather, p + offset, .35f, .65f, a * Mathf.Rad2Deg - 45, A(c, .5f * u));
            }
            Orb(m, p, .15f + u * (id == SkillId.Wand_ArcaneBlast ? .5f : .2f), A(White, u));
        }
    }

    public static void Projectile(SkillVfxMesh m, SkillId id, Vector3 self, Vector3 target, float progress)
    {
        float u = Mathf.Clamp01(progress);
        Vector3 a = self + Vector3.up * 1.2f, b = target + Vector3.up;
        Vector3 dir = (b - a).normalized;
        Vector3 p = Vector3.Lerp(a, b, u);
        Color c = ColorFor(id);
        float angle = Mathf.Atan2(Vector3.Dot(dir, m.Up), Vector3.Dot(dir, m.Right)) * Mathf.Rad2Deg;
        if (id == SkillId.Rosary_DistantHeal)
        {
            p += Vector3.up * Mathf.Sin(u * Mathf.PI) * .85f;
            Ribbon(m, a, p, .6f, .1f, A(c, .35f));
            for (int i = 1; i <= 2; i++)
            {
                float trail = Mathf.Clamp01(u - i * .08f);
                Vector3 echo = Vector3.Lerp(a, b, trail) + Vector3.up * Mathf.Sin(trail * Mathf.PI) * .85f;
                Heart(m, echo, .2f - i * .04f, A(c, .35f - i * .08f));
            }
            Heart(m, p, .55f, c);
        }
        else if (id == SkillId.Grimoire_Bolt)
        {
            p += m.Up * Mathf.Sin(u * Mathf.PI * 3) * .16f;
            Sprite(m, SkillVfxShape.Smoke, p - dir * .2f, .45f, .32f, -u * 240, A(Dark, .8f));
            for (int i = 1; i <= 2; i++)
                Sprite(m, SkillVfxShape.Sigil, p - dir * i * .34f, .25f - i * .04f, .25f - i * .04f,
                    -u * 180 - i * 25, A(c, .35f / i));
            Sprite(m, SkillVfxShape.Sigil, p, .5f, .5f, -u * 180, c);
            Orb(m, p, .12f, White);
        }
        else if (id == SkillId.Wand_ArcaneBlast)
        {
            Sprite(m, SkillVfxShape.Orb, p - dir * 1.05f, 1.65f, .65f, angle, A(c, .22f));
            Sprite(m, SkillVfxShape.Orb, p - dir * .6f, 1.9f, 1.2f, angle, c);
            for (int i = 0; i < 3; i++)
                m.Crescent(p - dir * (.15f + i * .3f), m.Right, m.Up, .55f + i * .09f,
                    .09f, angle + u * 540 + i * 120, 120, A(i == 0 ? White : c, .75f));
        }
        else
        {
            m.Crescent(p, m.Right, m.Up, .3f, .045f, angle + u * 640, 150, A(White, .7f));
            Sprite(m, SkillVfxShape.Orb, p - dir * .55f, .85f, .22f, angle, A(c, .26f));
            Sprite(m, SkillVfxShape.Orb, p - dir * .15f, 1.05f, .6f, angle, c);
            m.Stroke(p - dir * .5f, p, .055f, White, .1f);
        }
    }

    public static void Impact(SkillVfxMesh m, SkillId id, Vector3 self, Vector3 target, Vector3 point,
        Vector3 forward, float time, float radius)
    {
        float t = Mathf.Max(0, time);
        Vector3 p = target + Vector3.up;
        Vector3 normal = Vector3.Cross(m.Right, m.Up).normalized;
        p -= normal * .8f;
        Color c = ColorFor(id);
        switch (id)
        {
            case SkillId.Sword_Slash: Slash(m, p, forward, t, c); break;
            case SkillId.Sword_QuickSlash: QuickSlash(m, p, forward, t, c); break;
            case SkillId.Sword_StrongSlash: StrongSlash(m, p, forward, t, c); break;
            case SkillId.Shield_Slash: ShieldStrike(m, p, target, forward, t, c); break;
            case SkillId.Shield_ShoulderGuard: ShoulderGuard(m, self, target, t, c); break;
            case SkillId.Shield_IronWall: IronWall(m, target, p, t, c); break;
            case SkillId.Shield_Taunt: Taunt(m, target, p, t, c); break;
            case SkillId.Wand_Bolt: BoltHit(m, p, t, c); break;
            case SkillId.Wand_ArcaneBlast: ArcaneBlast(m, p, target, t, c); break;
            case SkillId.Wand_AreaBlast: AreaBlast(m, point, t, radius, c); break;
            case SkillId.Wand_GodsHand: GodsHand(m, target, t); break;
            case SkillId.Grimoire_Bolt:
            case SkillId.Grimoire_StrDebuff:
            case SkillId.StatDebuff_INT:
            case SkillId.StatDebuff_FAI:
            case SkillId.StatDebuff_AGI:
            case SkillId.Grimoire_Bind:
            case SkillId.Grimoire_Poison:
            case SkillId.Grimoire_Stealth: Curse(m, id, target, p, t, c); break;
            case SkillId.Bible_Smite:
            case SkillId.Bible_StrBuff:
            case SkillId.Bible_FaiBuff:
            case SkillId.Bible_IntBuff:
            case SkillId.Bible_AgiBuff:
            case SkillId.Bible_Invulnerable:
            case SkillId.Bible_Gotsume:
                Blessing(m, id, target, p, t, c); break;
            case SkillId.Rosary_Strike:
            case SkillId.Rosary_DistantHeal:
            case SkillId.Rosary_CloseHeal:
            case SkillId.Rosary_Regeneration:
            case SkillId.Rosary_HealingArea:
            case SkillId.Rosary_SacrificeThunder: Rosary(m, id, self, target, point, p, t, radius, c); break;
        }
    }

    private static bool IsCurse(SkillId id) => id is SkillId.Grimoire_Bolt or SkillId.Grimoire_StrDebuff or
        SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or SkillId.StatDebuff_AGI or
        SkillId.Grimoire_Bind or SkillId.Grimoire_Poison or SkillId.Grimoire_Stealth;
    private static Color A(Color c, float alpha) { c.a = Mathf.Clamp01(alpha); return c; }
    private static float In(float t, float duration = .3f) => Mathf.SmoothStep(0, 1, t / duration);
    private static float Out(float t, float start, float end) => 1 - Mathf.SmoothStep(0, 1, (t - start) / (end - start));
    private static float Ease(float t, float duration) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t / duration), 3);
    private static void Sprite(SkillVfxMesh m, SkillVfxShape shape, Vector3 p, float width, float height,
        float angle, Color color, float dissolve = 0)
    {
        float a = angle * Mathf.Deg2Rad;
        SkillVfxAtlas.Stamp(m, shape, p, (m.Right * Mathf.Cos(a) + m.Up * Mathf.Sin(a)) * width,
            (-m.Right * Mathf.Sin(a) + m.Up * Mathf.Cos(a)) * height, color, dissolve);
    }
    private static void Orb(SkillVfxMesh m, Vector3 p, float size, Color c)
    {
        // Use the round head of the comet; its visible center is right of the texture center.
        Rect full = SkillVfxAtlas.Shared.Regions[(int)SkillVfxShape.Orb];
        Rect head = new(full.x + full.width * .55f, full.y + full.height * .32f, full.width * .28f, full.height * .36f);
        m.TextureQuad(p, m.Right * size, m.Up * size, c, 0, head, .12f);
    }
    private static void Wave(SkillVfxMesh m, Vector3 foot, float radius, float width, Color color, bool terrain = false)
        => m.Ring(foot + Vector3.up * .1f, radius, width, color, 0, 360, true, 40, terrain);
    private static void Ribbon(SkillVfxMesh m, Vector3 a, Vector3 b, float bend, float width, Color c)
    {
        const int count = 14;
        Vector3 side = Vector3.Cross(b - a, Vector3.Cross(m.Right, m.Up)).normalized;
        for (int i = 0; i < count; i++)
        {
            float u = i / (float)count, v = (i + 1) / (float)count;
            Vector3 p = Vector3.Lerp(a, b, u) + side * (Mathf.Sin(u * Mathf.PI) * bend);
            Vector3 q = Vector3.Lerp(a, b, v) + side * (Mathf.Sin(v * Mathf.PI) * bend);
            float wa = Mathf.Sin(u * Mathf.PI) * width, wb = Mathf.Sin(v * Mathf.PI) * width;
            m.Quad(p - side * wa, p + side * wa, q + side * wb, q - side * wb, c);
        }
    }
}
