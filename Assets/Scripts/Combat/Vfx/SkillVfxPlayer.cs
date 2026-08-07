using System.Collections.Generic;
using UnityEngine;

public sealed class SkillVfxPlayer : MonoBehaviour
{
    [SerializeField] private SkillVfxCatalog _catalog;
    [SerializeField] private Transform _spawnRoot;
    [SerializeField, Min(0.1f)] private float _defaultLifetimeSeconds = 2f;

    private readonly List<GameObject> _alive = new();

    public bool TryPlay(
        SkillId skillId,
        Vector3 selfPosition,
        Vector3? targetPosition,
        Vector3? pointPosition,
        out string message)
    {
        ClearFinished();

        if (_catalog == null)
        {
            message = "SkillVfxCatalog が未設定です。";
            return false;
        }

        if (!_catalog.TryGetEntry(skillId, out SkillVfxCatalog.Entry entry) || entry.Prefab == null)
        {
            message = $"{skillId} に Prefab が未登録です。";
            return false;
        }

        if (!TryResolvePosition(entry.Anchor, selfPosition, targetPosition, pointPosition, out Vector3 position, out string resolveError))
        {
            message = resolveError;
            return false;
        }

        Transform parent = _spawnRoot != null ? _spawnRoot : transform;
        GameObject instance = Instantiate(entry.Prefab, position + entry.WorldOffset, Quaternion.identity, parent);
        instance.name = $"Vfx_{skillId}_{entry.Prefab.name}";
        _alive.Add(instance);

        float lifetime = entry.LifetimeSeconds > 0f
            ? entry.LifetimeSeconds
            : ResolveLifetime(instance, _defaultLifetimeSeconds);
        Destroy(instance, lifetime);

        message = $"{skillId} → {entry.Prefab.name} @ {entry.Anchor} ({lifetime:0.##}s)";
        return true;
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
    }

    private void Update()
    {
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
