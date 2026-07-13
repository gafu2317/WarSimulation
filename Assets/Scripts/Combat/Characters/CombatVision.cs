using System.Collections.Generic;
using UnityEngine;

// 記憶を管理する専用クラス
public class CharacterMemory
{
    public Character Target { get; private set; }
    public Vector3? LastSeenPosition { get; set; }
    public float LastSeenTime { get; set; }
    public CombatVisionMemorySource Source { get; set; }
    public Character SharedFrom { get; set; }

    public CharacterMemory(Character target, Vector3? position, float time)
    {
        Target = target;
        LastSeenPosition = position;
        LastSeenTime = time;
        Source = CombatVisionMemorySource.DirectSight;
        SharedFrom = null;
    }
}

public enum CombatVisionMemorySource
{
    DirectSight = 0,
    Shared = 1,
}

public readonly struct CombatVisionDebugMemorySnapshot
{
    public Character Target { get; }
    public bool HasPosition { get; }
    public Vector3 LastSeenPosition { get; }
    public float RemainingSeconds { get; }
    public CombatVisionMemorySource Source { get; }
    public Character SharedFrom { get; }

    public CombatVisionDebugMemorySnapshot(
        Character target,
        bool hasPosition,
        Vector3 lastSeenPosition,
        float remainingSeconds,
        CombatVisionMemorySource source,
        Character sharedFrom)
    {
        Target = target;
        HasPosition = hasPosition;
        LastSeenPosition = lastSeenPosition;
        RemainingSeconds = remainingSeconds;
        Source = source;
        SharedFrom = sharedFrom;
    }
}

public readonly struct CombatVisionDebugCommunicationSnapshot
{
    public Character Ally { get; }
    public bool CanCommunicate { get; }

    public CombatVisionDebugCommunicationSnapshot(Character ally, bool canCommunicate)
    {
        Ally = ally;
        CanCommunicate = canCommunicate;
    }
}

[RequireComponent(typeof(Character))]
public sealed class CombatVision : MonoBehaviour
{
    private const string VisionObstacleLayerName = "VisionObstacle";
    private const float HorizontalFovDegreesValue = 160f;

    private CombatCharacterSystem _characterSystem;
    private CombatMapSystem _mapSystem;

    [Header("Sight")]
    [SerializeField] private Vector3 _headOffsetFromFoot = new Vector3(0f, 1f, 0f);
    [SerializeField, Range(1f, 180f)] private float _verticalFov = 90f;
    [SerializeField, Min(0f)] private float _minimumSightRange = 30f;
    [SerializeField, Min(0f)] private float _maximumSightRange = 100f;
    [SerializeField, Min(0f)] private float _searchTimeout = 5f;
    [SerializeField] private LayerMask _obstructionLayers = ~0;
    [SerializeField] private bool _ignoreCharacterLayer = true;

    private readonly Dictionary<Character, CharacterMemory> _memories = new();
    private readonly List<Character> _visibleEnemies = new();
    private readonly List<Character> _rememberedEnemies = new();
    private readonly List<CharacterMemory> _memoriesToShare = new();
    private readonly List<CombatVisionDebugMemorySnapshot> _debugMemorySnapshots = new();
    private readonly List<CombatVisionDebugCommunicationSnapshot> _debugCommunicationSnapshots = new();
    private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[32];
    private readonly RaycastHit[] _communicationHits = new RaycastHit[32];

    private Character _owner;
    private Character _lastSharedTo;
    private Character _lastReceivedFrom;
    private float _lastSharedAt = float.NegativeInfinity;
    private float _lastReceivedAt = float.NegativeInfinity;

    public IReadOnlyList<Character> VisibleEnemies => _visibleEnemies;
    public IReadOnlyList<Character> RememberedEnemies => _rememberedEnemies;
    public float SearchTimeoutSeconds => _searchTimeout;
    public float HorizontalFovDegrees => HorizontalFovDegreesValue;
    public float VerticalFovDegrees => _verticalFov;
    public Vector3 EyePosition => transform.TransformPoint(_headOffsetFromFoot);
    public float CurrentSightRange => GetCurrentSightRange();
    public Character LastSharedTo => _lastSharedTo;
    public Character LastReceivedFrom => _lastReceivedFrom;
    public float LastSharedAgeSeconds => _lastSharedTo != null ? Time.time - _lastSharedAt : float.PositiveInfinity;
    public float LastReceivedAgeSeconds => _lastReceivedFrom != null ? Time.time - _lastReceivedAt : float.PositiveInfinity;

    private void Awake()
    {
        _owner = GetComponent<Character>();
    }

