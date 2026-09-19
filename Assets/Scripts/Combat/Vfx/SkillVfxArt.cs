using UnityEngine;

public static partial class SkillVfxArt
{
    private static readonly Color White = new(1, .98f, .86f);
    private static readonly Color Dark = new(.18f, .06f, .27f);
    private static readonly Color Gold = new(1, .74f, .24f);
    private static readonly Color BuffOrange = new(1, .42f, .08f);
    private static readonly Color DebuffBluePurple = new(.38f, .25f, 1f);
    private static readonly Color HealGreen = new(.22f, .91f, .59f);
    private static readonly Color SwordQuickCyan = new(.15f, .85f, 1f);
    private static readonly Color SwordStrongRed = new(1f, .28f, .12f);

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
        SkillId.Rosary_SacrificeThunder => 1.8f,
        _ => 1.3f
    };

    public static Color ColorFor(SkillId id) => id switch
    {
        SkillId.Sword_Slash => new(.78f, .88f, 1),
        SkillId.Sword_QuickSlash => SwordQuickCyan,
        SkillId.Sword_StrongSlash => SwordStrongRed,
        SkillId.Shield_Slash => new(.35f, .57f, .78f),
        SkillId.Shield_ShoulderGuard => BuffOrange,
        SkillId.Shield_IronWall => BuffOrange,
        SkillId.Shield_Taunt => DebuffBluePurple,
        SkillId.Wand_Bolt => new(.2f, 1f, .68f),
        SkillId.Wand_ArcaneBlast => new(1f, .18f, .055f),
        SkillId.Wand_AreaBlast => new(1, .34f, .08f),
        SkillId.Wand_GodsHand => new(1, .83f, .42f),
        SkillId.Grimoire_Bolt => new(.65f, .22f, .85f),
        SkillId.Grimoire_StrDebuff or SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or
        SkillId.StatDebuff_AGI or SkillId.Grimoire_Bind or SkillId.Grimoire_Poison => DebuffBluePurple,
        SkillId.Grimoire_Stealth => BuffOrange,
        SkillId.Bible_StrBuff or SkillId.Bible_IntBuff or SkillId.Bible_FaiBuff or
        SkillId.Bible_AgiBuff or SkillId.Bible_Invulnerable or SkillId.Bible_Gotsume => BuffOrange,
        SkillId.Bible_Smite => Gold,
        SkillId.Rosary_Strike => new(1f, .42f, .08f),
        SkillId.Rosary_DistantHeal or SkillId.Rosary_CloseHeal or SkillId.Rosary_Regeneration or
        SkillId.Rosary_HealingArea => HealGreen,
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
            float scale = u < .28f ? .82f : u < .56f ? 1.55f : 2.6f;
            float descend = Ease(u - .84f, .08f);
            Vector3 hand = target + Vector3.up * (3.35f * (1 - descend) + .15f);
            Fist(m, hand, scale, A(White, Mathf.Clamp01(u * 4)));
            Halo(m, hand + m.Up * scale * .75f, scale * .58f, A(Gold, .85f));
            float falling = In(u - .82f, .025f) * Out(u, .92f, .98f);
            if (falling > 0)
                for (int side = -1; side <= 1; side += 2)
                    Sprite(m, SkillVfxShape.Ray, target + m.Up * (2.55f + 1.25f * (1 - descend)) + m.Right * side * 1.05f,
                        .16f, 2.35f, 0, A(Gold, falling * .9f));
            return;
        }
        if (id == SkillId.Wand_Bolt)
        {
            float angle = u * 420;
            Sprite(m, SkillVfxShape.WindSpiral, p, .55f + u * 1.3f, .48f + u * .9f,
                -angle * .72f, A(c, u * .78f), 1 - u);
            Sprite(m, SkillVfxShape.WindBlade, p, .7f + u * 1.35f, .42f + u * .32f,
                angle, A(Color.Lerp(c, White, .18f), u));
            m.Crescent(p, m.Right, m.Up * .72f, .35f + u * 1.15f, .055f * u,
                25 + u * 120, 190, A(White, u * .65f));
            for (int i = -1; i <= 1; i += 2)
            {
                float curl = u * Mathf.PI * 1.4f + i * .7f;
                Vector3 a = p + m.Right * i * (.48f + u * .22f) - m.Up * .25f;
                Vector3 b = p + m.Right * i * Mathf.Cos(curl) * .28f + m.Up * (.45f + u * .35f);
                m.Stroke(a, b, .045f + u * .025f, A(White, u * .7f));
            }
            return;
        }
        if (id == SkillId.Wand_ArcaneBlast)
        {
            float charge = .2f + u * .58f;
            Orb(m, p, charge, A(Color.Lerp(c, White, .35f), u));
            m.Ring(p, .9f - u * .42f, .065f, A(c, u * .85f), u * 180, 280, false, 24);
            return;
        }
        if (id == SkillId.Wand_AreaBlast)
        {
            float spread = Ease(u, .72f);
            SkillVfxAtlas.Ground(m, SkillVfxShape.FireCracks, point,
                radius * (.18f + spread * .68f), u * .18f, A(c, u * .48f), 1 - u);
            m.Ring(point + Vector3.up * .1f, radius * (.2f + spread * .66f), .09f,
                A(Gold, u * .8f), 35 + u * 150, 300 * spread, true, 32, true);
            for (int i = 0; i < 4; i++)
            {
                float age = Mathf.Clamp01(u * 1.45f - i * .12f);
                float a = i * 1.73f + .4f;
                Vector3 q = m.Surface(point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * (.35f + i % 2 * .28f));
                Sprite(m, SkillVfxShape.FlameTongue, q + m.Up * (.2f + age * .3f),
                    .2f + age * (.12f + i % 2 * .05f), .35f + age * (.55f + i % 3 * .12f),
                    i % 2 == 0 ? -11 : 9, A(i == 0 ? White : i % 2 == 0 ? Gold : c, age * .9f));
            }
            return;
        }
        if (id == SkillId.Rosary_HealingArea)
        {
            Wave(m, point, radius, .035f, A(c, .35f * u), true);
            SkillVfxAtlas.Ground(m, SkillVfxShape.Petal,
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
            Orb(m, p, .62f, c);
            Orb(m, p + dir * .08f, .24f, White);
            m.Stroke(p - dir * 1.15f, p - dir * .15f, .16f, A(c, .55f), .08f);
        }
        else if (id == SkillId.Wand_Bolt)
        {
            Sprite(m, SkillVfxShape.WindSpiral, p - dir * .12f, 1.05f, .72f,
                -angle - u * 480, A(c, .72f));
            Sprite(m, SkillVfxShape.WindBlade, p, 1.5f, .6f,
                angle + u * 420, Color.Lerp(c, White, .15f));
            for (int i = 1; i <= 2; i++)
            {
                float trail = Mathf.Clamp01(u - i * .065f);
                Vector3 q = Vector3.Lerp(a, b, trail);
                m.Stroke(q - dir * (.55f + i * .16f), q, .055f + i * .012f,
                    A(i == 1 ? White : c, .72f / i), .02f);
            }
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
