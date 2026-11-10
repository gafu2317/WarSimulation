using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map
{
    [DisallowMultipleComponent]
    public sealed class CombatRuntimeRock : MonoBehaviour
    {
        public const float Height = 3f;
        public const float Thickness = 1.5f;
        private const float HalfSpan = 2.25f;
        private readonly List<GameObject> _hiddenTrees = new();
        private readonly List<BoxCollider> _parts = new();
        private CombatNavMeshBuilder _builder;
        private CombatCharacterSystem _characters;
        private GameObject _rock;
        private Material _material;
        private Coroutine _update;
        private Action<bool, string> _completed;

        public CombatRockPlacement? Placement { get; private set; }
        public bool IsUpdating { get; private set; }
        public GameObject Rock => _rock;

        public bool TryPlace(CombatMapSystem mapSystem, CombatRockPlacement placement,
            CombatCharacterSystem characters, Action<bool, string> completed, out string error)
        {
            error = null;
            if (IsUpdating) { error = "岩の配置を更新中です"; return false; }
            _builder = GetComponent<CombatNavMeshBuilder>();
            TerrainCollider terrain = GetComponentInChildren<TerrainCollider>();
            if (mapSystem == null || mapSystem.CurrentMap == null || terrain == null ||
                _builder == null || _builder.Surface == null || _builder.Surface.navMeshData == null)
            {
                error = "地形またはNavMeshが準備できていません";
                return false;
            }
            _characters = characters;
            Vector3 world = new(placement.Position.x, transform.position.y, placement.Position.y);
            Vector3 local = transform.InverseTransformPoint(world);
            var groundRay = new Ray(new Vector3(world.x, terrain.bounds.max.y + 1f, world.z), Vector3.down);
            if (!terrain.Raycast(groundRay, out RaycastHit groundHit, terrain.bounds.size.y + 2f))
            {
                error = "配置位置に地面がありません";
                return false;
            }
            local.y = transform.InverseTransformPoint(groundHit.point).y;
            var candidate = new GameObject("RuntimeRock");
            candidate.transform.SetParent(transform, false);
            candidate.transform.localPosition = local;
            candidate.transform.localRotation = Quaternion.Euler(0, placement.QuarterTurns * 90, 0);
            int layer = LayerMask.NameToLayer("VisionObstacle");
            if (layer >= 0) candidate.layer = layer;
            var modifier = candidate.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            var parts = new List<BoxCollider>();
            BuildShape(candidate.transform, placement.Shape, parts);
            Physics.SyncTransforms();
            if (!FitsMap(parts, mapSystem.CurrentMap.Height.WorldSize) ||
                !RockGrounding.TryGround(candidate.transform, candidate.transform, transform, terrain,
                    Mathf.Min(0.25f, mapSystem.CurrentMap.Height.CellSize * 0.5f), 0.02f, out error))
            {
                candidate.SetActive(false);
                DestroyGenerated(candidate);
                error ??= "岩全体がマップ内に収まる位置を選んでください";
                return false;
            }

            RemoveGeometry();
            _rock = candidate;
            candidate.AddComponent<CombatRuntimeRockLifetime>().Owner = this;
            _parts.AddRange(parts);
            Placement = placement;
            Physics.SyncTransforms();
            HideOverlappingTrees();
            Physics.SyncTransforms();
            IsUpdating = true;
            AsyncOperation operation = _builder.BeginRuntimeUpdate(mapSystem.CurrentMap);
            if (operation == null)
            {
                Clear();
                error = "NavMeshを更新できませんでした";
                return false;
            }
            _completed = completed;
            _update = StartCoroutine(CompleteUpdate(operation));
            return true;
        }

        private IEnumerator CompleteUpdate(AsyncOperation operation)
        {
            while (!operation.isDone) yield return null;
            _builder.CompleteRuntimeUpdate();
            RefreshCharacters();
            CombatAssaultRouteCache.Invalidate();
            CombatAiStaticMapCache.Invalidate();
            IsUpdating = false;
            _update = null;
            Action<bool, string> completed = _completed;
            _completed = null;
            completed?.Invoke(true, null);
        }

        public bool Contains(Vector3 world)
        {
            foreach (BoxCollider part in _parts)
            {
                if (part == null) continue;
                Vector3 local = part.transform.InverseTransformPoint(world) - part.center;
                Vector3 half = part.size * 0.5f;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z) return true;
            }
            return false;
        }

        public void Clear()
        {
            if (!Placement.HasValue && !IsUpdating && _hiddenTrees.Count == 0) return;
            if (_update != null) StopCoroutine(_update);
            _update = null;
            RemoveGeometry();
            if (_builder != null) _builder.RestoreOriginalData();
            Physics.SyncTransforms();
            RefreshCharacters();
            CombatAssaultRouteCache.Invalidate();
            CombatAiStaticMapCache.Invalidate();
            IsUpdating = false;
            Action<bool, string> completed = _completed;
            _completed = null;
            completed?.Invoke(false, "配置を中止しました");
        }

        private void OnDestroy()
        {
            Clear();
            DestroyGenerated(_material);
        }

        private void RemoveGeometry()
        {
            GameObject oldRock = _rock;
            _rock = null;
            _parts.Clear();
            Placement = null;
            if (oldRock != null)
            {
                oldRock.SetActive(false);
                DestroyGenerated(oldRock);
            }
            foreach (GameObject tree in _hiddenTrees)
                if (tree != null) tree.SetActive(true);
            _hiddenTrees.Clear();
        }

        private void HideOverlappingTrees()
        {
            Transform features = transform.Find("GeneratedFeatures");
            if (features == null) return;
            foreach (Transform tree in features)
            {
                if (!tree.name.StartsWith("Tree_", StringComparison.Ordinal) || !tree.gameObject.activeSelf) continue;
                bool overlaps = false;
                foreach (Collider collider in tree.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger) continue;
                    foreach (BoxCollider part in _parts)
                    {
                        if (!part.bounds.Intersects(collider.bounds)) continue;
                        if (Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                                collider, collider.transform.position, collider.transform.rotation, out _, out _))
                        {
                            overlaps = true;
                            break;
                        }
                    }
                    if (overlaps) break;
                }
                if (!overlaps) continue;
                _hiddenTrees.Add(tree.gameObject);
                tree.gameObject.SetActive(false);
            }
        }

        private bool FitsMap(List<BoxCollider> parts, Vector2 size)
        {
            foreach (BoxCollider part in parts)
            {
                Vector3 half = part.size * 0.5f;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = transform.InverseTransformPoint(part.transform.TransformPoint(
                        part.center + new Vector3(x * half.x, 0, z * half.z)));
                    if (point.x < 0 || point.z < 0 || point.x > size.x || point.z > size.y) return false;
                }
            }
            return true;
        }

        private void BuildShape(Transform root, CombatRockShape shape, List<BoxCollider> parts)
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _material = new Material(shader) { name = "RuntimeRock", color = new Color(0.4f, 0.42f, 0.45f) };
            }
            switch (shape)
            {
                case CombatRockShape.Straight:
                    AddSegment(root, Vector2.down * HalfSpan, Vector2.up * HalfSpan, parts);
                    break;
                case CombatRockShape.Bent:
                    AddSegment(root, new Vector2(0.6f, 1) * HalfSpan, Vector2.left * HalfSpan, parts);
                    AddSegment(root, Vector2.left * HalfSpan, new Vector2(0.6f, -1) * HalfSpan, parts);
                    break;
                case CombatRockShape.U:
                    AddSegment(root, new Vector2(-1, 1) * HalfSpan, new Vector2(-1, -1) * HalfSpan, parts);
                    AddSegment(root, new Vector2(-1, -1) * HalfSpan, new Vector2(1, -1) * HalfSpan, parts);
                    AddSegment(root, new Vector2(1, -1) * HalfSpan, new Vector2(1, 1) * HalfSpan, parts);
                    break;
                default:
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i * Mathf.PI / 16;
                        float b = (i + 1) * Mathf.PI / 16;
                        AddSegment(root, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * HalfSpan,
                            new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * HalfSpan, parts, extendEnds: false);
                    }
                    break;
            }
        }

        private void AddSegment(Transform root, Vector2 a, Vector2 b, List<BoxCollider> parts, bool extendEnds = true)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "RockSegment";
            part.layer = root.gameObject.layer;
            part.transform.SetParent(root, false);
            Vector2 middle = (a + b) * 0.5f;
            Vector2 delta = b - a;
            part.transform.localPosition = new Vector3(middle.x, Height * 0.5f, middle.y);
            part.transform.localRotation = Quaternion.Euler(0, Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 0);
            part.transform.localScale = new Vector3(Thickness, Height, delta.magnitude + (extendEnds ? Thickness : 0.15f));
            part.GetComponent<MeshRenderer>().sharedMaterial = _material;
            parts.Add(part.GetComponent<BoxCollider>());
        }

        private void RefreshCharacters()
        {
            if (_characters == null) return;
            RefreshCharacters(_characters.AllyCharacters);
            RefreshCharacters(_characters.EnemyCharacters);
        }

        private void RefreshCharacters(IReadOnlyList<Character> characters)
        {
            foreach (Character character in characters)
            {
                if (character == null || !character.gameObject.activeInHierarchy) continue;
                NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled) continue;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (NavMesh.SamplePosition(character.transform.position, out NavMeshHit hit, 8f, filter))
                {
                    agent.enabled = false;
                    character.transform.position = hit.position;
                    agent.enabled = true;
                }
                character.GetComponent<CombatCharacterBody>()?.InvalidateNavigation();
                character.GetComponent<CombatAiBrain>()?.InvalidateNavigation();
                character.Health?.InvalidateRetreatNavigation();
            }
        }

        private static void DestroyGenerated(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
