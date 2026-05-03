using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CombatTerrainInfoClickDebugger : MonoBehaviour
{
    [SerializeField] private Camera _cameraTarget;
    [SerializeField] private LayerMask _hitLayers = ~0;
    [SerializeField, Min(0.1f)] private float _maxRayDistance = 1000f;
    [SerializeField] private bool _logOnlyWhenMapReady = true;
    [SerializeField] private bool _logRaycastMiss = false;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        CombatMapSystem mapSystem = GetMapSystem();
        if (mapSystem == null)
        {
            Debug.LogWarning($"[{nameof(CombatTerrainInfoClickDebugger)}] CombatMapSystem is not available.");
            return;
        }

        if (_logOnlyWhenMapReady && mapSystem.CurrentMap == null)
        {
            Debug.LogWarning($"[{nameof(CombatTerrainInfoClickDebugger)}] CurrentMap is not set. Generate a map and make sure the generated MapData is assigned to CombatMapSystem before clicking.");
            return;
        }

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning($"[{nameof(CombatTerrainInfoClickDebugger)}] Camera Target is not assigned and Camera.main was not found.");
            return;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _hitLayers))
        {
            if (_logRaycastMiss)
            {
                Debug.Log($"[{nameof(CombatTerrainInfoClickDebugger)}] Raycast missed. Screen={screenPosition}");
            }
            return;
        }

        if (!mapSystem.TryGetTerrainInfo(hit.point, out TerrainInfo info))
        {
            Debug.LogWarning($"[{nameof(CombatTerrainInfoClickDebugger)}] TerrainInfo query failed. Hit={FormatVector3(hit.point)}");
            return;
        }

        Debug.Log(BuildLog(hit, info, mapSystem.CurrentMap));
    }

    private Camera ResolveCamera()
    {
        return _cameraTarget != null ? _cameraTarget : Camera.main;
    }

    private static CombatMapSystem GetMapSystem()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.MapSystem != null) return context.MapSystem;
        return FindAnyObjectByType<CombatMapSystem>();
    }

    private static string BuildLog(RaycastHit hit, TerrainInfo info, WarSimulation.Combat.Map.MapData map)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("[TerrainInfo Click]");
        sb.AppendLine($"Map Seed        : {(map != null ? map.Seed.ToString() : "(none)")}");
        sb.AppendLine($"Hit Object      : {hit.collider.name}");
        sb.AppendLine($"Hit World       : {FormatVector3(hit.point)}");
        sb.AppendLine($"Surface World   : {FormatVector3(info.WorldPosition)}");
        sb.AppendLine($"Map Local       : {FormatVector3(info.MapLocalPosition)}");
        sb.AppendLine($"Cell            : ({info.Cell.x}, {info.Cell.y})");
        sb.AppendLine($"Height          : {info.Height:F3}");
        sb.AppendLine($"GroundState     : {info.GroundState}");
        sb.AppendLine($"BiomeId         : {FormatBiomeId(info.BiomeId)}");
        sb.AppendLine($"IsWater         : {info.IsWater}");
        sb.AppendLine($"IsFrozenLake    : {info.IsFrozenLake}");
        sb.AppendLine($"IsForest        : {info.IsForest}");
        sb.AppendLine($"IsCliffFace     : {info.IsCliffFace}");
        sb.AppendLine($"SlopeDeg        : {info.SlopeDeg:F2}");
        sb.Append($"IsInBounds      : {info.IsInBounds}");
        return sb.ToString();
    }

    private static string FormatBiomeId(string biomeId)
    {
        return string.IsNullOrEmpty(biomeId) ? "(none)" : biomeId;
    }

    private static string FormatVector3(Vector3 v)
    {
        return $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
