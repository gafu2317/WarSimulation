using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マップ中心を注視点として旋回するオービットカメラ。
/// - 右クリックドラッグ : 注視点周りに回転（Yaw / Pitch）
/// - スクロールホイール : ズーム（注視点までの距離変更）
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] private float _orbitSpeed = 0.3f;
    [SerializeField, Range(-89f, 0f)] private float _pitchMin = -80f;
    [SerializeField, Range(1f, 89f)] private float _pitchMax = 80f;

    [Header("Zoom")]
    [SerializeField] private float _zoomSpeed = 5f;
    [SerializeField, Min(1f)] private float _zoomMin = 5f;
    [SerializeField] private float _zoomMax = 120f;

    private Vector3 _pivot;
    private float _yaw;
    private float _pitch;
    private float _distance;
    private bool _ready;

    // CombatMapSystem.Start() の完了を待つため 1 フレーム遅らせて初期化する
    private IEnumerator Start()
    {
        yield return null;

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x > 180f ? angles.x - 360f : angles.x;

        _pivot = ResolveMapCenter();
        _distance = Mathf.Clamp(Vector3.Distance(transform.position, _pivot), _zoomMin, _zoomMax);
        _ready = true;
    }

    private void Update()
    {
        if (!_ready) return;
        if (Mouse.current == null) return;

        HandleOrbit();
        HandleZoom();
        ApplyTransform();
    }

    private void HandleOrbit()
    {
        if (!Mouse.current.rightButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        _yaw += delta.x * _orbitSpeed;
        _pitch -= delta.y * _orbitSpeed;
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
    }

    private void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        _distance -= Mathf.Sign(scroll) * _zoomSpeed;
        _distance = Mathf.Clamp(_distance, _zoomMin, _zoomMax);
    }

    private void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.position = _pivot + rotation * new Vector3(0f, 0f, -_distance);
        transform.rotation = rotation;
    }

    private Vector3 ResolveMapCenter()
    {
        CombatMapSystem mapSystem = FindFirstObjectByType<CombatMapSystem>();
        if (mapSystem != null && mapSystem.CurrentMap != null)
        {
            var map = mapSystem.CurrentMap;
            float halfW = map.GroundStates.WorldSize.x * 0.5f;
            float halfH = map.GroundStates.WorldSize.y * 0.5f;
            return mapSystem.MapLocalToSurfaceWorldPosition(new Vector3(halfW, 0f, halfH));
        }

        return Vector3.zero;
    }
}
