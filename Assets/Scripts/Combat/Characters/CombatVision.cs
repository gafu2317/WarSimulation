using System.Collections.Generic;
using UnityEngine;

// 記憶を管理する専用クラス
public class CharacterMemory
{
    public Character Target { get; private set; }
    public Vector3? LastSeenPosition { get; set; }
    public float LastSeenTime { get; set; }

    public CharacterMemory(Character target, Vector3? position, float time)
    {
        Target = target;
        LastSeenPosition = position;
        LastSeenTime = time;
    }
}

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

    private readonly Dictionary<Character, CharacterMemory> _memories = new();
    private readonly List<Character> _visibleEnemies = new();
    private readonly List<Character> _rememberedEnemies = new();
    private readonly List<CharacterMemory> _memoriesToShare = new();
    private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[32];
    private readonly RaycastHit[] _communicationHits = new RaycastHit[32];

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
        _memories.Clear();
        _visibleEnemies.Clear();
        _rememberedEnemies.Clear();
        _memoriesToShare.Clear();

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

        bool canBroadcastObservation = IsOnHighGroundAndSharesObservation();

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

                if (IsTrackedInList(enemies, target))
                {
                    _visibleEnemies.Add(target);

                    if (canBroadcastObservation)
                    {
                        BroadcastObservationToAllies(target, target.transform.position, Time.time);
                    }
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

    public void ReceiveSharedObservation(Character enemy, Vector3 position, float reportedAt)
    {
        if (enemy == null) return;
        if (enemy == _owner) return;

        CharacterMemory memory = EnsureTracked(enemy);
        if (memory.LastSeenTime >= reportedAt) return;

        memory.LastSeenPosition = position;
        memory.LastSeenTime = reportedAt;
        RebuildRememberedEnemiesFromTracked();
    }

    // 味方から情報を受け取る
    public void ReceiveSharedMemory(List<CharacterMemory> sharedMemories)
    {
        if (sharedMemories == null) return;

        for (int i = 0; i < sharedMemories.Count; i++)
        {
            CharacterMemory sharedMemory = sharedMemories[i];
            if (sharedMemory == null || sharedMemory.Target == null) continue;
            if (sharedMemory.Target == _owner) continue;

            CharacterMemory myMemory = EnsureTracked(sharedMemory.Target);
            if (sharedMemory.LastSeenTime <= myMemory.LastSeenTime) continue;

            myMemory.LastSeenPosition = sharedMemory.LastSeenPosition;
            myMemory.LastSeenTime = sharedMemory.LastSeenTime;
        }

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
        int layerMask = _obstructionLayers;
        if (_ignoreCharacterLayer)
        {
            layerMask &= ~LayerMask.GetMask("Character");
        }

        if (_drawDebugRays)
        {
            Debug.DrawRay(myHeadPos, dirToAlly * distance, Color.green, 1f);
        }

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

        _memoriesToShare.Clear();
        foreach (CharacterMemory memory in _memories.Values)
        {
            _memoriesToShare.Add(memory);
        }

        IReadOnlyList<Character> allies = characterSystem.GetAlliesOf(_owner);
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally == _owner || ally.HP <= 0) continue;
            if (!CanCommunicateWith(ally)) continue;

            ally.Vision?.ReceiveSharedMemory(_memoriesToShare);
        }
    }
}
