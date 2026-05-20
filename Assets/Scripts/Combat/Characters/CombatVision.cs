using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
public sealed class CombatVision : MonoBehaviour
{
    [SerializeField] private CombatCharacterSystem _characterSystem;

    [Header("Sight")]
    [SerializeField] private Vector3 _headOffsetFromFoot = new Vector3(0f, 1f, 0f);
    [SerializeField, Range(1f, 180f)] private float _verticalFov = 90f;
    [SerializeField, Range(1f, 360f)] private float _horizontalFov = 120f;
    [SerializeField, Min(0.1f)] private float _maxSightDistance = 30f;
    [SerializeField, Min(0f)] private float _searchTimeout = 10f;
    [SerializeField] private LayerMask _obstructionLayers = ~0;
    [SerializeField] private bool _ignoreCharacterLayer = true;
    [SerializeField] private bool _drawDebugRays = false;
    [SerializeField, Min(0f)] private float _highGroundBroadcastThreshold = 5f;

    private readonly Dictionary<Character, Vector3?> _lastSeenPositions = new();
    private readonly Dictionary<Character, float> _lastSeenTime = new();
    private readonly List<Character> _visibleEnemies = new();
    private readonly List<Character> _rememberedEnemies = new();
    private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[32];

    private Character _owner;
    private CombatMapSystem _mapSystem;

    public IReadOnlyList<Character> VisibleEnemies => _visibleEnemies;
    public IReadOnlyList<Character> RememberedEnemies => _rememberedEnemies;
    public float SearchTimeoutSeconds => _searchTimeout;

    private void Awake()
    {
        _owner = GetComponent<Character>();
    }

    public void Initialize()
    {
        _owner ??= GetComponent<Character>();
        _lastSeenPositions.Clear();
        _lastSeenTime.Clear();
        _visibleEnemies.Clear();
        _rememberedEnemies.Clear();

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        IReadOnlyList<Character> enemies = characterSystem.GetEnemiesOf(_owner);
        for (int i = 0; i < enemies.Count; i++)
        {
            Character enemy = enemies[i];
            if (enemy == null || enemy == _owner) continue;

            _lastSeenPositions[enemy] = null;
            _lastSeenTime[enemy] = Time.time - _searchTimeout - 1f;
        }
    }

    public void UpdateVision()
    {
        _owner ??= GetComponent<Character>();
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        IReadOnlyList<Character> enemies = characterSystem.GetEnemiesOf(_owner);
        SyncTrackedEnemies(enemies);
        _visibleEnemies.Clear();

        bool canBroadcastObservation = IsOnHighGroundAndSharesObservation();

        foreach (Character enemy in enemies)
        {
            if (enemy == null || enemy == _owner) continue;

            // Line-of-sight loss (forest, obstacles) only affects visibility, not memory until timeout.
            bool visible = HasLineOfSight(enemy.transform);
            if (visible)
            {
                _visibleEnemies.Add(enemy);
                _lastSeenPositions[enemy] = enemy.transform.position;
                _lastSeenTime[enemy] = Time.time;

                if (canBroadcastObservation)
                {
                    BroadcastObservationToAllies(enemy, enemy.transform.position, Time.time);
                }
            }
            else if (_lastSeenTime.TryGetValue(enemy, out float lastSeenAt) &&
                Time.time - lastSeenAt > _searchTimeout)
            {
                _lastSeenPositions[enemy] = null;
            }
        }

        RebuildRememberedEnemies(enemies);
    }

    public bool IsVisible(Character target)
    {
        return target != null && _visibleEnemies.Contains(target);
    }

    public bool TryGetLastKnownPosition(Character target, out Vector3 position)
    {
        position = default;
        if (target == null) return false;
        if (!_lastSeenPositions.TryGetValue(target, out Vector3? stored) || !stored.HasValue) return false;

        position = stored.Value;
        return true;
    }

    public bool HasMemoryOf(Character target)
    {
        if (target == null) return false;
        if (!_lastSeenPositions.TryGetValue(target, out Vector3? stored) || !stored.HasValue) return false;
        if (!_lastSeenTime.TryGetValue(target, out float lastSeenAt)) return false;

        return Time.time - lastSeenAt <= _searchTimeout;
    }

    public float GetMemoryAgeSeconds(Character target)
    {
        if (target == null || !_lastSeenTime.TryGetValue(target, out float lastSeenAt)) return float.PositiveInfinity;
        if (!HasMemoryOf(target)) return float.PositiveInfinity;

        return Time.time - lastSeenAt;
    }

    public float GetMemoryRemainingSeconds(Character target)
    {
        if (!HasMemoryOf(target)) return 0f;

        return Mathf.Max(0f, _searchTimeout - GetMemoryAgeSeconds(target));
    }

