using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CombatCharacterBody))]
[RequireComponent(typeof(CombatCharacterRouteVisualizer))]
public sealed class ClickMoveBrain : MonoBehaviour
{
    [SerializeField] private Camera _cameraTarget;
    [SerializeField] private LayerMask _hitLayers = ~0;
    [SerializeField, Min(0.1f)] private float _maxRayDistance = 1000f;
    [SerializeField] private bool _logFailure = true;

    private CombatCharacterBody _body;

    private void Awake()
    {
        _body = GetComponent<CombatCharacterBody>();
        if (_body == null)
        {
            _body = gameObject.AddComponent<CombatCharacterBody>();
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            if (_logFailure)
            {
                Debug.LogWarning($"[{nameof(ClickMoveBrain)}] Camera Target is not assigned and Camera.main was not found.");
            }
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _hitLayers))
        {
            return;
        }

        if (!_body.TrySetDestination(hit.point) && _logFailure)
        {
            Debug.Log($"[{nameof(ClickMoveBrain)}] Could not set destination: {FormatVector3(hit.point)}", this);
        }
    }

    private Camera ResolveCamera()
    {
        return _cameraTarget != null ? _cameraTarget : Camera.main;
    }

    private static string FormatVector3(Vector3 v)
    {
        return $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
