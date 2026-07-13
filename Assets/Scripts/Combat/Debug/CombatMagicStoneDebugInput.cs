using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CombatMagicStoneDebugInput : CombatDebugBehaviour
{
    public override string InspectorDescription => "戦闘中に魔石を左クリックして、指定量のダメージを与えます。";

    [SerializeField, Min(1)] private int _damagePerClick = 50;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _raycastMask = Physics.DefaultRaycastLayers;

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }
    }

    private void Update()
    {
        if (!CombatBattleFlow.IsRunning) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Camera camera = _camera != null ? _camera : Camera.main;
        if (camera == null) return;

        Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _raycastMask)) return;

        MagicStone stone = hit.collider.GetComponentInParent<MagicStone>();
        if (stone == null) return;

        CombatMagicStoneSystem system = ResolveSystem();
        system?.TakeDamage(stone.FeatureIndex, _damagePerClick);
    }

    private static CombatMagicStoneSystem ResolveSystem()
    {
        return CombatMagicStoneSystemResolver.Resolve();
    }
}
