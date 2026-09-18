using UnityEngine;

public sealed class ShieldTauntTargetEffect : MonoBehaviour
{
    private Character _source;
    private float _expiresAt;

    public Character Source => _source;
    public bool IsActive => isActiveAndEnabled &&
        Time.time < _expiresAt &&
        _source != null &&
        _source.Health != null &&
        _source.Health.IsAlive &&
        _source.StatusEffects != null &&
        _source.StatusEffects.HasActiveEffect(ShieldTauntSkill.EffectKey);

    public void Initialize(Character source, float durationSeconds)
    {
        _source = source;
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);
    }

    public void CancelImmediate()
    {
        if (Application.isPlaying)
        {
            Destroy(this);
            return;
        }

        DestroyImmediate(this);
    }

    private void Update()
    {
        if (!IsActive) CancelImmediate();
    }
}
