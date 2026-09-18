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
    private Vector3 _self, _target, _point, _forward;
    private Transform _caster, _victim;
    private CombatStatusEffects _status;
    private ShieldShoulderGuardEffect _guard;
    private BibleGotsumeEffect _thorns;
    private RosaryHealingAreaZone _zone;
    private bool _bound;
    private float _ending = -1f, _radius, _previewLead;
    private Phase _phase;
    private Color _color;
    private Camera _camera;
    private readonly List<SpriteRenderer> _sprites = new();
    private readonly List<float> _spriteAlphas = new();

    public void Prepare(SkillId skill, Vector3 self, Vector3 target, Vector3 point,
        Phase phase = Phase.Preview, float castDuration = 0f, float radius = 3f)
    {
        if (_mesh == null)
        {
            _mesh = new SkillVfxMesh();
            var child = new GameObject("Spell mesh") { hideFlags = gameObject.hideFlags };
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = _mesh.Mesh;
            _renderer = child.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = SkillVfxAtlas.Shared.Material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        RestoreSprites();
        Skill = skill; _self = self; _target = target; _point = point; _phase = phase; _radius = radius;
        _caster = null; _victim = null; FollowCharacter = null; StatusKey = null; _status = null;
        _guard = null; _thorns = null; _zone = null; _bound = false;
        _ending = -1; Age = 0; Finished = false; ActionId = 0;
        _camera = Camera.main;
        _mesh.SetGround(skill is SkillId.Wand_AreaBlast or SkillId.Rosary_HealingArea
            ? CombatSceneContext.Instance?.MapSystem : null, point, Mathf.Max(1.5f, radius));
        _forward = Vector3.ProjectOnPlane(target - self, Vector3.up).normalized;
        if (_forward.sqrMagnitude < .01f) _forward = Vector3.forward;
        _color = ColorFor(skill);
        Lifetime = phase == Phase.Cast ? Mathf.Max(.05f, castDuration) :
            phase == Phase.Preview ? SkillVfxArt.Duration(skill) : IsPersistent(skill) ? 5f : SkillVfxArt.Duration(skill);
        _previewLead = phase == Phase.Preview ? SkillVfxArt.PreviewLead(skill) : 0;
        Lifetime += _previewLead;
        transform.position = Vector3.zero; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        RenderAt(0);
    }

    public void SetPreviewCamera(Camera camera) => _camera = camera;

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
            if (_phase == Phase.Preview && Skill == SkillId.Wand_GodsHand)
            {
                float lead = _previewLead;
                if (time < lead) { DrawCast(time / Mathf.Max(.01f, lead)); _mesh.Upload(); return; }
                hitTime -= lead;
            }
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

    private void DrawCast(float t) => SkillVfxArt.Cast(_mesh, Skill, _self, _target, _point, t, _radius);
    private void DrawProjectile(float t) => SkillVfxArt.Projectile(_mesh, Skill, _self, _target, t);
    private void DrawImpact(float t)
    {
        Vector3 direction = Vector3.ProjectOnPlane(_target - _self, Vector3.up);
        if (direction.sqrMagnitude > .001f) _forward = direction.normalized;
        SkillVfxArt.Impact(_mesh, Skill, _self, _target, _point, _forward, t, _radius);
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
        SkillId.Shield_IronWall or SkillId.Shield_Taunt or
        SkillId.Grimoire_StrDebuff or SkillId.StatDebuff_INT or SkillId.StatDebuff_FAI or SkillId.StatDebuff_AGI or
        SkillId.Grimoire_Bind or SkillId.Grimoire_Poison or SkillId.Grimoire_Stealth or
        SkillId.Bible_StrBuff or SkillId.Bible_IntBuff or SkillId.Bible_FaiBuff or SkillId.Bible_AgiBuff or
        SkillId.Bible_Invulnerable or SkillId.Bible_Gotsume or
        SkillId.Rosary_Regeneration or SkillId.Rosary_HealingArea;

    public static Color ColorFor(SkillId id) => SkillVfxArt.ColorFor(id);

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
