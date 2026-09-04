using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using WarSimulation.Combat.Map;

public class CombatCharacterSystem : MonoBehaviour
{
    public event Action CandidatesReady;
    private const int CandidateCountPerTeam = 10;
    private const string GeneratedCharactersRootName = "GeneratedCombatCharacters";
    private static readonly string[] DefaultAllyCandidateNames =
    {
        "砂狼シロコ", "小鳥遊ホシノ", "陸八魔アル", "空崎ヒナ", "浅黄ムツキ",
        "黒見セリカ", "十六夜ノノミ", "奥空アヤネ", "聖園ミカ", "早瀬ユウカ",
    };
    private static readonly string[] DefaultEnemyCandidateNames =
    {
        "杏山カズサ", "才羽モモイ", "才羽ミドリ", "天雨アコ", "銀鏡イオリ",
        "火宮チナツ", "愛清フウカ", "棗イロハ", "下江コハル", "浦和ハナコ",
    };
    private static readonly ProfilerMarker CollectAiBrainsMarker = new("CombatAI.CollectBrains");
    private static readonly ProfilerMarker ScanAiVisionMarker = new("CombatAI.ScanVision");
    private static readonly ProfilerMarker ShareAiVisionMarker = new("CombatAI.ShareVision");
    private static readonly ProfilerMarker PrepareAiDecisionsMarker = new("CombatAI.PrepareDecisions");
    private static readonly ProfilerMarker ExecuteAiDecisionsMarker = new("CombatAI.ExecuteDecisions");
    private static readonly ProfilerMarker InitialPlacementMarker =
        new("CombatLoading.InitialCharacterPlacement");
    private static readonly ProfilerMarker CharacterResetMarker =
        new("CombatLoading.CharacterReset");

    [SerializeField, Min(0.05f)] private float _aiDecisionIntervalSeconds = 0.5f;
    public List<Character> AllyCharacters = new List<Character>();
    public List<Character> EnemyCharacters = new List<Character>();

    [SerializeField] private bool _generateCandidatesAtRuntime;
    [SerializeField] private Character _characterPrefab;
    [SerializeField] private CombatMapSystem _mapSystem;

    private readonly Dictionary<Character, Vector3> _initialPositions = new Dictionary<Character, Vector3>();
    private readonly List<CombatAiBrain> _orderedAiBrains = new List<CombatAiBrain>();
    private readonly CombatAiTeamReservations _allyAiReservations = new CombatAiTeamReservations();
    private readonly CombatAiTeamReservations _enemyAiReservations = new CombatAiTeamReservations();
    private CombatAiDecisionSchedule _aiDecisionSchedule;
    private GameObject _generatedCharactersRoot;
    private CombatMagicStoneSystem _magicStoneSystem;
    private Character _allyMarkedStoneAttacker;
    private Character _enemyMarkedStoneAttacker;

    public int LastSkippedAiDecisionCount { get; private set; }
    public int TotalSkippedAiDecisionCount { get; private set; }

    public IReadOnlyList<Character> GetAlliesOf(Character character)
    {
        if (character == null) return System.Array.Empty<Character>();
        return character.Team == CombatTeam.Ally ? AllyCharacters : EnemyCharacters;
    }

    public IReadOnlyList<Character> GetEnemiesOf(Character character)
    {
        if (character == null) return System.Array.Empty<Character>();
        return character.Team == CombatTeam.Ally ? EnemyCharacters : AllyCharacters;
    }

    public Character GetMarkedStoneAttacker(Character character)
    {
        if (character == null) return null;

        Character marked = character.Team == CombatTeam.Ally
            ? _allyMarkedStoneAttacker
            : _enemyMarkedStoneAttacker;
        if (marked != null && marked.Team != character.Team && marked.Health != null && marked.Health.IsAlive)
        {
            return marked;
        }

        if (character.Team == CombatTeam.Ally) _allyMarkedStoneAttacker = null;
        else _enemyMarkedStoneAttacker = null;
        return null;
    }

    public void AssignTeamsFromLists()
    {
        AssignTeam(AllyCharacters, CombatTeam.Ally);
        AssignTeam(EnemyCharacters, CombatTeam.Enemy);
        AssignBattleParticipantIds();
    }

