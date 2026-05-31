using UnityEngine;

public sealed class BibleGotsumeEffect : MonoBehaviour
{
    private Character _wearer;
    private int _reflectDamage;
    private float _expiresAt;

    public void Initialize(Character wearer, int reflectDamage, float durationSeconds)
    {
        _wearer = wearer;
        _reflectDamage = Mathf.Max(1, reflectDamage);
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);

        if (_wearer != null && _wearer.Health != null)
        {
            _wearer.Health.Damaged += OnWearerDamaged;
        }
    }

    private void Update()
    {
        if (_wearer == null || _wearer.Health == null || Time.time >= _expiresAt)
        {
            DestroySelf();
        }
    }

    private void OnDestroy()
    {
        if (_wearer != null && _wearer.Health != null)
        {
            _wearer.Health.Damaged -= OnWearerDamaged;
        }
    }

    private void OnWearerDamaged(int damage, Character attacker)
    {
        if (attacker == null || attacker == _wearer) return;
        if (attacker.Team == _wearer.Team) return;
        if (attacker.Health == null || !attacker.Health.IsTargetable) return;

        attacker.Health.TakeDamage(_reflectDamage, null);
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