    public void Initialize()
    {
        _owner ??= GetComponent<Character>();
        _memories.Clear();
        _visibleEnemies.Clear();
        _rememberedEnemies.Clear();
        _memoriesToShare.Clear();
        _debugMemorySnapshots.Clear();
        _debugCommunicationSnapshots.Clear();
        _lastSharedTo = null;
        _lastReceivedFrom = null;
        _lastSharedAt = float.NegativeInfinity;
        _lastReceivedAt = float.NegativeInfinity;

        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        float initialTime = Time.time - _searchTimeout - 1f;
        RegisterTrackedCharacters(characterSystem.GetEnemiesOf(_owner), initialTime);
        RegisterTrackedCharacters(characterSystem.GetAlliesOf(_owner), initialTime);
    }

    public void UpdateVision()
    {
        _owner ??= GetComponent<Character>();
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        IReadOnlyList<Character> enemies = characterSystem.GetEnemiesOf(_owner);
        IReadOnlyList<Character> allies = characterSystem.GetAlliesOf(_owner);
        SyncTrackedCharacters(enemies);
        SyncTrackedCharacters(allies);
        _visibleEnemies.Clear();

        foreach (KeyValuePair<Character, CharacterMemory> memoryPair in _memories)
        {
            Character target = memoryPair.Key;
            CharacterMemory memory = memoryPair.Value;
            if (target == null || target == _owner || memory == null) continue;

            bool visible = HasLineOfSight(target.transform);
            if (visible)
            {
                memory.LastSeenPosition = target.transform.position;
                memory.LastSeenTime = Time.time;
                memory.Source = CombatVisionMemorySource.DirectSight;
                memory.SharedFrom = null;

                if (IsTrackedInList(enemies, target))
                {
                    _visibleEnemies.Add(target);
                }
            }
            else if (Time.time - memory.LastSeenTime > _searchTimeout)
            {
                memory.LastSeenPosition = null;
            }
        }

        RebuildRememberedEnemies(enemies);
        BroadcastMemoriesToAllies();
    }

    public bool IsVisible(Character target)
    {
        return target != null && _visibleEnemies.Contains(target);
    }

    public bool HasRecognitionOf(Character target)
    {
        if (target == null) return false;
        return IsVisible(target) || HasMemoryOf(target);
    }

    public bool IsRecognizedBy(Character observer)
    {
        _owner ??= GetComponent<Character>();
        return observer != null &&
            _owner != null &&
            observer.Vision != null &&
            observer.Vision.HasRecognitionOf(_owner);
    }

    public bool TryGetLastKnownPosition(Character target, out Vector3 position)
    {
        position = default;
        if (target == null) return false;
        if (!_memories.TryGetValue(target, out CharacterMemory memory)) return false;
        if (!memory.LastSeenPosition.HasValue) return false;

        position = memory.LastSeenPosition.Value;
        return true;
    }

    public bool HasMemoryOf(Character target)
    {
        if (target == null) return false;
        if (!_memories.TryGetValue(target, out CharacterMemory memory)) return false;
        if (!memory.LastSeenPosition.HasValue) return false;

        return Time.time - memory.LastSeenTime <= _searchTimeout;
    }

    public float GetMemoryAgeSeconds(Character target)
    {
        if (target == null || !_memories.TryGetValue(target, out CharacterMemory memory)) return float.PositiveInfinity;
        if (!HasMemoryOf(target)) return float.PositiveInfinity;

        return Time.time - memory.LastSeenTime;
    }

    public float GetMemoryRemainingSeconds(Character target)
    {
        if (!HasMemoryOf(target)) return 0f;

        return Mathf.Max(0f, _searchTimeout - GetMemoryAgeSeconds(target));
    }

    // 味方から情報を受け取る
    public void ReceiveSharedMemory(Character sharedFrom, List<CharacterMemory> sharedMemories)
    {
        if (sharedMemories == null) return;

        for (int i = 0; i < sharedMemories.Count; i++)
        {
            CharacterMemory sharedMemory = sharedMemories[i];
            if (sharedMemory == null || sharedMemory.Target == null) continue;
            if (sharedMemory.Target == _owner) continue;
            if (!sharedMemory.LastSeenPosition.HasValue) continue;

            CharacterMemory myMemory = EnsureTracked(sharedMemory.Target);
            if (sharedMemory.LastSeenTime <= myMemory.LastSeenTime) continue;

            myMemory.LastSeenPosition = sharedMemory.LastSeenPosition;
            myMemory.LastSeenTime = sharedMemory.LastSeenTime;
            myMemory.Source = CombatVisionMemorySource.Shared;
            myMemory.SharedFrom = sharedFrom;
        }

        _lastReceivedFrom = sharedFrom;
        _lastReceivedAt = Time.time;
        RebuildRememberedEnemiesFromTracked();
    }