    public void SetParticipants(
        IReadOnlyList<Character> allies,
        IReadOnlyList<Character> enemies)
    {
        var previousCharacters = new HashSet<Character>(AllyCharacters);
        previousCharacters.UnionWith(EnemyCharacters);

        ReplaceParticipants(AllyCharacters, allies, CombatTeam.Ally);
        ReplaceParticipants(EnemyCharacters, enemies, CombatTeam.Enemy);

        var participants = new HashSet<Character>(AllyCharacters);
        participants.UnionWith(EnemyCharacters);
        foreach (Character character in previousCharacters)
        {
            if (character != null && !participants.Contains(character))
            {
                character.gameObject.SetActive(false);
            }
        }

        AssignBattleParticipantIds();
    }

    public void SetParticipants(
        IReadOnlyList<CombatParticipantSetup> allies,
        IReadOnlyList<CombatParticipantSetup> enemies)
    {
        var allyCharacters = ApplySetups(allies);
        var enemyCharacters = ApplySetups(enemies);
        SetParticipants(allyCharacters, enemyCharacters);
    }

    public bool TryGetHomePosition(Character character, out Vector3 homePosition)
    {
        homePosition = default;
        if (character == null) return false;

        RegisterInitialPosition(character);

        if (TryGetMainStonePositionForTeam(character.Team, out homePosition))
        {
            return true;
        }

        return _initialPositions.TryGetValue(character, out homePosition);
    }

    public bool TryGetMainStoneHomePosition(Character character, out Vector3 homePosition)
    {
        homePosition = default;
        if (character == null) return false;

        return TryGetMainStonePositionForTeam(character.Team, out homePosition);
    }

    public bool TryGetEnemyHomePosition(Character character, out Vector3 enemyHomePosition)
    {
        enemyHomePosition = default;
        if (character == null) return false;

        CombatTeam enemyTeam = character.Team == CombatTeam.Ally
            ? CombatTeam.Enemy
            : CombatTeam.Ally;
        return TryGetMainStonePositionForTeam(enemyTeam, out enemyHomePosition);
    }

    public void RegisterInitialPosition(Character character)
    {
        if (character == null || _initialPositions.ContainsKey(character)) return;

        _initialPositions[character] = character.transform.position;
    }

    public void CaptureCurrentPositionsAsInitialPositions()
    {
        CaptureCurrentPositions(AllyCharacters);
        CaptureCurrentPositions(EnemyCharacters);
    }

    public void ResetCharactersForBattle()
    {
        using (CharacterResetMarker.Auto())
        {
            EnsureMagicStoneSubscription();
            _allyMarkedStoneAttacker = null;
            _enemyMarkedStoneAttacker = null;
            AssignBattleParticipantIds();
            ResetCharactersForBattle(AllyCharacters);
            ResetCharactersForBattle(EnemyCharacters);
            ResetAiDecisionSchedule(Time.time);
        }
    }

    public int TickAiDecisionsNow(float currentTime)
    {
        EnsureMagicStoneSubscription();
        EnsureAiDecisionSchedule();
        if (!_aiDecisionSchedule.TryConsume(currentTime, out int skippedDecisionCount))
        {
            LastSkippedAiDecisionCount = 0;
            return 0;
        }

        LastSkippedAiDecisionCount = skippedDecisionCount;
        TotalSkippedAiDecisionCount += skippedDecisionCount;
        using (CollectAiBrainsMarker.Auto()) CollectOrderedAiBrains();
        using (ScanAiVisionMarker.Auto()) ScanAiVision();
        using (ShareAiVisionMarker.Auto()) ShareAiVision();
        _allyAiReservations.Clear();
        _enemyAiReservations.Clear();

        int preparedCount = 0;
        using (PrepareAiDecisionsMarker.Auto())
        {
            for (int i = 0; i < _orderedAiBrains.Count; i++)
            {
                CombatAiBrain brain = _orderedAiBrains[i];
                Character owner = brain.GetComponent<Character>();
                CombatAiTeamReservations reservations = owner.Team == CombatTeam.Ally
                    ? _allyAiReservations
                    : _enemyAiReservations;
                if (brain.PrepareScheduledDecision(reservations, true))
                {
                    preparedCount++;
                    if (brain.TryGetPreparedPlan(out CombatAiPlan plan))
                    {
                        reservations.Reserve(owner, plan);
                    }
                }
            }
        }

        using (ExecuteAiDecisionsMarker.Auto())
        {
            for (int i = 0; i < _orderedAiBrains.Count; i++)
            {
                _orderedAiBrains[i].ExecutePreparedDecision();
            }
        }

        return preparedCount;
    }

    public bool TryRelocateCharactersNearMainStones()
    {
        using var _ = InitialPlacementMarker.Auto();
        CombatMapSystem mapSystem = ResolveMapSystem();
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null) return false;

