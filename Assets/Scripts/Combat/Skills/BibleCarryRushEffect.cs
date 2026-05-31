using UnityEngine;

public sealed class BibleCarryRushEffect : MonoBehaviour
{
    private Character _carrier;
    private Character _passenger;
    private CombatCharacterBody _carrierBody;
    private CombatCharacterBody _passengerBody;
    private float _carrierBaseSpeed;
    private float _passengerBaseSpeed;
    private float _speedMultiplier;
    private float _expiresAt;
    private bool _hasAppliedSpeedBoost;

    public void Initialize(
        Character carrier,
        Character passenger,
        float speedMultiplier,
        float durationSeconds)
    {
        RestoreBaseSpeeds();

        _carrier = carrier;
        _passenger = passenger;
        _carrierBody = carrier != null ? carrier.GetComponent<CombatCharacterBody>() : null;
        _passengerBody = passenger != null ? passenger.GetComponent<CombatCharacterBody>() : null;
        _carrierBaseSpeed = _carrierBody != null ? _carrierBody.BaseSpeed : 0f;
        _passengerBaseSpeed = _passengerBody != null ? _passengerBody.BaseSpeed : 0f;
        _speedMultiplier = Mathf.Max(1f, speedMultiplier);
        _expiresAt = Time.time + Mathf.Max(0f, durationSeconds);

        if (_carrierBody != null)
        {
            _carrierBody.BaseSpeed = _carrierBaseSpeed * _speedMultiplier;
        }

        if (_passengerBody != null)
        {
            _passengerBody.BaseSpeed = _passengerBaseSpeed * _speedMultiplier;
        }

        if (_carrier != null && _passenger != null)
        {
            _passenger.transform.position = _carrier.transform.position;
        }

        _hasAppliedSpeedBoost = true;
    }

    private void Update()
    {
        if (_carrier == null ||
            _passenger == null ||
            _carrier.Health == null ||
            _passenger.Health == null ||
            !_carrier.Health.IsAlive ||
            !_passenger.Health.IsAlive)
        {
            RestoreBaseSpeeds();
            DestroySelf();
            return;
        }

        _passenger.transform.position = _carrier.transform.position;

        if (Time.time >= _expiresAt)
        {
            RestoreBaseSpeeds();
            DestroySelf();
        }
    }

    private void OnDestroy()
    {
        RestoreBaseSpeeds();
    }

    private void RestoreBaseSpeeds()
    {
        if (!_hasAppliedSpeedBoost) return;

        if (_carrierBody != null)
        {
            _carrierBody.BaseSpeed = _carrierBaseSpeed;
        }

        if (_passengerBody != null)
        {
            _passengerBody.BaseSpeed = _passengerBaseSpeed;
        }

        _hasAppliedSpeedBoost = false;
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
