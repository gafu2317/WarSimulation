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

    private readonly Dictionary<Character, Vector3?> _lastSeenPositions = new();
    private readonly Dictionary<Character, float> _lastSeenTime = new();
    private readonly List<Character> _visibleEnemies = new();
    private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[32];

    private Character _owner;

    public IReadOnlyList<Character> VisibleEnemies => _visibleEnemies;

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

        foreach (Character enemy in enemies)
        {
            if (enemy == null || enemy == _owner) continue;

            bool visible = HasLineOfSight(enemy.transform);
            if (visible)
            {
                _visibleEnemies.Add(enemy);
                _lastSeenPositions[enemy] = enemy.transform.position;
                _lastSeenTime[enemy] = Time.time;
            }
            else if (_lastSeenTime.TryGetValue(enemy, out float lastSeenAt) &&
                Time.time - lastSeenAt > _searchTimeout)
            {
                _lastSeenPositions[enemy] = null;
            }
        }
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
}
