using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class EditorStyleCameraController : MonoBehaviour
{
    [Header("移動設定 (Movement)")]
    public float moveSpeed = 10f;             // 通常の移動速度
    public float fastMoveMultiplier = 3f;     // Shiftキーを押した時の速度倍率
    public float panSpeed = 0.5f;             // パン（中クリック）の移動速度
    public float scrollSpeed = 20f;           // スクロール時の移動速度

    [Header("回転設定 (Rotation)")]
    public float lookSpeed = 0.2f;            // マウス感度

    private float pitch = 0f;
    private float yaw = 0f;
    private GameObject _touchControlsRoot;
    private GameObject _touchEventSystemObject;
    private readonly List<RectTransform> _touchControlRegions = new();
    private readonly HashSet<int> _touchesStartedOnUi = new();
    private Vector2 _touchLookDelta;
    private Vector2 _touchPanDelta;
    private float _touchZoomDelta;
    private bool _touchLookActive;
    private bool _touchPanActive;
    private bool _hasPreviousTwoTouch;
    private Vector2 _previousTwoTouchCenter;
    private float _previousTwoTouchDistance;

    private void Start()
    {
        SyncStateFromTransform();
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        ResetTouchInput();
        if (_touchControlsRoot != null)
        {
            _touchControlsRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_touchControlsRoot != null)
        {
            Destroy(_touchControlsRoot);
        }

        if (_touchEventSystemObject != null)
        {
            Destroy(_touchEventSystemObject);
        }
    }

    public void SyncStateFromTransform()
    {
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        yaw = angles.y > 180f ? angles.y - 360f : angles.y;
    }

    private void Update()
    {
        UpdateTouchControls();
        ReadTouchInput();
        HandleRotationAndMovement();
        HandlePanning();
        HandleZoom();
    }

    /// <summary>
    /// 右クリックによる視点移動とWASD移動
    /// </summary>
    private void HandleRotationAndMovement()
    {
        Mouse mouse = Mouse.current;
        bool mouseLookActive = mouse != null && mouse.rightButton.isPressed;
        Keyboard keyboard = Keyboard.current;
        Vector3 moveDirection = Vector3.zero;
        bool keyboardMoveActive = false;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveDirection += Vector3.forward;
            if (keyboard.sKey.isPressed) moveDirection += Vector3.back;
            if (keyboard.aKey.isPressed) moveDirection += Vector3.left;
            if (keyboard.dKey.isPressed) moveDirection += Vector3.right;
            if (keyboard.eKey.isPressed) moveDirection += Vector3.up;
            if (keyboard.qKey.isPressed) moveDirection += Vector3.down;
            keyboardMoveActive = moveDirection != Vector3.zero;
        }

        bool touchStickActive = false;
        if (Touchscreen.current != null && Gamepad.current != null)
        {
            Vector2 stickInput = Gamepad.current.leftStick.ReadValue();
            touchStickActive = stickInput.sqrMagnitude > 0.0001f;
            moveDirection += new Vector3(stickInput.x, 0f, stickInput.y);
        }

        bool touchInputAvailable = Touchscreen.current != null;
        bool movementActive = mouseLookActive || _touchLookActive ||
            (touchInputAvailable && (keyboardMoveActive || touchStickActive));
        if (!movementActive) return;

        if (_touchLookActive || mouseLookActive)
        {
            Vector2 lookDelta = _touchLookActive ? _touchLookDelta : mouse.delta.ReadValue();
            yaw += lookDelta.x * lookSpeed;
            pitch -= lookDelta.y * lookSpeed;
            pitch = Mathf.Clamp(pitch, -90f, 90f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        float currentSpeed = moveSpeed;
        if (keyboard != null && keyboard.leftShiftKey.isPressed)
        {
            currentSpeed *= fastMoveMultiplier;
        }

        if (moveDirection != Vector3.zero)
        {
            transform.Translate(moveDirection.normalized * currentSpeed * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// 中クリックによるパン（平行移動）
    /// </summary>
    private void HandlePanning()
    {
        if (_touchPanActive)
        {
            ApplyPan(_touchPanDelta);
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed) return;
        ApplyPan(mouse.delta.ReadValue());
    }

    /// <summary>
    /// マウスホイールによるズーム（前後移動）
    /// </summary>
    private void HandleZoom()
    {
        if (_touchZoomDelta != 0f)
        {
            ApplyZoom(Mathf.Sign(_touchZoomDelta));
            return;
        }

        if (IsPointerOverUi()) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scrollValue = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollValue) <= 0.01f) return;

        ApplyZoom(Mathf.Sign(scrollValue));
    }

    private void ApplyPan(Vector2 delta)
    {
        Vector3 panDirection = new Vector3(-delta.x, -delta.y, 0f);
        transform.Translate(panDirection * panSpeed * Time.deltaTime, Space.Self);
    }

    private void ApplyZoom(float direction)
    {
        transform.Translate(Vector3.forward * direction * scrollSpeed * Time.deltaTime, Space.Self);
    }

    private void ReadTouchInput()
    {
        _touchLookActive = false;
        _touchPanActive = false;
        _touchLookDelta = Vector2.zero;
        _touchPanDelta = Vector2.zero;
        _touchZoomDelta = 0f;

        if (Touchscreen.current == null)
        {
            ResetTouchGestureState();
            return;
        }

        int worldTouchCount = 0;
        Touch firstTouch = default;
        Touch secondTouch = default;

        foreach (Touch touch in Touch.activeTouches)
        {
            if (!IsWorldTouch(touch)) continue;

            if (worldTouchCount == 0) firstTouch = touch;
            else if (worldTouchCount == 1) secondTouch = touch;
            worldTouchCount++;
        }

        if (worldTouchCount == 1)
        {
            _touchLookActive = true;
            _touchLookDelta = firstTouch.delta;
            ResetTouchGestureState();
            return;
        }

        if (worldTouchCount < 2)
        {
            ResetTouchGestureState();
            return;
        }

        Vector2 center = (firstTouch.screenPosition + secondTouch.screenPosition) * 0.5f;
        float distance = Vector2.Distance(firstTouch.screenPosition, secondTouch.screenPosition);
        if (_hasPreviousTwoTouch)
        {
            _touchPanActive = true;
            _touchPanDelta = center - _previousTwoTouchCenter;
            _touchZoomDelta = distance - _previousTwoTouchDistance;
        }

        _previousTwoTouchCenter = center;
        _previousTwoTouchDistance = distance;
        _hasPreviousTwoTouch = true;
    }

    private bool IsWorldTouch(Touch touch)
    {
        if (touch.phase == TouchPhase.Began)
        {
            if (IsTouchOverUi(touch))
            {
                _touchesStartedOnUi.Add(touch.touchId);
                return false;
            }

            _touchesStartedOnUi.Remove(touch.touchId);
        }

        if (_touchesStartedOnUi.Contains(touch.touchId)) return false;
        return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
    }

    private bool IsTouchOverUi(Touch touch)
    {
        for (int i = 0; i < _touchControlRegions.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    _touchControlRegions[i], touch.screenPosition, null))
                return true;
        }

        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject(touch.touchId);
    }

    private void ResetTouchInput()
    {
        ResetTouchGestureState();
        _touchesStartedOnUi.Clear();
    }

    private void ResetTouchGestureState()
    {
        _hasPreviousTwoTouch = false;
        _previousTwoTouchCenter = Vector2.zero;
        _previousTwoTouchDistance = 0f;
    }

    private void UpdateTouchControls()
    {
        bool touchAvailable = Touchscreen.current != null;
        if (!touchAvailable)
        {
            if (_touchControlsRoot != null) _touchControlsRoot.SetActive(false);
            return;
        }

        if (_touchControlsRoot == null)
        {
            CreateTouchControls();
        }

        _touchControlsRoot.SetActive(true);
    }

    private void CreateTouchControls()
    {
        EnsureTouchEventSystem();

        _touchControlsRoot = new GameObject(
            "CameraTouchControls",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        _touchControlsRoot.SetActive(false);

        Canvas canvas = _touchControlsRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = _touchControlsRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        CreateTouchStick(new Vector2(144f, 132f));
        CreateTouchKey("Q", "q", new Vector2(24f, 22f));
        CreateTouchKey("Shift", "leftShift", new Vector2(108f, 22f), new Vector2(120f, 72f));
        CreateTouchKey("E", "e", new Vector2(240f, 22f));
    }

    private void CreateTouchStick(Vector2 position)
    {
        GameObject stickObject = new GameObject(
            "MoveStick",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(OnScreenStick));
        stickObject.SetActive(false);
        stickObject.transform.SetParent(_touchControlsRoot.transform, false);

        RectTransform rect = stickObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180f, 180f);
        _touchControlRegions.Add(rect);

        RawImage background = stickObject.GetComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.5f);

        OnScreenStick screenStick = stickObject.GetComponent<OnScreenStick>();
        screenStick.controlPath = "<Gamepad>/leftStick";
        screenStick.movementRange = 72f;
        screenStick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;
        screenStick.useIsolatedInputActions = true;
        stickObject.SetActive(true);
    }

    private void CreateTouchKey(
        string label,
        string keyPath,
        Vector2 position,
        Vector2 size = default)
    {
        if (size == default) size = new Vector2(72f, 72f);

        GameObject keyObject = new GameObject(
            label,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(Button),
            typeof(OnScreenButton));
        keyObject.SetActive(false);
        keyObject.transform.SetParent(_touchControlsRoot.transform, false);

        RectTransform rect = keyObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        _touchControlRegions.Add(rect);

        RawImage background = keyObject.GetComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.65f);

        Button button = keyObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.65f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = colors.normalColor;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(keyObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = label == "Shift" ? 20 : 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        OnScreenButton screenButton = keyObject.GetComponent<OnScreenButton>();
        screenButton.controlPath = $"<Keyboard>/{keyPath}";
        keyObject.SetActive(true);
    }

    private void EnsureTouchEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            _touchEventSystemObject = new GameObject(
                "CameraTouchEventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() != null) return;

        BaseInputModule[] inputModules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < inputModules.Length; i++)
        {
            inputModules[i].enabled = false;
        }

        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        if (eventSystem.IsPointerOverGameObject()) return true;
        return Mouse.current != null && eventSystem.IsPointerOverGameObject(Mouse.current.deviceId);
    }
}
