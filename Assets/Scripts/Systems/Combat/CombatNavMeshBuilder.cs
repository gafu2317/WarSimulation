using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatNavMeshBuilder : MonoBehaviour
{
    [SerializeField] private NavMeshSurface _surface;
    [SerializeField] private LayerMask _layerMask = ~0;
    [SerializeField] private NavMeshCollectGeometry _geometry = NavMeshCollectGeometry.RenderMeshes;
    [SerializeField] private bool _buildHeightMesh = false;

    public NavMeshSurface Surface => _surface;

    public bool Build(MapData map)
    {
        if (map == null)
        {
            Debug.LogWarning($"[{nameof(CombatNavMeshBuilder)}] Build called with null MapData.");
            return false;
        }

        EnsureSurface();
        _surface.collectObjects = CollectObjects.Children;
        _surface.layerMask = _layerMask;
        _surface.useGeometry = _geometry;
        _surface.buildHeightMesh = _buildHeightMesh;
        _surface.ignoreNavMeshAgent = true;
        _surface.ignoreNavMeshObstacle = true;
        _surface.BuildNavMesh();

        return _surface.navMeshData != null;
    }

    public void Clear()
    {
        if (_surface == null) return;
        _surface.RemoveData();
        _surface.navMeshData = null;
    }

    private void EnsureSurface()
    {
        if (_surface != null) return;

        _surface = GetComponent<NavMeshSurface>();
        if (_surface == null)
        {
            _surface = gameObject.AddComponent<NavMeshSurface>();
        }
    }
}
