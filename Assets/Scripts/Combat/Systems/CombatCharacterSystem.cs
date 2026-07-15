using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using WarSimulation.Combat.Map;

public class CombatCharacterSystem : MonoBehaviour
{
    private const float FlatCellMaxSlopeDeg = 8f;
    private const float TeamQuarterNavMeshSampleRadius = 2f;
    private const float CharacterSpacingDistance = 1.5f;
    private const float InitialFeatureClearanceDistance = 3f;

    public List<Character> AllyCharacters = new List<Character>();
    public List<Character> EnemyCharacters = new List<Character>();

    [SerializeField] private CombatMapSystem _mapSystem;

    private readonly Dictionary<Character, Vector3> _initialPositions = new Dictionary<Character, Vector3>();

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
        AssignBattleParticipantIds();
        ResetCharactersForBattle(AllyCharacters);
        ResetCharactersForBattle(EnemyCharacters);
    }

    public void SnapAllCharactersToNavMesh(float searchRadius = 10f)
    {
        if (TryRelocateCharactersNearMainStones())
        {
            return;
        }

        SnapListToNavMesh(AllyCharacters, searchRadius);
        SnapListToNavMesh(EnemyCharacters, searchRadius);
    }

    public bool TryRelocateCharactersNearMainStones()
    {
        CombatMapSystem mapSystem = ResolveMapSystem();
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null) return false;

        bool movedAllies = TryRelocateTeamNearMainStone(AllyCharacters, CombatTeam.Ally, mapSystem, map);
        bool movedEnemies = TryRelocateTeamNearMainStone(EnemyCharacters, CombatTeam.Enemy, mapSystem, map);
        return movedAllies && movedEnemies;
    }

    public bool TryRelocateCharactersToTeamQuarterFlats()
    {
        return TryRelocateCharactersNearMainStones();
    }

    private static void SnapListToNavMesh(List<Character> characters, float radius)
    {
        foreach (Character character in characters)
        {
            if (character == null) continue;
            if (NavMesh.SamplePosition(character.transform.position, out NavMeshHit hit, radius, NavMesh.AllAreas))
                character.transform.position = hit.position;
        }
    }

    private bool TryRelocateTeamNearMainStone(
        List<Character> characters,
        CombatTeam team,
        CombatMapSystem mapSystem,
        MapData map)
    {
        if (characters == null || characters.Count == 0) return true;
        if (!TryGetMainStonePositionForTeam(team, out Vector3 anchorPosition))
        {
            return false;
        }

        if (!TryCollectFlatPositionsNearAnchor(mapSystem, map, anchorPosition, out List<Vector3> candidates) ||
            candidates.Count == 0)
        {
            return false;
        }

        var destinations = new List<Vector3>(characters.Count);
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            if (!TrySelectCandidateWithSpacing(candidates, destinations, out Vector3 destination))
            {
                return false;
            }

            destinations.Add(destination);
        }

        int destinationIndex = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            PlaceCharacter(character, destinations[destinationIndex++]);
        }

        return destinations.Count > 0;
    }

    private static bool TryCollectFlatPositionsNearAnchor(
        CombatMapSystem mapSystem,
        MapData map,
        Vector3 anchorPosition,
        out List<Vector3> candidates)
    {
        candidates = new List<Vector3>();

        GroundStateGrid ground = map.GroundStates;
        HeightMap height = map.Height;

        for (int z = 0; z < ground.Height; z++)
        {
            for (int x = 0; x < ground.Width; x++)
            {
                if (ground.GetCell(x, z) == GroundState.Water) continue;
                if (height.IsCliffFaceCell(x, z)) continue;

                Vector3 mapLocalPosition = GetCellCenterLocalPosition(ground, x, z);
                if (height.SampleSlopeDeg(mapLocalPosition) > FlatCellMaxSlopeDeg) continue;

                Vector3 worldPosition = mapSystem.MapLocalToSurfaceWorldPosition(mapLocalPosition);
                if (!IsClearOfSolidFeatures(mapSystem, map, worldPosition)) continue;

                if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, TeamQuarterNavMeshSampleRadius, NavMesh.AllAreas))
                {
                    if (IsClearOfSolidFeatures(mapSystem, map, hit.position))
                    {
                        candidates.Add(hit.position);
                    }
                    continue;
                }

                candidates.Add(worldPosition);
            }
        }

        candidates.Sort((a, b) =>
        {
            float distanceA = HorizontalDistanceSqr(anchorPosition, a);
            float distanceB = HorizontalDistanceSqr(anchorPosition, b);
            return distanceA.CompareTo(distanceB);
        });

        return candidates.Count > 0;
    }

    private static bool TrySelectCandidateWithSpacing(
        List<Vector3> candidates,
        List<Vector3> placedPositions,
        out Vector3 selected)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 candidate = candidates[i];
            if (HasEnoughSpacing(candidate, placedPositions))
            {
                selected = candidate;
                return true;
            }
        }

        selected = default;
        return false;
    }

    private static bool IsClearOfSolidFeatures(
        CombatMapSystem mapSystem,
        MapData map,
        Vector3 worldPosition)
    {
        float clearanceSqr = InitialFeatureClearanceDistance * InitialFeatureClearanceDistance;
        Transform origin = mapSystem.MapOrigin;
        List<PlacedFeature> features = map.Features;
        for (int i = 0; i < features.Count; i++)
        {
            PlacedFeature feature = features[i];
            if (feature.Type == FeatureType.Bridge) continue;

            Vector3 featurePosition = origin != null
                ? origin.TransformPoint(feature.WorldPosition)
                : feature.WorldPosition;
            if (HorizontalDistanceSqr(worldPosition, featurePosition) < clearanceSqr)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasEnoughSpacing(Vector3 candidate, List<Vector3> placedPositions)
    {
        for (int i = 0; i < placedPositions.Count; i++)
        {
            if (HorizontalDistanceSqr(candidate, placedPositions[i]) <
                CharacterSpacingDistance * CharacterSpacingDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static Vector3 GetCellCenterLocalPosition(GroundStateGrid ground, int x, int z)
    {
        float cellSize = ground.CellSize;
        return new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);
    }

    private static void PlaceCharacter(Character character, Vector3 worldPosition)
    {
        CombatCharacterBody body = character.GetComponent<CombatCharacterBody>();
        body?.Stop();

        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(worldPosition);
            return;
        }

        character.transform.position = worldPosition;
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

            if (_initialPositions.TryGetValue(character, out Vector3 initialPosition))
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
        CollectCharactersFromScene();
        AssignTeamsFromLists();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CollectCharactersFromScene();
        AssignTeamsFromLists();
    }
#endif

    private void CollectCharactersFromScene()
    {
        AllyCharacters.Clear();
        EnemyCharacters.Clear();
        Character[] all = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (Character c in all)
        {
            if (c.Team == CombatTeam.Ally) AllyCharacters.Add(c);
            else EnemyCharacters.Add(c);
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
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null) return false;

        FeatureType targetType = team == CombatTeam.Ally
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;

        List<PlacedFeature> features = map.Features;
        for (int i = 0; i < features.Count; i++)
        {
            PlacedFeature feature = features[i];
            if (feature.Type != targetType) continue;

            Transform origin = mapSystem.MapOrigin;
            position = origin != null ? origin.TransformPoint(feature.WorldPosition) : feature.WorldPosition;
            return true;
        }

        return false;
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

            setup.Character.ConfigureForBattle(setup.Weapon, setup.Personality);
            characters.Add(setup.Character);
        }

        return characters;
    }
}