        var allyDestinations = new List<Vector3>(AllyCharacters.Count);
        var enemyDestinations = new List<Vector3>(EnemyCharacters.Count);
        if (!TryBuildTeamSpawnPositions(
                AllyCharacters,
                CombatTeam.Ally,
                map,
                mapSystem,
                allyDestinations) ||
            !TryBuildTeamSpawnPositions(
                EnemyCharacters,
                CombatTeam.Enemy,
                map,
                mapSystem,
                enemyDestinations))
        {
            return false;
        }

        PlaceTeam(AllyCharacters, allyDestinations);
        PlaceTeam(EnemyCharacters, enemyDestinations);
        if (_generatedCharactersRoot != null)
        {
            _generatedCharactersRoot.SetActive(true);
        }
        return true;
    }

    private bool TryBuildTeamSpawnPositions(
        List<Character> characters,
        CombatTeam team,
        MapData map,
        CombatMapSystem mapSystem,
        List<Vector3> destinations)
    {
        if (characters == null || characters.Count == 0) return true;

        int requiredCount = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null) requiredCount++;
        }

        if (requiredCount == 0) return true;
        if (!mapSystem.TryGetMainStonePosition(team, out Vector3 stoneWorldPosition)) return false;
        Transform origin = mapSystem.MapOrigin;
        Vector3 stoneLocalPosition = origin != null
            ? origin.InverseTransformPoint(stoneWorldPosition)
            : stoneWorldPosition;
        IReadOnlyList<Vector3> candidates = InitialSpawnPositionBaker.Build(
            map,
            stoneLocalPosition,
            requireFlatTerrain: false);
        if (candidates.Count < requiredCount) return false;

        int candidateIndex = 0;
        float spacingSqr = InitialSpawnPositionBaker.CharacterSpacingDistance *
            InitialSpawnPositionBaker.CharacterSpacingDistance;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            bool placed = false;
            while (candidateIndex < candidates.Count)
            {
                Vector3 localDestination = candidates[candidateIndex++];
                Vector3 destination = mapSystem.MapLocalToSurfaceWorldPosition(localDestination);
                if (Application.isPlaying)
                {
                    NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
                    int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
                    if (!NavMesh.SamplePosition(
                            destination,
                            out NavMeshHit hit,
                            InitialSpawnPositionBaker.CharacterSpacingDistance,
                            areaMask))
                        continue;
                    destination = hit.position;
                }

                bool hasSpace = true;
                for (int p = 0; p < destinations.Count; p++)
                {
                    if (HorizontalDistanceSqr(destination, destinations[p]) < spacingSqr)
                    {
                        hasSpace = false;
                        break;
                    }
                }

                if (!hasSpace) continue;
                destinations.Add(destination);
                placed = true;
                break;
            }

            if (!placed) return false;
        }

        return destinations.Count == requiredCount;
    }

    private static void PlaceTeam(List<Character> characters, List<Vector3> destinations)
    {
        if (characters == null) return;

        int destinationIndex = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;
            PlaceCharacterAtSurface(character, destinations[destinationIndex++]);
        }
    }

    private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static bool IsNearCurrentNavMesh(Character character, Vector3 position)
    {
        if (!Application.isPlaying) return true;

        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        return NavMesh.SamplePosition(
            position,
            out _,
            InitialSpawnPositionBaker.CharacterSpacingDistance,
            areaMask);
    }

    private static void PlaceCharacter(Character character, Vector3 worldPosition)
    {
        character.GetComponent<CombatCharacterBody>()?.Stop();
        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            character.transform.position = worldPosition;
            return;
        }

        bool wasEnabled = agent.enabled;
        agent.enabled = false;
        character.transform.position = worldPosition;
        if (!wasEnabled) return;

        agent.enabled = true;
        if (agent.isOnNavMesh)
        {
            agent.Warp(worldPosition);
            agent.nextPosition = worldPosition;
        }
    }

    private static void PlaceCharacterAtSurface(Character character, Vector3 surfacePosition)
    {
        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
        float baseOffset = agent != null ? agent.baseOffset : 0f;
        PlaceCharacter(character, surfacePosition + Vector3.up * baseOffset);
    }

    private void CaptureCurrentPositions(List<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            _initialPositions[character] = character.transform.position;
        }
    }

    private void ResetCharactersForBattle(List<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            CleanupTransientSkillArtifacts(character);

            CombatCharacterBody body = character.GetComponent<CombatCharacterBody>();
            body?.Stop();

            character.Health?.RestoreFull();
            character.StatusEffects?.ClearAll();
            character.SkillCooldowns?.ClearAll();

            if (_initialPositions.TryGetValue(character, out Vector3 initialPosition) &&
                IsNearCurrentNavMesh(character, initialPosition))
            {
                PlaceCharacter(character, initialPosition);
            }
            else
            {
                _initialPositions[character] = character.transform.position;
            }

            character.InitializeOnBattleStart();
        }
    }

    private static void CleanupTransientSkillArtifacts(Character character)
    {
        character.GetComponent<BibleGotsumeEffect>()?.CancelImmediate();
        character.GetComponent<ShieldShoulderGuardEffect>()?.CancelImmediate();
        character.GetComponent<BibleCarryRushEffect>()?.CancelImmediate();
        character.SkillCaster.ClearCast();
    }

    private void Awake()
    {
        EnsureMagicStoneSubscription();
        if (_generateCandidatesAtRuntime)
        {
            if (_characterPrefab == null)
            {
                Debug.LogError($"[{nameof(CombatCharacterSystem)}] Character prefab is not configured.", this);
                enabled = false;
                return;
            }

            CombatMapSystem mapSystem = ResolveMapSystem();
            if (mapSystem == null)
            {
                Debug.LogError($"[{nameof(CombatCharacterSystem)}] CombatMapSystem is missing.", this);
                enabled = false;
                return;
            }

            if (mapSystem.CurrentMap != null)
            {
                GenerateCandidates();
            }
            else
            {
                mapSystem.CurrentMapChanged += OnRuntimeMapReady;
            }
        }

        AssignTeamsFromLists();
        ResetAiDecisionSchedule(Time.time);
    }

    private void OnDestroy()
    {
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.Damaged -= OnMagicStoneDamaged;
        }

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem != null) mapSystem.CurrentMapChanged -= OnRuntimeMapReady;
    }

    private void EnsureMagicStoneSubscription()
    {
        CombatMagicStoneSystem resolved = CombatMagicStoneSystemResolver.Resolve();
        if (resolved == _magicStoneSystem) return;

        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.Damaged -= OnMagicStoneDamaged;
        }

        _magicStoneSystem = resolved;
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.Damaged += OnMagicStoneDamaged;
        }
    }

    private void OnMagicStoneDamaged(int featureIndex, int damage, Character attacker)
    {
        if (damage <= 0 || attacker == null || _magicStoneSystem == null ||
            !_magicStoneSystem.TryGetState(featureIndex, out MagicStoneRuntimeState state)) return;

        if (state.Type == FeatureType.OwnMainStone && attacker.Team == CombatTeam.Enemy)
        {
            _allyMarkedStoneAttacker = attacker;
        }
        else if (state.Type == FeatureType.EnemyMainStone && attacker.Team == CombatTeam.Ally)
        {
            _enemyMarkedStoneAttacker = attacker;
        }
    }

    private void OnRuntimeMapReady()
    {
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null || mapSystem.CurrentMap == null) return;
        mapSystem.CurrentMapChanged -= OnRuntimeMapReady;
        GenerateCandidates();
        AssignTeamsFromLists();
        ResetAiDecisionSchedule(Time.time);
        CandidatesReady?.Invoke();
    }

    private void Update()
    {
        if (!CombatBattleFlow.AllowsCombatActions) return;

        TickAiDecisionsNow(Time.time);
    }

    private void GenerateCandidates()
    {
        AllyCharacters.Clear();
        EnemyCharacters.Clear();

        if (_generatedCharactersRoot != null)
        {
            Destroy(_generatedCharactersRoot);
        }

        var root = new GameObject(GeneratedCharactersRootName);
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        _generatedCharactersRoot = root;
        GenerateTeamCandidates(root.transform, CombatTeam.Ally, AllyCharacters);
        GenerateTeamCandidates(root.transform, CombatTeam.Enemy, EnemyCharacters);
    }

    private void GenerateTeamCandidates(
        Transform parent,
        CombatTeam team,
        List<Character> destination)
    {
        string[] defaultNames = team == CombatTeam.Ally
            ? DefaultAllyCandidateNames
            : DefaultEnemyCandidateNames;
        for (int i = 0; i < CandidateCountPerTeam; i++)
        {
            Character character = Instantiate(_characterPrefab, parent);
            character.name = defaultNames[i];
            character.gameObject.SetActive(true);
            character.SetTeam(team);
            destination.Add(character);
        }
    }

    private void AssignTeam(List<Character> characters, CombatTeam team)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;
            character.SetTeam(team);
            RegisterInitialPosition(character);
        }
    }

    private void AssignBattleParticipantIds()
    {
        AssignBattleParticipantIds(AllyCharacters, 1);
        AssignBattleParticipantIds(EnemyCharacters, -1);
    }

    private void ResetAiDecisionSchedule(float startTime)
    {
        _aiDecisionSchedule = new CombatAiDecisionSchedule(_aiDecisionIntervalSeconds);
        _aiDecisionSchedule.Reset(startTime);
        LastSkippedAiDecisionCount = 0;
        TotalSkippedAiDecisionCount = 0;
    }

    private void EnsureAiDecisionSchedule()
    {
        if (_aiDecisionSchedule == null)
        {
            ResetAiDecisionSchedule(Time.time);
        }
    }

    private void CollectOrderedAiBrains()
    {
        _orderedAiBrains.Clear();
        AddAiBrains(AllyCharacters);
        AddAiBrains(EnemyCharacters);
        _orderedAiBrains.Sort(CompareAiBrains);
    }

    private void AddAiBrains(List<Character> characters)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || !character.gameObject.activeInHierarchy) continue;

            CombatAiBrain brain = character.GetComponent<CombatAiBrain>();
            if (brain != null && brain.isActiveAndEnabled)
            {
                _orderedAiBrains.Add(brain);
            }
        }
    }

    private void ScanAiVision()
    {
        for (int i = 0; i < _orderedAiBrains.Count; i++)
        {
            Character owner = _orderedAiBrains[i].GetComponent<Character>();
            owner.Vision?.ScanVision();
        }
    }

    private void ShareAiVision()
    {
        for (int i = 0; i < _orderedAiBrains.Count; i++)
        {
            Character owner = _orderedAiBrains[i].GetComponent<Character>();
            owner.Vision?.PrepareVisionShare();
        }

        for (int i = 0; i < _orderedAiBrains.Count; i++)
        {
            Character owner = _orderedAiBrains[i].GetComponent<Character>();
            owner.Vision?.ShareVision();
        }
    }

    private static int CompareAiBrains(CombatAiBrain left, CombatAiBrain right)
    {
        Character leftCharacter = left.GetComponent<Character>();
        Character rightCharacter = right.GetComponent<Character>();
        int teamComparison = leftCharacter.Team.CompareTo(rightCharacter.Team);
        if (teamComparison != 0) return teamComparison;

        return Mathf.Abs(leftCharacter.BattleParticipantId)
            .CompareTo(Mathf.Abs(rightCharacter.BattleParticipantId));
    }

    private static void AssignBattleParticipantIds(List<Character> characters, int teamSign)
    {
        var ordered = new List<Character>();
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null) ordered.Add(characters[i]);
        }

        ordered.Sort((left, right) => string.CompareOrdinal(
            BuildHierarchyKey(left.transform),
            BuildHierarchyKey(right.transform)));
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].SetBattleParticipantId(teamSign * (i + 1));
        }
    }

    private static string BuildHierarchyKey(Transform current)
    {
        if (current == null) return string.Empty;

        string key = current.GetSiblingIndex().ToString("D4");
        while (current.parent != null)
        {
            current = current.parent;
            key = current.GetSiblingIndex().ToString("D4") + "/" + key;
        }

        return current.gameObject.scene.path + ":" + key;
    }

    private void ReplaceParticipants(
        List<Character> destination,
        IReadOnlyList<Character> source,
        CombatTeam team)
    {
        destination.Clear();
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character == null || destination.Contains(character)) continue;

            character.gameObject.SetActive(true);
            character.SetTeam(team);
            destination.Add(character);
            RegisterInitialPosition(character);
        }
    }

    private bool TryGetMainStonePositionForTeam(CombatTeam team, out Vector3 position)
    {
        position = default;
        CombatMapSystem mapSystem = ResolveMapSystem();
        return mapSystem != null && mapSystem.TryGetMainStonePosition(team, out position);
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

    private static List<Character> ApplySetups(IReadOnlyList<CombatParticipantSetup> setups)
    {
        var characters = new List<Character>();
        if (setups == null) return characters;

        for (int i = 0; i < setups.Count; i++)
        {
            CombatParticipantSetup setup = setups[i];
            if (setup?.Character == null || characters.Contains(setup.Character)) continue;

            setup.Character.ConfigureForBattle(
                setup.Weapon,
                setup.Personality,
                setup.MovementSpeedMultiplier,
                setup.TagalongTarget,
                setup.StatAdjustments);
            characters.Add(setup.Character);
        }

        return characters;
    }
}
