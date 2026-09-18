using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;

public sealed class SkillVfxPlayer : MonoBehaviour
{
    [SerializeField] private Transform _spawnRoot;
    private const int Capacity = 96;
    private static readonly ProfilerMarker UpdateMarker = new("SkillVfx.Update");
    private readonly List<SkillVfxEffect> _active = new(Capacity);
    private readonly Stack<SkillVfxEffect> _pool = new(Capacity);
    private bool _wasCombatRunning;
    public int ActiveCount => _active.Count;
    public int PooledCount => _pool.Count;
    public int PeakActiveCount { get; private set; }
    public int DroppedCount { get; private set; }

    private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        ClearAll();
    }
    private void OnSceneChanged(Scene before, Scene after) => ClearAll();

    public bool TryPlay(SkillId skillId, Vector3 selfPosition, Vector3? targetPosition,
        Vector3? pointPosition, out string message)
    {
        if (skillId == SkillId.None) { message = "未定義のスキル"; return false; }
        SkillBase skill = CombatSkillFactory.Create(skillId);
        if (skill == null) { message = "未定義のスキル"; return false; }
        var effect = Play(skillId, selfPosition, targetPosition ?? selfPosition,
            pointPosition ?? targetPosition ?? selfPosition, SkillVfxEffect.Phase.Preview, 0, skill.AreaRadius);
        message = effect != null ? $"{skillId} / 画像併用" : "VFX同時表示上限";
        return effect != null;
    }

    public void PlayCast(CombatSkillActionInfo action)
    {
        if (action.Actor == null || action.Skill == null || action.Skill.CastTimeSeconds <= 0) return;
        Vector3 self = SkillVfxEffect.FootPosition(action.Actor.transform);
        var context = action.Context;
        var effect = Play(action.SkillId, self, TargetPosition(context, self),
            context.HasTargetPoint ? context.TargetPoint : TargetPosition(context, self),
            SkillVfxEffect.Phase.Cast, action.Skill.CastTimeSeconds, action.Skill.AreaRadius);
        if (effect == null) return;
        effect.ActionId = action.ActionId;
        effect.BindCast(action.Actor, TargetTransform(context));
    }

    public void CancelCast(CombatSkillActionResult result) => RemoveCast(result.Action.ActionId);
    private void RemoveCast(long actionId)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i].ActionId == actionId && actionId != 0) Release(i);
    }

    public void PlayAction(CombatSkillActionResult result)
    {
        if (result == null) return;
        RemoveCast(result.Action.ActionId);
        if (result.Outcome != CombatSkillActionOutcome.Completed || result.Action.Actor == null) return;
        var actor = result.Action.Actor;
        var context = result.Action.Context;
        var id = result.Action.SkillId;
        Vector3 self = SkillVfxEffect.FootPosition(actor.transform);
        Vector3 target = TargetPosition(context, self);
        Vector3 point = context.HasTargetPoint ? context.TargetPoint : target;
        if (id == SkillId.Rosary_SacrificeThunder)
        {
            for (int i = 0; i < result.Effects.Count; i++)
            {
                var hit = result.Effects[i];
                if (hit.Kind == CombatActionEffectKind.Damage && hit.Target != null)
                    Play(id, self, SkillVfxEffect.FootPosition(hit.Target.transform), point, SkillVfxEffect.Phase.Impact);
                if (hit.Kind == CombatActionEffectKind.MagicStoneDamage)
                    for (int j = 0; j < context.ResolvedStones.Count; j++)
                    {
                        var stone = context.ResolvedStones[j];
                        if (stone != null && stone.FeatureIndex == hit.MagicStoneFeatureIndex)
                            Play(id, self, SkillVfxEffect.FootPosition(stone.transform), point, SkillVfxEffect.Phase.Impact);
                    }
            }
            return;
        }
        if (SkillVfxEffect.IsPersistent(id))
        {
            if (id == SkillId.Rosary_HealingArea)
            {
                var zones = FindObjectsByType<RosaryHealingAreaZone>();
                RosaryHealingAreaZone zone = null;
                for (int i = zones.Length - 1; i >= 0; i--)
                {
                    if (zones[i].Owner != actor || (zones[i].transform.position - point).sqrMagnitude >= .001f) continue;
                    bool claimed = false;
                    for (int j = 0; j < _active.Count; j++) if (_active[j].BoundZone == zones[i]) { claimed = true; break; }
                    if (!claimed) { zone = zones[i]; break; }
                }
                var area = Play(id, self, target, point, SkillVfxEffect.Phase.Impact, 0, result.Action.Skill.AreaRadius);
                area?.Bind(actor, null, null, zone);
                return;
            }
            for (int i = 0; i < result.Effects.Count; i++)
            {
                var hit = result.Effects[i];
                if (hit.Kind != CombatActionEffectKind.StatusApplied && hit.Kind != CombatActionEffectKind.StatusRefreshed &&
                    hit.Kind != CombatActionEffectKind.PersistentEffectStarted) continue;
                Character recipient = hit.Target;
                if (recipient == null) continue;
                for (int j = _active.Count - 1; j >= 0; j--)
                    if (_active[j].Skill == id && _active[j].FollowCharacter == recipient) Release(j);
                var persistent = Play(id, self, SkillVfxEffect.FootPosition(recipient.transform), point, SkillVfxEffect.Phase.Impact);
                persistent?.Bind(actor, recipient, hit.StatusKey);
            }
            return;
        }
        var effect = Play(id, self, target, point, SkillVfxEffect.Phase.Impact, 0, result.Action.Skill.AreaRadius);
        effect?.Bind(actor, context.PrimaryTarget, null);
    }

    private SkillVfxEffect Play(SkillId id, Vector3 self, Vector3 target, Vector3 point,
        SkillVfxEffect.Phase phase, float castTime = 0, float radius = 3)
    {
        if (_active.Count >= Capacity) { DroppedCount++; return null; }
        SkillVfxEffect effect;
        if (_pool.Count > 0) effect = _pool.Pop();
        else
        {
            var go = new GameObject("Pooled skill VFX");
            go.transform.SetParent(_spawnRoot != null ? _spawnRoot : transform, false);
            effect = go.AddComponent<SkillVfxEffect>();
        }
        effect.Prepare(id, self, target, point, phase, castTime, radius);
        _active.Add(effect);
        PeakActiveCount = Mathf.Max(PeakActiveCount, _active.Count);
        return effect;
    }

    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--) Release(i);
    }
    private void Release(int i)
    {
        var effect = _active[i];
        _active.RemoveAt(i);
        if (effect == null) return;
        effect.gameObject.SetActive(false);
        _pool.Push(effect);
    }
    private void Update()
    {
        using var sample = UpdateMarker.Auto();
        bool running = CombatBattleFlow.IsRunning;
        if (_wasCombatRunning && !running) ClearAll();
        _wasCombatRunning = running;
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i] == null || !_active[i].Tick(Time.deltaTime)) Release(i);
    }
    private static Transform TargetTransform(SkillExecutionContext context)
    {
        if (context.PrimaryTarget != null) return context.PrimaryTarget.transform;
        if (context.PrimaryStone != null) return context.PrimaryStone.transform;
        if (context.ResolvedTargets.Count > 0 && context.ResolvedTargets[0] != null) return context.ResolvedTargets[0].transform;
        if (context.ResolvedStones.Count > 0 && context.ResolvedStones[0] != null) return context.ResolvedStones[0].transform;
        return null;
    }
    private static Vector3 TargetPosition(SkillExecutionContext context, Vector3 fallback)
    {
        Transform target = TargetTransform(context);
        return target != null ? SkillVfxEffect.FootPosition(target) : context.HasTargetPoint ? context.TargetPoint : fallback;
    }
}
