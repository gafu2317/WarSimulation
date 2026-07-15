using UnityEngine;

public sealed class ShieldShoulderGuardEffect : MonoBehaviour
{
    private Character _guardian;
    private Character _protectedTarget;
    private float _damageMultiplier;
    private float _expiresAt;
    private CombatEffectSource _source;

    public void Initialize(Character guardian, Character protectedTarget, float damageMultiplier, float durationSeconds)
    {
        CleanupSubscription();

        _guardian = guardian;
        _protectedTarget = protectedTarget;
        _damageMultiplier = Mathf.Max(0f, damageMultiplier);
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);
        _source = CombatEffectSource.Capture(guardian);

        if (_protectedTarget != null && _protectedTarget.Health != null)
        {
            _protectedTarget.Health.IncomingDamage += OnProtectedTargetIncomingDamage;
        }
    }

    private void Update()
    {
        if (_guardian == null ||
            _protectedTarget == null ||
            _guardian.Health == null ||
            _protectedTarget.Health == null ||
            Time.time >= _expiresAt)
        {
            DestroySelf();
        }
    }

    private void OnDestroy()
    {
        CleanupSubscription();
    }

    public void CancelImmediate()
    {
        enabled = false;
        CleanupSubscription();
        DestroySelf();
    }

    private void CleanupSubscription()
    {
        if (_protectedTarget != null && _protectedTarget.Health != null)
        {
            _protectedTarget.Health.IncomingDamage -= OnProtectedTargetIncomingDamage;
        }
    }

    private void OnProtectedTargetIncomingDamage(CombatHealth.IncomingDamageContext context)
    {
        if (context == null || context.IsHandled || context.Amount <= 0) return;
        if (_guardian == null || _protectedTarget == null) return;
        if (_guardian.Health == null || _protectedTarget.Health == null) return;
        if (!_guardian.Health.IsAlive || !_protectedTarget.Health.IsAlive) return;

        Character attacker = context.Attacker;
        if (attacker == null || attacker == _guardian) return;
        if (attacker.Team == _guardian.Team) return;

        CombatVision guardianVision = _guardian.Vision;
        guardianVision?.UpdateVision();
        if (guardianVision != null && !guardianVision.IsVisible(attacker)) return;

        context.IsHandled = true;
        context.PreventionSource = _source;
        int redirectedDamage = Mathf.Max(1, Mathf.RoundToInt(context.Amount * _damageMultiplier));
        _guardian.Health.TakeDamage(redirectedDamage, context.AttackSource);
    }

    private void DestroySelf()
    {
        if (Application.isPlaying)
        {
            Destroy(this);
            return;
        }

        DestroyImmediate(this);
    }
}
