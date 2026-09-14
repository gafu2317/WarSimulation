using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class SkillVfxEffect : MonoBehaviour
{
    public enum Phase { Preview, Cast, Impact }
    public SkillId Skill { get; private set; }
    public float Age { get; private set; }
    public float Lifetime { get; private set; }
    public bool Finished { get; private set; }
    public long ActionId { get; set; }
    public Character FollowCharacter { get; private set; }
    public string StatusKey { get; private set; }
    public RosaryHealingAreaZone BoundZone => _zone;
    private SkillVfxMesh _mesh;
    private MeshRenderer _renderer;
    private Vector3 _self, _target, _point, _forward, _right;
    private Transform _caster, _victim;
    private CombatStatusEffects _status;
    private ShieldShoulderGuardEffect _guard;
    private BibleGotsumeEffect _thorns;
    private BibleCarryRushEffect _rush;
    private RosaryHealingAreaZone _zone;
    private bool _bound;
    private float _ending = -1f, _radius;
    private Phase _phase;
    private Color _color;
    private Camera _camera;
    private readonly List<SpriteRenderer> _sprites = new();
    private readonly List<float> _spriteAlphas = new();
    private static readonly Color Ivory = new(1f, .97f, .81f);
    private static readonly Color Ink = new(.19f, .075f, .29f);

    public void Prepare(SkillId skill, Vector3 self, Vector3 target, Vector3 point,
        Phase phase = Phase.Preview, float castDuration = 0f, float radius = 3f)
    {
        if (_mesh == null)
        {
            _mesh = new SkillVfxMesh();
            var child = new GameObject("Spell mesh");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = _mesh.Mesh;
            _renderer = child.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = Resources.Load<Material>("Combat/Vfx/StylizedSkill");
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        RestoreSprites();
        Skill = skill; _self = self; _target = target; _point = point; _phase = phase; _radius = radius;
        _caster = null; _victim = null; FollowCharacter = null; StatusKey = null; _status = null;
        _guard = null; _thorns = null; _rush = null; _zone = null; _bound = false;
        _ending = -1; Age = 0; Finished = false; ActionId = 0;
        _camera = Camera.main;
        _mesh.SetGround(skill is SkillId.Wand_AreaBlast or SkillId.Rosary_HealingArea
            ? CombatSceneContext.Instance?.MapSystem : null, point, Mathf.Max(1.5f, radius));
        _forward = Vector3.ProjectOnPlane(target - self, Vector3.up).normalized;
        if (_forward.sqrMagnitude < .01f) _forward = Vector3.forward;
        _right = Vector3.Cross(Vector3.up, _forward);
        _color = ColorFor(skill);
        Lifetime = phase == Phase.Cast ? Mathf.Max(.05f, castDuration) : IsPersistent(skill) ? 5f : 1.15f;
        if (phase != Phase.Cast && (skill == SkillId.Sword_Slash || skill == SkillId.Shield_Slash)) Lifetime = .55f;
        if (phase != Phase.Cast && skill == SkillId.Wand_GodsHand) Lifetime = 1.6f;
        if (phase == Phase.Preview && IsProjectile(skill)) Lifetime += .45f;
        transform.position = Vector3.zero; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        RenderAt(0);
    }

    public void BindCast(Character caster, Transform target) { _caster = caster.transform; _victim = target; }

    public void Bind(Character actor, Character target, string key, RosaryHealingAreaZone zone = null)
    {
        FollowCharacter = target; StatusKey = key; _caster = actor != null ? actor.transform : null;
        _victim = target != null ? target.transform : null; _zone = zone;
        _bound = IsPersistent(Skill);
        if (target != null)
        {
            if (Skill == SkillId.Grimoire_Stealth)
            {
                target.GetComponentsInChildren(true, _sprites);
                for (int i = 0; i < _sprites.Count; i++) _spriteAlphas.Add(_sprites[i].color.a);
            }
            _status = target.StatusEffects;
            _guard = target.GetComponent<ShieldShoulderGuardEffect>();
            _thorns = target.GetComponent<BibleGotsumeEffect>();
            _rush = target.GetComponent<BibleCarryRushEffect>();
        }
    }

    public void End() { if (_ending < 0) _ending = Age; }

    public bool Tick(float delta)
    {
        Age += delta;
        if (_caster != null) _self = FootPosition(_caster);
        if (_victim != null) _target = FootPosition(_victim);
        if (_bound && _ending < 0 && !BindingAlive()) End();
        if (!_bound && Age >= Lifetime) Finished = true;
        if (_ending >= 0 && Age - _ending >= .25f) Finished = true;
        if (_sprites.Count > 0)
        {
            float alpha = _ending >= 0 ? Mathf.Lerp(.25f, 1, (Age - _ending) / .25f) : Mathf.Lerp(1, .25f, Mathf.Clamp01(Age / .4f));
            for (int i = 0; i < _sprites.Count; i++)
            {
                if (_sprites[i] == null) continue;
                Color tint = _sprites[i].color; tint.a = _spriteAlphas[i] * alpha; _sprites[i].color = tint;
            }
        }
        if (!Finished) RenderAt(Age);
        return !Finished;
    }

    private bool BindingAlive()
    {
        if (_zone != null) return _zone.IsActive;
        if (Skill == SkillId.Rosary_HealingArea) return false;
        if (FollowCharacter == null || !FollowCharacter.gameObject.activeInHierarchy ||
            FollowCharacter.Health == null || !FollowCharacter.Health.IsAlive) return false;
        if (Skill == SkillId.Shield_ShoulderGuard) return _guard != null && _guard.IsActive;
        if (Skill == SkillId.Bible_Gotsume) return _thorns != null && _thorns.IsActive;
        if (Skill == SkillId.Bible_CarryRush) return _rush != null && _rush.Affects(FollowCharacter);
        return _status != null && !string.IsNullOrEmpty(StatusKey) && _status.HasActiveEffect(StatusKey);
    }

    public void RenderAt(float time)
    {
        _mesh.Clear();
        if (_camera == null) _camera = Camera.main;
        if (_camera != null)
        {
            _mesh.Right = _camera.transform.right; _mesh.Up = _camera.transform.up;
            float worldHeight = _camera.orthographic ? _camera.orthographicSize * 2 :
                Vector3.Distance(_camera.transform.position, _target) * 2 * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad);
            _mesh.MinimumStroke = Mathf.Clamp(worldHeight / 360f, .025f, .12f);
        }
        _mesh.Opacity = _ending >= 0 ? 1 - Mathf.Clamp01((time - _ending) / .25f) :
            _bound || _phase == Phase.Cast ? 1 : 1 - Mathf.SmoothStep(0, 1, (time - Lifetime + .25f) / .25f);
        if (_phase == Phase.Cast) DrawCast(time / Mathf.Max(.01f, Lifetime));
        else
        {
            float hitTime = time;
            if (_phase == Phase.Preview && IsProjectile(Skill))
            {
                if (time < .45f) { DrawProjectile(time / .45f); _mesh.Upload(); return; }
                hitTime -= .45f;
            }
            if (_phase == Phase.Impact && IsProjectile(Skill) && time < .18f)
                _mesh.Stroke(Vector3.Lerp(_self + Vector3.up, _target + Vector3.up, time / .18f),
                    _target + Vector3.up, .07f, Alpha(_color, 1 - time / .18f), .1f);
            DrawImpact(hitTime);
        }
        _mesh.Upload();
    }

    private void DrawCast(float t)
    {
        float gather = Mathf.SmoothStep(0, 1, t);
        Vector3 p = _self + Vector3.up * 1.1f;
        if (IsProjectile(Skill) && t > .72f) { DrawProjectile((t - .72f) / .28f); return; }
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120 + t * 90;
            _mesh.Crescent(p, _mesh.Right, _mesh.Up, .22f + (1 - gather) * .6f, .07f, a, 65, _color);
        }
        _mesh.Gem(p, .08f + gather * .12f, .14f + gather * .12f, Ivory);
        if (Skill == SkillId.Wand_GodsHand)
            Hand(_target + Vector3.up * (3.5f - .3f * gather), .5f * gather, _color);
    }

    private void DrawProjectile(float t)
    {
        Vector3 a = _self + Vector3.up * 1.1f, b = _target + Vector3.up;
        Vector3 p = Vector3.Lerp(a, b, t);
        bool heal = Skill == SkillId.Rosary_DistantHeal;
        bool curse = Skill == SkillId.Grimoire_Bolt;
        bool heavy = Skill == SkillId.Wand_ArcaneBlast;
        if (heal) p += Vector3.up * Mathf.Sin(t * Mathf.PI) * .8f;
        if (curse) p += _right * Mathf.Sin(t * Mathf.PI * 3) * .3f;
        Vector3 dir = (b - a).normalized;
        float size = heavy ? .35f : .16f;
        _mesh.Stroke(p - dir * (heavy ? 1.8f : .9f), p, size * 1.7f, _color, .1f);
        _mesh.Stroke(p - dir * .7f, p, .065f, Ivory, .2f);
        _mesh.Gem(p, size, size * (curse ? 1 : 1.5f), _color, curse ? t * 540 : 0);
        if (heavy || curse)
            for (int i = 0; i < 3; i++)
                _mesh.Crescent(p - dir * i * .35f, _mesh.Right, _mesh.Up, size + .15f, .065f, t * 720 + i * 120, 95, _color);
        if (heal) _mesh.Ring(p, .25f, .045f, Ivory, 0, 360, false, 16);
    }

    private void DrawImpact(float t)
    {
        Vector3 p = _target + Vector3.up;
        if (_camera != null) p -= _camera.transform.forward * .55f;
        float e = 1 - Mathf.Exp(-t * 12);
        float fade = 1 - Mathf.Clamp01(t / .8f);
        Color c = _color; c.a = fade;
        switch (Skill)
        {
            case SkillId.Sword_Slash:
                _mesh.Crescent(p - _forward * .45f, _right, (_forward + Vector3.up * .3f).normalized,
                    1.25f, .28f * (Mathf.Max(0, 1 - t / .55f)), -135 + e * 85, 125, c);
                _mesh.Crescent(p - _forward * .45f, _right, (_forward + Vector3.up * .3f).normalized,
                    1.3f, .065f, -135 + e * 85, 120, Alpha(Ivory, fade));
                Burst(p, t, 5, .8f, c); break;
            case SkillId.Wand_Bolt:
                Burst(p, t, 4, .65f, c); _mesh.Gem(p, .22f * fade, .4f * fade, Ivory); break;
            case SkillId.Wand_GodsHand:
                Hand(p + Vector3.up * (.45f + 2.4f * (1 - e)), 1.35f * (1 - Mathf.Clamp01((t - .7f) / .7f)), Alpha(_color, 1 - Mathf.Clamp01((t - .65f) / .7f)));
                GroundWave(_target, t, 1.3f, _color);
                Burst(p, t - .13f, 8, 1.5f, c); break;
            case SkillId.Grimoire_Bind:
                BindCage(p, t); break;
            case SkillId.Rosary_CloseHeal:
                Blossom(_target, t, 1.65f); break;
            default:
                DrawOther(t, p, e, c); break;
        }
    }

    private void DrawOther(float t, Vector3 p, float e, Color c)
    {
        float pulse = .9f + .1f * Mathf.Sin(t * 3);
        float appear = Mathf.SmoothStep(0, 1, t / .3f);
        Vector3 foot = _target + Vector3.up * .12f;
        switch (Skill)
        {
            case SkillId.Shield_Slash:
                _mesh.Shield(p - _forward * (.55f * (1 - e)), .8f * (Mathf.Max(0, 1 - t / .65f)), c);
                for (int i = -2; i <= 2; i++)
                {
                    Vector3 d = (_forward + _right * i * .5f).normalized;
                    _mesh.Stroke(p + d * e * .45f, p + d * e * 1.5f, .15f * (Mathf.Max(0, 1 - t / .8f)), c, .1f);
                }
                GroundWave(_target, t, 1.05f, _color); break;
            case SkillId.Shield_ShoulderGuard:
                for (int i = -1; i <= 1; i += 2)
                    _mesh.Shield(p + _mesh.Right * i * (.78f + .4f * (1 - appear)), .65f * pulse, _color);
                Link(_self + Vector3.up * .35f, foot, t, _color); break;
            case SkillId.Wand_ArcaneBlast:
                _mesh.Ring(p, .2f + e * .75f, .14f * (Mathf.Max(0, 1 - t / 1.15f)), c, t * 100, 320, false);
                _mesh.Crescent(p, _mesh.Right, _mesh.Up, .45f + e * .95f, .22f * (Mathf.Max(0, 1 - t / 1.15f)), -t * 180, 210, c);
                Burst(p, t, 9, 1.9f, c); GroundWave(_target, t, 1.25f, _color); break;
            case SkillId.Wand_AreaBlast:
                GroundWave(_point, t, _radius, _color);
                GroundWave(_point, t - .16f, _radius, Ivory);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    Vector3 d = new(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vector3 tip = _point + d * _radius * e + Vector3.up * (.15f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * .65f);
                    _mesh.Gem(tip, .16f * (Mathf.Max(0, 1 - t / 1.15f)), .4f * (Mathf.Max(0, 1 - t / 1.15f)), c, i * 60);
                    _mesh.Stroke(_point + d * _radius * e * .55f + Vector3.up * .1f, tip, .09f, c, .1f);
                }
                break;
            case SkillId.Grimoire_Bolt:
                for (int i = 0; i < 3; i++)
                    _mesh.Crescent(p, _mesh.Right, _mesh.Up, .3f + e * .5f, .11f, -t * 140 + i * 120, 70, c);
                Burst(p, t, 3, .8f, Alpha(Ink, c.a)); break;
            case SkillId.Grimoire_StrDebuff:
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector3 q = p + _mesh.Right * i * .75f - Vector3.up * (.2f * appear);
                    _mesh.Chevron(q, .33f * pulse, Ink, true);
                    _mesh.Chevron(q + Vector3.up * .16f, .25f, _color, true);
                }
                _mesh.Ring(foot, .72f, .08f, Ink, 0, 300); break;
            case SkillId.StatDebuff_INT:
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90 - t * 65) * Mathf.Deg2Rad;
                    Vector3 q = p + Vector3.up * .95f + (_mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a)) * .4f;
                    _mesh.Gem(q, .12f, .24f, Alpha(i % 2 == 0 ? Ink : _color, .65f + .35f * Mathf.Sin(t * 7 + i)), i * 90 + t * 80);
                }
                break;
            case SkillId.StatDebuff_FAI:
                p += Vector3.up * (.13f * Mathf.Sin(t * 2));
                _mesh.Crescent(p + Vector3.up * .9f, _mesh.Right, (_mesh.Up + _mesh.Right * .35f).normalized, .52f, .12f, 20, 120, Ink);
                _mesh.Crescent(p + Vector3.up * .9f, _mesh.Right, _mesh.Up, .52f, .09f, 190, 125, _color);
                _mesh.Stroke(p + Vector3.up * .8f, p + Vector3.up * .25f, .08f, Ink);
                _mesh.Stroke(p + Vector3.up * .5f - _mesh.Right * .18f, p + Vector3.up * .5f + _mesh.Right * .18f, .08f, _color);
                break;
            case SkillId.StatDebuff_AGI:
                for (int i = 0; i < 3; i++)
                {
                    _mesh.Ring(foot, .8f - i * .17f, .085f, i % 2 == 0 ? Ink : _color, t * -22 + i * 100, 215);
                    _mesh.Chevron(foot + _mesh.Right * (i - 1) * .45f, .18f, _color, true);
                }
                break;
            case SkillId.Grimoire_Poison:
                _mesh.Ring(foot, .68f * pulse, .08f, Ink, 0, 280);
                for (int i = 0; i < 5; i++)
                {
                    float f = Mathf.Repeat(t * .48f + i * .21f, 1);
                    Vector3 q = foot + _mesh.Right * Mathf.Sin(i * 12.7f) * .65f + Vector3.up * f * 1.65f;
                    float size = .07f + Mathf.Sin(f * Mathf.PI) * .1f;
                    _mesh.Ring(q, size, .035f, Alpha(_color, 1 - f), 0, 320, false, 12);
                    if (f < .55f) _mesh.Gem(q, size * .35f, size * .55f, Alpha(new Color(.7f, .85f, .22f), 1 - f));
                }
                break;
            case SkillId.Grimoire_Stealth:
                for (int i = 0; i < 3; i++)
                {
                    float f = Mathf.Repeat(t * .16f + i * .33f, 1);
                    _mesh.Crescent(p, _mesh.Right, _mesh.Up, .85f * (1 - f * .25f), .06f * (1 - f), i * 120 + f * 30, 42, Alpha(Ink, .5f * (1 - f)));
                }
                if (t < .55f) _mesh.Crescent(p, _mesh.Right, _mesh.Up, 1.25f * (1 - t), .09f, 0, 280, Alpha(_color, Mathf.Max(0, 1 - t / .55f)));
                break;
            case SkillId.Bible_Smite:
                _mesh.Stroke(p + Vector3.up * 3 * (1 - e), p - Vector3.up * .35f, .17f * (Mathf.Max(0, 1 - t / 1.15f)), c, .3f);
                _mesh.Stroke(p + Vector3.up * 2.8f * (1 - e), p, .045f, Ivory);
                _mesh.Stroke(p - _mesh.Right * .45f * e, p + _mesh.Right * .45f * e, .08f, c);
                GroundWave(_target, t, .65f, _color); break;
            case SkillId.Bible_StrBuff:
                for (int i = 0; i < 2; i++)
                    _mesh.Chevron(p + Vector3.up * (.8f + i * .36f + .22f * appear), (.45f - i * .08f) * pulse, _color);
                _mesh.Gem(p + Vector3.up * .75f, .2f, .28f, Ivory); break;
            case SkillId.Bible_IntBuff:
                p += Vector3.up * (.035f * Mathf.Sin(t * 2));
                for (int i = 0; i < 2; i++)
                {
                    float angle = i * 45 + Mathf.Min(1, t * 2) * 90;
                    _mesh.Ring(p + Vector3.up * .85f, .45f - i * .1f, .055f, _color, angle, 360, false, 4);
                }
                _mesh.Gem(p + Vector3.up * .85f, .09f, .14f, Ivory); break;
            case SkillId.Bible_FaiBuff:
                p += Vector3.up * (.05f * Mathf.Sin(t * 2));
                Wings(p + Vector3.up * .75f, .85f * appear, _color);
                _mesh.Ring(p + Vector3.up * 1.15f, .32f, .06f, Ivory, 0, 360, true, 24); break;
            case SkillId.Bible_AgiBuff:
                Wings(foot + Vector3.up * .15f, .65f * pulse, _color);
                for (int i = 0; i < 2; i++)
                    _mesh.Chevron(foot + Vector3.up * (Mathf.Repeat(t * .8f + i * .5f, 1) * .6f), .28f, Alpha(Ivory, .7f));
                break;
            case SkillId.Bible_Invulnerable:
                _mesh.Ring(p, 1.08f * appear, .085f, _color, 30, 360, false, 6);
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60 + 30) * Mathf.Deg2Rad;
                    _mesh.Gem(p + (_mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a)) * 1.08f * appear, .075f, .12f, Ivory, i * 60);
                }
                _mesh.Ring(foot, .9f, .065f, Alpha(_color, .5f)); break;
            case SkillId.Bible_Gotsume:
                _mesh.Ring(p, .88f, .08f, _color, t * 15, 360, false, 12);
                for (int i = 0; i < 8; i++)
                {
                    float a = (i * 45 + t * 15) * Mathf.Deg2Rad;
                    Vector3 d = _mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a);
                    _mesh.Petal(p + d * .78f, d, .45f * appear, .12f, _color);
                }
                break;
            case SkillId.Bible_CarryRush:
                Vector3 direction = _caster != null ? _caster.forward : _forward;
                direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector3 q = foot + side * i * .5f;
                    _mesh.Stroke(q - direction * 1.2f, q + direction * .45f, .08f, Alpha(_color, .65f), .3f);
                    _mesh.Stroke(q + side * i * .3f, q + direction * .45f, .1f, _color, .15f);
                }
                Wings(p, .65f * pulse * appear, _color); break;
            case SkillId.Rosary_Strike:
                for (int i = 0; i < 5; i++)
                {
                    float f = Mathf.Clamp01((t - i * .055f) / .22f);
                    float a = (-110 + f * 150 + i * 9) * Mathf.Deg2Rad;
                    Vector3 q = p + _mesh.Right * Mathf.Cos(a) * .95f + _mesh.Up * Mathf.Sin(a) * .65f;
                    _mesh.Gem(q, .09f * (Mathf.Max(0, 1 - t / 1.15f)), .12f * (Mathf.Max(0, 1 - t / 1.15f)), c);
                    if (i > 0) _mesh.Stroke(q, q - _mesh.Right * .11f, .035f, c);
                }
                Burst(p, t - .17f, 5, .75f, c); break;
            case SkillId.Rosary_DistantHeal:
                Blossom(_target, t, .85f); break;
            case SkillId.Rosary_Regeneration:
                float grow = Mathf.Repeat(t, 1);
                Vector3 sprout = p + Vector3.up * .9f;
                _mesh.Stroke(sprout - Vector3.up * .3f, sprout + Vector3.up * .25f, .055f, _color);
                _mesh.Petal(sprout, (_mesh.Right + _mesh.Up).normalized, .38f * pulse, .12f, _color);
                _mesh.Petal(sprout, (-_mesh.Right + _mesh.Up).normalized, .38f * pulse, .12f, _color);
                _mesh.Gem(sprout + Vector3.up * (.3f + grow * .55f), .08f * (1 - grow), .14f * (1 - grow), Ivory);
                _mesh.Ring(foot, .62f, .05f, Alpha(_color, .65f)); break;
            case SkillId.Rosary_HealingArea:
                Vector3 center = _point + Vector3.up * .09f;
                _mesh.Ring(center, _radius, .065f, Alpha(_color, .7f), terrain: true);
                _mesh.Ring(center, _radius * .92f, .025f, Alpha(Ivory, .6f), 0, 360, true, 48, terrain: true);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    Vector3 q = _mesh.Surface(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _radius);
                    _mesh.Petal(q, Vector3.up + _mesh.Right * .7f, .32f, .08f, _color);
                    _mesh.Petal(q, Vector3.up - _mesh.Right * .7f, .32f, .08f, _color);
                }
                float wave = Mathf.Repeat(t, 1);
                _mesh.Ring(center, _radius * wave, .035f, Alpha(_color, .25f * (1 - wave)), terrain: true); break;
            case SkillId.Rosary_SacrificeThunder:
                Thunder(t, p); break;
        }
    }

    private void Wings(Vector3 p, float size, Color c)
    {
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
                _mesh.Petal(p + _mesh.Right * side * i * .12f,
                    _mesh.Right * side + _mesh.Up * (.25f + i * .35f), size * (1 - i * .16f), size * .11f, c);
    }

    private void Link(Vector3 a, Vector3 b, float t, Color c)
    {
        if ((a - b).sqrMagnitude < .01f) return;
        // Broken strokes convey the connection without a solid stripe across the battle.
        for (int i = 0; i < 8; i++)
        {
            float u = (i + Mathf.Repeat(t * .5f, 1)) / 8;
            _mesh.Stroke(Vector3.Lerp(a, b, u), Vector3.Lerp(a, b, Mathf.Min(1, u + .035f)), .055f, Alpha(c, .55f));
        }
    }

    private void Thunder(float t, Vector3 p)
    {
        bool selfHit = (_target - _self).sqrMagnitude < .01f;
        if (selfHit)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                float r = Mathf.Max(.05f, 1 - t * 2);
                _mesh.Gem(_self + Vector3.up + (_mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a)) * r, .09f, .14f, Alpha(_color, 1 - t));
            }
            return;
        }
        float flash = t < .13f ? 1 : t < .22f ? .25f : t < .36f ? .9f : Mathf.Max(0, 1 - (t - .36f) * 3);
        Vector3 from = _self + Vector3.up, last = from;
        for (int i = 1; i <= 9; i++)
        {
            float f = i / 9f;
            Vector3 q = Vector3.Lerp(from, p, f);
            if (i < 9) q += _mesh.Up * Mathf.Sin(i * 17.1f) * .35f + _mesh.Right * Mathf.Cos(i * 8.3f) * .2f;
            _mesh.Stroke(last, q, .15f, Alpha(_color, flash));
            _mesh.Stroke(last, q, .045f, Alpha(Ivory, flash));
            last = q;
        }
        Burst(p, t, 7, 1.25f, Alpha(Ivory, flash));
        GroundWave(_target, t, .85f, _color);
    }

    private void BindCage(Vector3 p, float t)
    {
        float s = 1 + (.7f * (1 - Mathf.Clamp01(t / .25f)));
        for (int k = 0; k < 2; k++)
        {
            Vector3 x = (_mesh.Right + (k == 0 ? 1 : -1) * _mesh.Up * .7f).normalized;
            Vector3 y = Vector3.Cross(Vector3.Cross(_mesh.Right, _mesh.Up), x);
            _mesh.Crescent(p, x, y, .85f * s, .1f, k * 180, 290, Ink);
            for (int i = 0; i < 7; i++)
            {
                float a = (i * 45 + k * 20) * Mathf.Deg2Rad;
                _mesh.Gem(p + (x * Mathf.Cos(a) + y * Mathf.Sin(a)) * .85f * s, .065f, .12f, _color, i * 45);
            }
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            _mesh.Gem(p + (_mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a)) * .9f * s, .13f, .2f, Ink, i * 90);
        }
    }

    private void Hand(Vector3 p, float s, Color c)
    {
        Vector3 x = _mesh.Right * s, y = _mesh.Up * s;
        _mesh.Quad(p - x * .42f - y * .5f, p + x * .42f - y * .5f, p + x * .5f + y * .3f, p - x * .5f + y * .3f, Alpha(c, c.a * .45f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = p + x * (i * .27f - .4f) + y * .2f;
            Vector3 b = a + y * (.75f - Mathf.Abs(i - 1.5f) * .13f);
            _mesh.Stroke(a, b, .19f * s, c, .8f);
            _mesh.Stroke(a, b, .045f * s, Alpha(Ivory, c.a), .8f);
        }
        _mesh.Stroke(p - x * .36f - y * .22f, p - x * .86f + y * .25f, .22f * s, c, .7f);
        _mesh.Stroke(p - x * .4f - y * .5f, p + x * .4f - y * .5f, .12f * s, c);
        _mesh.Ring(p + y * .1f, .22f * s, .04f * s, Alpha(Ivory, c.a), 0, 360, false, 16);
    }

    private void Blossom(Vector3 ground, float t, float size)
    {
        float open = Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI);
        Vector3 p = ground + Vector3.up * .35f;
        for (int i = 0; i < 5; i++)
        {
            float a = (i * 36 + 18) * Mathf.Deg2Rad;
            Vector3 d = _mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a);
            _mesh.Petal(p, d, size * open, .21f * open, _color);
        }
        GroundWave(ground, t, size, _color);
        _mesh.Gem(p + Vector3.up * (.3f + t * .8f), .18f * open, .3f * open, Ivory);
    }

    private void Burst(Vector3 p, float t, int count, float size, Color c)
    {
        if (t < 0 || t > .8f) return;
        float e = 1 - Mathf.Exp(-t * 10), f = Mathf.Max(0, 1 - t / .8f);
        for (int i = 0; i < count; i++)
        {
            float a = (i * 360f / count + 17) * Mathf.Deg2Rad;
            Vector3 d = _mesh.Right * Mathf.Cos(a) + _mesh.Up * Mathf.Sin(a);
            _mesh.Stroke(p + d * size * e * .45f, p + d * size * e, .11f * f, c, .05f);
            _mesh.Gem(p + d * size * e, .035f * f, .1f * f, Alpha(Ivory, c.a), -i * 360f / count);
        }
    }

    private void GroundWave(Vector3 p, float t, float size, Color c)
    {
        if (t < 0 || t > .85f) return;
        float e = 1 - Mathf.Exp(-t * 8);
        _mesh.Ring(p + Vector3.up * .08f, size * e, .1f * (1 - t / .85f), Alpha(c, 1 - t / .85f), terrain: true);
    }

    private static Color Alpha(Color c, float a) { c.a = a; return c; }
    public static Vector3 FootPosition(Transform target)
    {
        Vector3 p = target.position;
        CombatMapSystem map = CombatSceneContext.Instance?.MapSystem;
        return map != null && map.CurrentMap != null ? map.MapLocalToSurfaceWorldPosition(map.MapOrigin.InverseTransformPoint(p)) : p;
    }

    public static bool IsProjectile(SkillId id) => id == SkillId.Wand_Bolt || id == SkillId.Wand_ArcaneBlast ||
        id == SkillId.Grimoire_Bolt || id == SkillId.Rosary_DistantHeal;
    public static bool IsPersistent(SkillId id) => id is SkillId.Shield_ShoulderGuard or
        SkillId.Grimoire_StrDebuff or SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or SkillId.StatDebuff_AGI or
        SkillId.Grimoire_Bind or SkillId.Grimoire_Poison or SkillId.Grimoire_Stealth or
        SkillId.Bible_StrBuff or SkillId.Bible_IntBuff or SkillId.Bible_FaiBuff or SkillId.Bible_AgiBuff or
        SkillId.Bible_Invulnerable or SkillId.Bible_Gotsume or SkillId.Bible_CarryRush or
        SkillId.Rosary_Regeneration or SkillId.Rosary_HealingArea;

    public static Color ColorFor(SkillId id) => id switch
    {
        SkillId.Sword_Slash => new(1, .23f, .13f),
        SkillId.Shield_Slash or SkillId.Shield_ShoulderGuard => new(.18f, .65f, 1),
        SkillId.Wand_Bolt or SkillId.Wand_ArcaneBlast or SkillId.Wand_AreaBlast or SkillId.Wand_GodsHand => new(1, .66f, .12f),
        SkillId.Grimoire_Bolt or SkillId.Grimoire_StrDebuff or SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or
        SkillId.StatDebuff_AGI or SkillId.Grimoire_Bind or SkillId.Grimoire_Poison or SkillId.Grimoire_Stealth => new(.72f, .28f, .95f),
        SkillId.Rosary_Strike or SkillId.Rosary_CloseHeal or SkillId.Rosary_DistantHeal or
        SkillId.Rosary_Regeneration or SkillId.Rosary_HealingArea or SkillId.Rosary_SacrificeThunder => new(.1f, .85f, .57f),
        _ => new(1, .82f, .36f)
    };

    private void OnDisable() => RestoreSprites();
    private void RestoreSprites()
    {
        for (int i = 0; i < _sprites.Count; i++)
        {
            if (_sprites[i] == null) continue;
            Color tint = _sprites[i].color; tint.a = _spriteAlphas[i]; _sprites[i].color = tint;
        }
        _sprites.Clear(); _spriteAlphas.Clear();
    }

    private void OnDestroy()
    {
        if (_mesh == null) return;
        if (Application.isPlaying) Destroy(_mesh.Mesh); else DestroyImmediate(_mesh.Mesh);
    }
}