    public IReadOnlyList<CombatVisionDebugMemorySnapshot> GetDebugMemorySnapshots()
    {
        _debugMemorySnapshots.Clear();
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        IReadOnlyList<Character> enemies = characterSystem != null && _owner != null
            ? characterSystem.GetEnemiesOf(_owner)
            : null;

        foreach (KeyValuePair<Character, CharacterMemory> pair in _memories)
        {
            Character target = pair.Key;
            CharacterMemory memory = pair.Value;
            if (target == null || target == _owner || memory == null) continue;
            if (enemies != null && !IsTrackedInList(enemies, target)) continue;

            bool hasPosition = memory.LastSeenPosition.HasValue && Time.time - memory.LastSeenTime <= _searchTimeout;
            _debugMemorySnapshots.Add(new CombatVisionDebugMemorySnapshot(
                target,
                hasPosition,
                memory.LastSeenPosition.GetValueOrDefault(),
                hasPosition ? Mathf.Max(0f, _searchTimeout - (Time.time - memory.LastSeenTime)) : 0f,
                memory.Source,
                memory.SharedFrom));
        }

        return _debugMemorySnapshots;
    }

    public IReadOnlyList<CombatVisionDebugCommunicationSnapshot> GetDebugCommunicationSnapshots()
    {
        return _debugCommunicationSnapshots;
    }

    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Character targetCharacter = target.GetComponent<Character>();
        if (targetCharacter != null &&
            targetCharacter != _owner &&
            targetCharacter.StatusEffects != null &&
            targetCharacter.StatusEffects.IsStealthed)
        {
            return false;
        }

        if (!IsWithinFieldOfView(target)) return false;

        Vector3 headPos = transform.TransformPoint(_headOffsetFromFoot);
        Vector3 targetHeadPos = target.TransformPoint(_headOffsetFromFoot);
        Vector3 diff = targetHeadPos - headPos;
        float distanceToTarget = diff.magnitude;

        if (distanceToTarget < Mathf.Epsilon) return true;
        Vector2 horizontalDiff = new Vector2(diff.x, diff.z);
        float sightRange = GetCurrentSightRange();
        if (horizontalDiff.sqrMagnitude > sightRange * sightRange) return false;

        Vector3 dirToTarget = diff / distanceToTarget;

        int layerMask = ResolveObstructionLayerMask();

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

    public bool IsWithinFieldOfView(Transform target)
    {
        if (target == null) return false;

        Vector3 diff = target.TransformPoint(_headOffsetFromFoot) - transform.TransformPoint(_headOffsetFromFoot);
        if (diff.sqrMagnitude < Mathf.Epsilon) return true;

        Vector3 localDirection = transform.InverseTransformDirection(diff.normalized);
        float horizontalAngle = Mathf.Abs(Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg);
        if (horizontalAngle > HorizontalFovDegreesValue * 0.5f) return false;

        float verticalAngle = Mathf.Abs(Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg);
        return verticalAngle <= _verticalFov * 0.5f;
    }

    private float GetCurrentSightRange()
    {
        float minimumRange = Mathf.Min(_minimumSightRange, _maximumSightRange);
        float maximumRange = Mathf.Max(_minimumSightRange, _maximumSightRange);
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null ||
            !mapSystem.TryGetSightHeightContext(
                transform.position,
                out float currentHeight,
                out float minimumHeight,
                out float maximumHeight)) return minimumRange;

        float heightRange = maximumHeight - minimumHeight;
        if (heightRange <= Mathf.Epsilon) return minimumRange;

