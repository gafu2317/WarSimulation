using UnityEngine;

public sealed class RosaryHealingAreaZone : MonoBehaviour
{
    private Character _owner;
    private CombatEffectSource _source;
    private float _radius;
    private int _healPerTick;
    private float _expiresAt;
    private float _tickIntervalSeconds;
    private float _nextTickTime;

    public void Initialize(
        Character owner,
        float radius,
        int healPerTick,
        float durationSeconds,
        float tickIntervalSeconds)
    {
        _owner = owner;
        _source = CombatEffectSource.Capture(owner);
        _radius = radius;
        _healPerTick = healPerTick;
        _tickIntervalSeconds = Mathf.Max(0.01f, tickIntervalSeconds);
        _nextTickTime = Time.time + _tickIntervalSeconds;
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);
    }

    private void Update()
    {
        if (_owner == null)
        {
            DestroyZone();
            return;
        }

        float now = Time.time;
        while (now >= _nextTickTime && _nextTickTime <= _expiresAt)
        {
            ApplyTick();
            _nextTickTime += _tickIntervalSeconds;
        }

        if (now >= _expiresAt)
        {
            DestroyZone();
        }
    }

    private void ApplyTick()
    {
        var allies = CombatSkillTargeting.GetAlliesInRadius(_owner, transform.position, _radius, includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally.Health == null || !ally.Health.IsAlive) continue;

            ally.Health.Heal(_healPerTick, _source);
        }
    }

    public void CancelImmediate()
    {
        enabled = false;
        DestroyZone();
    }

    private void DestroyZone()
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }
}
