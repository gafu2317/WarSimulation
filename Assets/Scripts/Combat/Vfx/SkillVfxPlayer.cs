using System.Collections.Generic;
using UnityEngine;

public sealed class SkillVfxPlayer : MonoBehaviour
{
    [SerializeField] private SkillVfxCatalog _catalog;
    [SerializeField] private Transform _spawnRoot;
    [SerializeField, Min(0.1f)] private float _defaultLifetimeSeconds = 2f;

    private readonly List<GameObject> _alive = new();
    private bool _wasCombatRunning;
    private GameObject _lastPlayed;

    internal void SetCatalog(SkillVfxCatalog catalog)
    {
        _catalog = catalog;
    }

    public bool TryPlay(
        SkillId skillId,
        Vector3 selfPosition,
        Vector3? targetPosition,
        Vector3? pointPosition,
        out string message)
    {
        _lastPlayed = null;
        ClearFinished();

        Transform parent = _spawnRoot != null ? _spawnRoot : transform;
        if (_catalog != null &&
            _catalog.TryGetEntry(skillId, out SkillVfxCatalog.Entry entry) &&
            entry.Prefab != null &&
            !entry.Prefab.name.StartsWith("Placeholder"))
        {
            return TryPlayPrefab(
                entry,
                skillId,
                selfPosition,
                targetPosition,
                pointPosition,
                parent,
                out message);
        }

        if (!SkillVfxProceduralFactory.TryCreate(
                skillId,
                selfPosition,
                targetPosition,
                pointPosition,
                parent,
                out GameObject procedural,
                out float proceduralLifetime))
        {
            message = $"{skillId} のエフェクト定義がありません。";
            return false;
        }

        _alive.Add(procedural);
        _lastPlayed = procedural;
        Destroy(procedural, proceduralLifetime);
        message = $"{skillId} → Procedural ({proceduralLifetime:0.##}s)";
        return true;
    }

    public void PlayAction(CombatSkillActionResult result)
    {
        if (result == null ||
            result.Outcome == CombatSkillActionOutcome.Failed ||
            result.Outcome == CombatSkillActionOutcome.Cancelled ||
            result.Outcome == CombatSkillActionOutcome.NoEffect ||
            result.Action.Actor == null)
        {
            return;
        }

        Character actor = result.Action.Actor;
        SkillExecutionContext context = result.Action.Context;
        Vector3 self = actor.transform.position;
        Vector3? target = ResolveTargetPosition(context);
        Vector3? point = context.HasTargetPoint ? context.TargetPoint : null;

        if (result.Action.SkillId == SkillId.Rosary_SacrificeThunder &&
            (context.ResolvedTargets.Count > 0 || context.ResolvedStones.Count > 0))
        {
            Transform parent = _spawnRoot != null ? _spawnRoot : transform;
            var hitCharacters = new HashSet<Character>();
            var hitStoneIndices = new HashSet<int>();
            bool paidSelfCost = false;
            for (int i = 0; i < result.Effects.Count; i++)
            {
                CombatActionEffect effect = result.Effects[i];
                if (effect.Kind == CombatActionEffectKind.Damage && effect.Target == actor)
                {
                    paidSelfCost = true;
                }
                else if (effect.Kind == CombatActionEffectKind.Damage &&
                    effect.Target != null &&
                    effect.Target != actor)
                {
                    hitCharacters.Add(effect.Target);
                }
                else if (effect.Kind == CombatActionEffectKind.MagicStoneDamage)
                {
                    hitStoneIndices.Add(effect.MagicStoneFeatureIndex);
                }
            }

            bool customPrefab = HasCustomPrefab(result.Action.SkillId);
            if (paidSelfCost && !customPrefab)
            {
                Track(SkillVfxProceduralFactory.CreateSacrificeSelf(self, parent, out float lifetime), lifetime);
            }

            foreach (Character hit in hitCharacters)
            {
                PlaySacrificeHit(hit.transform.position, self, point, parent, customPrefab);
            }

            for (int i = 0; i < context.ResolvedStones.Count; i++)
            {
                MagicStone resolved = context.ResolvedStones[i];
                if (resolved == null || !hitStoneIndices.Contains(resolved.FeatureIndex)) continue;
                PlaySacrificeHit(resolved.transform.position, self, point, parent, customPrefab);
            }
            return;
        }

        TryPlay(result.Action.SkillId, self, target, point, out _);
        if (_lastPlayed == null || !ShouldFollowCharacter(result.Action.SkillId)) return;

        Transform follow = ResolveFollowTarget(result.Action.SkillId, actor, context);
        if (follow != null)
        {
            _lastPlayed.transform.SetParent(follow, true);
        }
    }

