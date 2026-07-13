using UnityEngine;
using UnityEngine.AI;

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
    private Transform _passengerOriginalParent;
    private NavMeshAgent _passengerAgent;
    private bool _passengerAgentWasEnabled;
    private bool _hasAttachedPassenger;

    public void Initialize(
        Character carrier,
        Character passenger,
        float speedMultiplier,
        float durationSeconds)
    {
        ReleasePassenger();
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
            AttachPassenger();
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
            ReleasePassenger();
            DestroySelf();
            return;
        }

        if (Time.time >= _expiresAt)
        {
            RestoreBaseSpeeds();
            ReleasePassenger();
            DestroySelf();
        }
    }

    private void OnDestroy()
    {
        RestoreBaseSpeeds();
        ReleasePassenger();
    }

    public void CancelImmediate()
    {
        enabled = false;
        RestoreBaseSpeeds();
        ReleasePassenger();
        DestroySelf();
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

    private void AttachPassenger()
    {
        if (_carrier == null || _passenger == null) return;

        _passengerBody?.Stop();
        _passengerOriginalParent = _passenger.transform.parent;
        _passengerAgent = _passenger.GetComponent<NavMeshAgent>();
        if (_passengerAgent != null)
        {
            _passengerAgentWasEnabled = _passengerAgent.enabled;
            _passengerAgent.enabled = false;
        }

        _passenger.transform.SetParent(_carrier.transform, worldPositionStays: true);
        _hasAttachedPassenger = true;
    }

    private void ReleasePassenger()
    {
        if (!_hasAttachedPassenger || _passenger == null) return;

        Transform passengerTransform = _passenger.transform;
        passengerTransform.SetParent(_passengerOriginalParent, worldPositionStays: true);
        if (_passengerAgent != null && _passengerAgentWasEnabled)
        {
            if (NavMesh.SamplePosition(passengerTransform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                passengerTransform.position = hit.position;
            }

            _passengerAgent.enabled = true;
        }

        _passengerOriginalParent = null;
        _passengerAgent = null;
        _passengerAgentWasEnabled = false;
        _hasAttachedPassenger = false;
    }
}