        float heightRatio = Mathf.InverseLerp(minimumHeight, maximumHeight, currentHeight);
        return Mathf.Lerp(minimumRange, maximumRange, heightRatio);
    }

    public bool TryGetSightRay(Transform target, out Vector3 origin, out Vector3 end, out bool blocked)
    {
        origin = transform.TransformPoint(_headOffsetFromFoot);
        end = target != null ? target.TransformPoint(_headOffsetFromFoot) : origin;
        blocked = false;
        if (target == null) return false;

        Vector3 diff = end - origin;
        float distance = diff.magnitude;
        if (distance < Mathf.Epsilon) return true;

        Vector3 direction = diff / distance;
        float horizontalDistance = new Vector2(diff.x, diff.z).magnitude;
        float rayDistance = distance;
        if (horizontalDistance > CurrentSightRange && horizontalDistance > Mathf.Epsilon)
        {
            rayDistance *= CurrentSightRange / horizontalDistance;
            end = origin + direction * rayDistance;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _lineOfSightHits,
            rayDistance,
            ResolveObstructionLayerMask(),
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _lineOfSightHits[i];
            Transform hitTransform = hit.transform;
            if (IsPartOfTransform(hitTransform, transform) || IsPartOfTransform(hitTransform, target)) continue;
            if (hit.distance >= nearestDistance) continue;

            nearestDistance = hit.distance;
            end = hit.point;
            blocked = true;
        }

        return true;
    }

    private int ResolveObstructionLayerMask()
    {
        int layerMask = _obstructionLayers;
        int visionObstacleLayer = LayerMask.NameToLayer(VisionObstacleLayerName);
        if (visionObstacleLayer >= 0)
        {
            layerMask |= 1 << visionObstacleLayer;
        }

        if (_ignoreCharacterLayer)
        {
            layerMask &= ~LayerMask.GetMask("Character");
        }

        return layerMask;
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
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        IReadOnlyList<Character> enemies = characterSystem != null && _owner != null
            ? characterSystem.GetEnemiesOf(_owner)
            : null;

        foreach (KeyValuePair<Character, CharacterMemory> pair in _memories)
        {
            if (pair.Key == null || pair.Key == _owner) continue;
            if (enemies != null && !IsTrackedInList(enemies, pair.Key)) continue;
            if (!HasMemoryOf(pair.Key)) continue;

            _rememberedEnemies.Add(pair.Key);
        }
    }

    private void SyncTrackedCharacters(IReadOnlyList<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || character == _owner || _memories.ContainsKey(character)) continue;

            _memories[character] = new CharacterMemory(character, null, Time.time - _searchTimeout - 1f);
        }
    }

    private void RegisterTrackedCharacters(IReadOnlyList<Character> characters, float initialTime)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || character == _owner || _memories.ContainsKey(character)) continue;

            _memories[character] = new CharacterMemory(character, null, initialTime);
        }
    }

    private CharacterMemory EnsureTracked(Character target)
    {
        if (_memories.TryGetValue(target, out CharacterMemory memory))
        {
            return memory;
        }

        memory = new CharacterMemory(target, null, Time.time - _searchTimeout - 1f);
        _memories[target] = memory;
        return memory;
    }

    private static bool IsTrackedInList(IReadOnlyList<Character> characters, Character target)
    {
        if (characters == null || target == null) return false;

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == target) return true;
        }

        return false;
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

        CombatCharacterSystem[] systems = FindObjectsByType<CombatCharacterSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (systems != null)
        {
            for (int i = systems.Length - 1; i >= 0; i--)
            {
                CombatCharacterSystem system = systems[i];
                if (_owner != null &&
                    (system.AllyCharacters.Contains(_owner) || system.EnemyCharacters.Contains(_owner)))
                {
                    _characterSystem = system;
                    return _characterSystem;
                }
            }

            _characterSystem = systems.Length > 0 ? systems[systems.Length - 1] : null;
        }

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

    // 指定した味方との間に障害物がないか判定する
    private bool CanCommunicateWith(Character ally)
    {
        if (ally == null) return false;

        Vector3 myHeadPos = transform.TransformPoint(_headOffsetFromFoot);
        Vector3 allyHeadPos = ally.transform.TransformPoint(_headOffsetFromFoot);
        Vector3 diff = allyHeadPos - myHeadPos;
        float distance = diff.magnitude;

        if (distance < Mathf.Epsilon) return true;

        Vector3 dirToAlly = diff / distance;
        int layerMask = ResolveObstructionLayerMask();

        int hitCount = Physics.RaycastNonAlloc(
            myHeadPos,
            dirToAlly,
            _communicationHits,
            distance,
            layerMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = _communicationHits[i].transform;
            if (IsPartOfTransform(hitTransform, transform) || IsPartOfTransform(hitTransform, ally.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // 通信可能な味方（Rayが通る相手）にだけ記憶を共有する
    private void BroadcastMemoriesToAllies()
    {
        CombatCharacterSystem characterSystem = ResolveCharacterSystem();
        if (characterSystem == null || _owner == null) return;

        _debugCommunicationSnapshots.Clear();
        _memoriesToShare.Clear();
        foreach (CharacterMemory memory in _memories.Values)
        {
            _memoriesToShare.Add(memory);
        }

        IReadOnlyList<Character> allies = characterSystem.GetAlliesOf(_owner);
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally == _owner) continue;

            bool canCommunicate = ally.HP > 0 && CanCommunicateWith(ally);
            _debugCommunicationSnapshots.Add(new CombatVisionDebugCommunicationSnapshot(ally, canCommunicate));
            if (!canCommunicate) continue;

            ally.Vision?.ReceiveSharedMemory(_owner, _memoriesToShare);
            _lastSharedTo = ally;
            _lastSharedAt = Time.time;
        }
    }
}