    public void ReceiveSharedObservation(Character enemy, Vector3 position, float reportedAt)
    {
        if (enemy == null) return;
        if (_lastSeenTime.TryGetValue(enemy, out float mine) && mine >= reportedAt) return;

        _lastSeenPositions[enemy] = position;
        _lastSeenTime[enemy] = reportedAt;
        RebuildRememberedEnemiesFromTracked();
    }

    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector3 headPos = transform.TransformPoint(_headOffsetFromFoot);
        Vector3 targetHeadPos = target.TransformPoint(_headOffsetFromFoot);
        Vector3 diff = targetHeadPos - headPos;
        float distanceToTarget = diff.magnitude;

        if (distanceToTarget < Mathf.Epsilon) return true;
        if (distanceToTarget > _maxSightDistance) return false;

        Vector3 dirToTarget = diff / distanceToTarget;
        Vector3 localDir = transform.InverseTransformDirection(dirToTarget);

        float horizontalAngle = Mathf.Abs(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg);
        if (horizontalAngle > _horizontalFov * 0.5f) return false;

        float verticalAngle = Mathf.Abs(Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg);
        if (verticalAngle > _verticalFov * 0.5f) return false;

        int layerMask = _obstructionLayers;
        if (_ignoreCharacterLayer)
        {
            layerMask &= ~LayerMask.GetMask("Character");
        }

        if (_drawDebugRays)
        {
            Debug.DrawRay(headPos, dirToTarget * distanceToTarget, Color.red, 1f);
        }

        int hitCount = Physics.RaycastNonAlloc(
            headPos,
            dirToTarget,
            _lineOfSightHits,
            distanceToTarget,
            layerMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = _lineOfSightHits[i].transform;
            if (IsPartOfTransform(hitTransform, transform) || IsPartOfTransform(hitTransform, target))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsPartOfTransform(Transform candidate, Transform root)
    {
        if (candidate == null || root == null) return false;
        return candidate == root || candidate.IsChildOf(root);
    }

    private void RebuildRememberedEnemies(IReadOnlyList<Character> enemies)
    {
        _rememberedEnemies.Clear();
        for (int i = 0; i < enemies.Count; i++)
        {
            Character enemy = enemies[i];
            if (enemy == null || enemy == _owner) continue;
            if (!HasMemoryOf(enemy)) continue;

            _rememberedEnemies.Add(enemy);
        }
    }

    private void RebuildRememberedEnemiesFromTracked()
    {
        _rememberedEnemies.Clear();
        foreach (KeyValuePair<Character, Vector3?> pair in _lastSeenPositions)
        {
            if (pair.Key == null || pair.Key == _owner) continue;
            if (!HasMemoryOf(pair.Key)) continue;

            _rememberedEnemies.Add(pair.Key);
        }
    }

    private void SyncTrackedEnemies(IReadOnlyList<Character> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Character enemy = enemies[i];
            if (enemy == null || enemy == _owner || _lastSeenPositions.ContainsKey(enemy)) continue;

            _lastSeenPositions[enemy] = null;
            _lastSeenTime[enemy] = Time.time - _searchTimeout - 1f;
        }
    }

    private CombatCharacterSystem ResolveCharacterSystem()
    {
        if (_characterSystem != null) return _characterSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            _characterSystem = context.CharacterSystem;
            return _characterSystem;
        }

        _characterSystem = FindAnyObjectByType<CombatCharacterSystem>();
        return _characterSystem;
    }

    private CombatMapSystem ResolveMapSystem()
    {
        if (_mapSystem != null) return _mapSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.MapSystem != null)
        {
            _mapSystem = context.MapSystem;
            return _mapSystem;
        }

        _mapSystem = FindAnyObjectByType<CombatMapSystem>();
        return _mapSystem;
    }

    private bool IsOnHighGroundAndSharesObservation()
    {
        _owner ??= GetComponent<Character>();
        WeaponBase weapon = _owner != null ? _owner.EquippedWeapon : null;
        if (weapon == null || !weapon.SharesObservationFromHighGround) return false;

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null || !mapSystem.TryGetTerrainInfo(transform.position, out TerrainInfo terrain))
        {
            return false;
        }

        return terrain.Height >= _highGroundBroadcastThreshold;
    }

    private void BroadcastObservationToAllies(Character enemy, Vector3 position, float time)
    {
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        IReadOnlyList<Character> allies = characterSystem.GetAlliesOf(_owner);
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally == _owner) continue;

            ally.Vision?.ReceiveSharedObservation(enemy, position, time);
        }
    }
}