    private bool TryPlayPrefab(
        SkillVfxCatalog.Entry entry,
        SkillId skillId,
        Vector3 selfPosition,
        Vector3? targetPosition,
        Vector3? pointPosition,
        Transform parent,
        out string message)
    {
        if (!TryResolvePosition(
                entry.Anchor,
                selfPosition,
                targetPosition,
                pointPosition,
                out Vector3 position,
                out string resolveError))
        {
            message = resolveError;
            return false;
        }

        GameObject instance = Instantiate(entry.Prefab, position + entry.WorldOffset, Quaternion.identity, parent);
        instance.name = $"Vfx_{skillId}_{entry.Prefab.name}";
        _alive.Add(instance);
        _lastPlayed = instance;

        float lifetime = entry.LifetimeSeconds > 0f
            ? entry.LifetimeSeconds
            : ResolveLifetime(instance, _defaultLifetimeSeconds);
        Destroy(instance, lifetime);

        message = $"{skillId} → {entry.Prefab.name} @ {entry.Anchor} ({lifetime:0.##}s)";
        return true;
    }

    private static Vector3? ResolveTargetPosition(SkillExecutionContext context)
    {
        if (context.PrimaryTarget != null) return context.PrimaryTarget.transform.position;
        if (context.PrimaryStone != null) return context.PrimaryStone.transform.position;
        if (context.ResolvedTargets.Count > 0 && context.ResolvedTargets[0] != null)
        {
            return context.ResolvedTargets[0].transform.position;
        }
        if (context.ResolvedStones.Count > 0 && context.ResolvedStones[0] != null)
        {
            return context.ResolvedStones[0].transform.position;
        }
        return null;
    }

    private static bool ShouldFollowCharacter(SkillId skillId)
    {
        return skillId == SkillId.Grimoire_StrDebuff ||
            skillId == SkillId.StatDebuff_INT ||
            skillId == SkillId.StatDebuff_FAI ||
            skillId == SkillId.StatDebuff_AGI ||
            skillId == SkillId.Grimoire_Bind ||
            skillId == SkillId.Grimoire_Poison ||
            skillId == SkillId.Grimoire_Stealth ||
            skillId == SkillId.Bible_Invulnerable ||
            skillId == SkillId.Bible_Gotsume ||
            skillId == SkillId.Bible_CarryRush ||
            skillId == SkillId.Rosary_Regeneration ||
            skillId == SkillId.Shield_ShoulderGuard;
    }

    private static Transform ResolveFollowTarget(
        SkillId skillId,
        Character actor,
        SkillExecutionContext context)
    {
        if (skillId == SkillId.Grimoire_Stealth ||
            skillId == SkillId.Bible_Invulnerable ||
            skillId == SkillId.Bible_CarryRush)
        {
            return actor.transform;
        }

        return context.PrimaryTarget != null ? context.PrimaryTarget.transform : null;
    }

    private bool HasCustomPrefab(SkillId skillId)
    {
        return _catalog != null &&
            _catalog.TryGetEntry(skillId, out SkillVfxCatalog.Entry entry) &&
            entry.Prefab != null &&
            !entry.Prefab.name.StartsWith("Placeholder");
    }

    private void PlaySacrificeHit(
        Vector3 hitPosition,
        Vector3 self,
        Vector3? point,
        Transform parent,
        bool customPrefab)
    {
        if (customPrefab)
        {
            TryPlay(SkillId.Rosary_SacrificeThunder, self, hitPosition, point, out _);
            return;
        }

        Track(
            SkillVfxProceduralFactory.CreateSacrificeBolt(self, hitPosition, parent, out float lifetime),
            lifetime);
    }

    private void Track(GameObject instance, float lifetime)
    {
        _alive.Add(instance);
        Destroy(instance, lifetime);
    }

    public void ClearAll()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] != null)
            {
                Destroy(_alive[i]);
            }
        }

        _alive.Clear();
        _lastPlayed = null;
    }

    private void Update()
    {
        bool combatRunning = CombatBattleFlow.IsRunning;
        if (!_wasCombatRunning && combatRunning)
        {
            ClearAll();
        }
        _wasCombatRunning = combatRunning;
        ClearFinished();
    }

    private void ClearFinished()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
            {
                _alive.RemoveAt(i);
            }
        }
    }

    private static bool TryResolvePosition(
        SkillVfxSpawnAnchor anchor,
        Vector3 selfPosition,
        Vector3? targetPosition,
        Vector3? pointPosition,
        out Vector3 position,
        out string error)
    {
        switch (anchor)
        {
            case SkillVfxSpawnAnchor.Self:
                position = selfPosition;
                error = null;
                return true;
            case SkillVfxSpawnAnchor.Target:
                if (!targetPosition.HasValue)
                {
                    position = default;
                    error = "Target アンカーだが対象位置がありません。";
                    return false;
                }

                position = targetPosition.Value;
                error = null;
                return true;
            case SkillVfxSpawnAnchor.Point:
                if (!pointPosition.HasValue)
                {
                    position = default;
                    error = "Point アンカーだが地点がありません。";
                    return false;
                }

                position = pointPosition.Value;
                error = null;
                return true;
            default:
                position = default;
                error = $"未対応のアンカー: {anchor}";
                return false;
        }
    }

    private static float ResolveLifetime(GameObject instance, float fallbackSeconds)
    {
        float max = 0f;
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            float duration = main.duration;
            if (main.loop)
            {
                return fallbackSeconds;
            }

            max = Mathf.Max(max, duration + main.startLifetime.constantMax);
        }

        return max > 0.05f ? max : fallbackSeconds;
    }
}
