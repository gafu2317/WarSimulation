using UnityEngine;
using UnityEngine.InputSystem;

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

    private void Start()
    {
        // カメラの初期の回転角度を取得して適用
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
    }

    private void Update()
    {
        // マウスやキーボードが接続されていない場合は処理を抜ける
        if (Mouse.current == null || Keyboard.current == null) return;

        HandleRotationAndMovement();
        HandlePanning();
        HandleZoom();
    }

    /// <summary>
    /// 右クリックによる視点移動とWASD移動
    /// </summary>
    private void HandleRotationAndMovement()
    {
        // 右クリックが押されている間のみ実行
        if (Mouse.current.rightButton.isPressed)
        {
            // --- 視点回転 (Look) ---
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            yaw += mouseDelta.x * lookSpeed;
            pitch -= mouseDelta.y * lookSpeed;
            
            // ジンバルロック（カメラがひっくり返る）を防ぐための制限
            pitch = Mathf.Clamp(pitch, -90f, 90f);
            
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);

            // --- 移動 (Fly) ---
            Vector3 moveDirection = Vector3.zero;

            // WASDによる前後左右
            if (Keyboard.current.wKey.isPressed) moveDirection += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) moveDirection += Vector3.back;
            if (Keyboard.current.aKey.isPressed) moveDirection += Vector3.left;
            if (Keyboard.current.dKey.isPressed) moveDirection += Vector3.right;
            
            // Q/Eによる上下
            if (Keyboard.current.eKey.isPressed) moveDirection += Vector3.up;
            if (Keyboard.current.qKey.isPressed) moveDirection += Vector3.down;

            // Shiftキーで加速
            float currentSpeed = moveSpeed;
            if (Keyboard.current.leftShiftKey.isPressed)
            {
                currentSpeed *= fastMoveMultiplier;
            }

            // カメラが向いている方向（ローカル空間）を基準に移動させる
            if (moveDirection != Vector3.zero)
            {
                transform.Translate(moveDirection.normalized * currentSpeed * Time.deltaTime, Space.Self);
            }
        }
    }

    /// <summary>
    /// 中クリックによるパン（平行移動）
    /// </summary>
    private void HandlePanning()
    {
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            // マウスの動きと逆方向にカメラを動かしてパン操作を再現
            Vector3 panDirection = new Vector3(-mouseDelta.x, -mouseDelta.y, 0f);
            transform.Translate(panDirection * panSpeed * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// マウスホイールによるズーム（前後移動）
    /// </summary>
    private void HandleZoom()
    {
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        
        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            // ホイールの回転量に応じてカメラを前進・後退させる
            // Note: 環境によってスクロール値の大きさが変わるため、正規化してスピードを掛けています
            float zoomAmount = Mathf.Sign(scrollValue) * scrollSpeed;
            transform.Translate(Vector3.forward * zoomAmount * Time.deltaTime, Space.Self);
        }
    }
}