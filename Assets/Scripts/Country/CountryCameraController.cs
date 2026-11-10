using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CountryCameraController : MonoBehaviour
{
    public bool InputEnabled { get; set; } = true;
    [SerializeField, Min(0f)] private float _moveSpeed = 0.35f;
    [SerializeField, Min(1f)] private float _fastMoveMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float _zoomSensitivity = 0.15f;
    [SerializeField, Min(1f)] private float _minimumDistance = 35f;
    [SerializeField, Min(1f)] private float _maximumDistance = 600f;
    [SerializeField] private float _groundHeight;

    private Camera _camera;
    private Vector3 _focus;
    private Quaternion _rotation;
    private float _distance;
    private Vector2 _previousPointer;
    private bool _dragging;
    private bool _dragButtonHeld;
    private int _firstTouchId;
    private int _secondTouchId;
    private Vector2 _previousTouchCenter;
    private float _previousTouchSpan;
    private bool _touchBlocked;
    private bool _ready;

    private Plane Ground => new(Vector3.up, new Vector3(0f, _groundHeight, 0f));

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        SyncStateFromTransform();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        ResetInput();
    }

    public void SyncStateFromTransform()
    {
        _camera = GetComponent<Camera>();
        ResetInput();
        _ready = Ground.Raycast(new Ray(transform.position, transform.forward), out float distance);
        if (!_ready) return;

        _focus = transform.position + transform.forward * distance;
        _rotation = transform.rotation;
        _distance = Mathf.Clamp(distance, _minimumDistance, _maximumDistance);
        ApplyPose();
    }

    private void LateUpdate()
    {
        if (!InputEnabled || !_ready || !Application.isFocused)
        {
            ResetInput();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && !IsEditingText())
        {
            Vector2 direction = new(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            float speed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? _fastMoveMultiplier : 1f;
            Move(direction, Time.unscaledDeltaTime * speed);
        }

        if (HandleTouch()) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool overUi = IsPointerOverUi(mouse.deviceId);
        Vector2 pointer = mouse.position.ReadValue();
        bool held = mouse.middleButton.isPressed || mouse.rightButton.isPressed;
        if (held && !_dragButtonHeld)
        {
            _dragging = !overUi;
            _previousPointer = pointer;
        }
        _dragButtonHeld = held;
        if (!held) _dragging = false;
        if (_dragging && !overUi) Pan(_previousPointer, pointer);
        _previousPointer = pointer;

        if (!overUi) Zoom(mouse.scroll.ReadValue().y);
    }

    private bool HandleTouch()
    {
        Touch first = default;
        Touch second = default;
        int count = 0;
        EventSystem events = EventSystem.current;
        foreach (Touch touch in Touch.activeTouches)
        {
            if (!touch.isInProgress) continue;
            if (count == 0) first = touch;
            else if (count == 1) second = touch;
            count++;
            _touchBlocked |= events != null && events.IsPointerOverGameObject(touch.touchId);
        }

        if (count == 0)
        {
            _firstTouchId = _secondTouchId = 0;
            _touchBlocked = false;
            return false;
        }

        _dragging = _dragButtonHeld = false;
        if (_touchBlocked || count > 2)
        {
            _firstTouchId = _secondTouchId = 0;
            return true;
        }

        int secondId = count == 2 ? second.touchId : 0;
        Vector2 center = count == 2
            ? (first.screenPosition + second.screenPosition) * 0.5f : first.screenPosition;
        float span = count == 2 ? Vector2.Distance(first.screenPosition, second.screenPosition) : 0f;
        if (_firstTouchId == first.touchId && _secondTouchId == secondId)
        {
            Pan(_previousTouchCenter, center);
            if (count == 2) Pinch(center, _previousTouchSpan, span);
        }
        _firstTouchId = first.touchId;
        _secondTouchId = secondId;
        _previousTouchCenter = center;
        _previousTouchSpan = span;
        return true;
    }

    private void ResetInput()
    {
        _dragging = _dragButtonHeld = false;
        _firstTouchId = _secondTouchId = 0;
        _touchBlocked = false;
    }

    public void Move(Vector2 direction, float seconds)
    {
        if (!_ready || direction == Vector2.zero) return;

        Vector3 forward = Vector3.ProjectOnPlane(_rotation * Vector3.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(_rotation * Vector3.right, Vector3.up).normalized;
        Vector2 input = Vector2.ClampMagnitude(direction, 1f);
        _focus += (right * input.x + forward * input.y) * (_distance * _moveSpeed * seconds);
        ApplyPose();
    }

    public void Pan(Vector2 from, Vector2 to)
    {
        if (!_ready || from == to) return;

        Ray previous = _camera.ScreenPointToRay(from);
        Ray current = _camera.ScreenPointToRay(to);
        Plane ground = Ground;
        if (!ground.Raycast(previous, out float previousDistance)
            || !ground.Raycast(current, out float currentDistance)) return;

        _focus += previous.GetPoint(previousDistance) - current.GetPoint(currentDistance);
        ApplyPose();
    }

    public void Zoom(float steps)
    {
        if (!_ready || steps == 0f) return;

        _distance = Mathf.Clamp(_distance * Mathf.Exp(-steps * _zoomSensitivity),
            _minimumDistance, _maximumDistance);
        ApplyPose();
    }

    public void Pinch(Vector2 center, float previousSpan, float currentSpan)
    {
        if (!_ready || previousSpan < 1f || currentSpan < 1f || previousSpan == currentSpan) return;

        Plane ground = Ground;
        Ray before = _camera.ScreenPointToRay(center);
        if (!ground.Raycast(before, out float beforeDistance)) return;
        Vector3 grabbed = before.GetPoint(beforeDistance);

        _distance = Mathf.Clamp(_distance * previousSpan / currentSpan, _minimumDistance, _maximumDistance);
        ApplyPose();
        Ray after = _camera.ScreenPointToRay(center);
        if (ground.Raycast(after, out float afterDistance))
        {
            _focus += grabbed - after.GetPoint(afterDistance);
            ApplyPose();
        }
    }

    private void ApplyPose()
    {
        transform.SetPositionAndRotation(_focus - _rotation * Vector3.forward * _distance, _rotation);
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        EventSystem events = EventSystem.current;
        return events != null && (events.IsPointerOverGameObject()
            || events.IsPointerOverGameObject(pointerId));
    }

    private static bool IsEditingText()
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && (selected.TryGetComponent(out TMP_InputField tmp) && tmp.isFocused
            || selected.TryGetComponent(out InputField input) && input.isFocused);
    }
}
