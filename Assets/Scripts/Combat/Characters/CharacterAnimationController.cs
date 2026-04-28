using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Transform _spriteRootObj;
    [SerializeField] private GameObject _frontLeftObj;
    [SerializeField] private GameObject _frontRightObj;
    [SerializeField] private GameObject _backLeftObj;
    [SerializeField] private GameObject _backRightObj;

    [Header("Billboard Settings")]
    [SerializeField, Range(0f, 45f), Tooltip("各表示範囲の中央からのビルボード回転許容角度（度）")]
    private float _billboardLimitAngle = 20f;

    [Header("Animation Settings")]
    [SerializeField, Tooltip("オブジェクト切り替え時の回転速度（度/秒）")] 
    private float _flipSpeed = 720f;

    // 列挙型にインデックスを割り当て、配列の参照に使用できるようにする
    private enum Direction
    {
        FrontLeft = 0,
        FrontRight = 1,
        BackLeft = 2,
        BackRight = 3
    }

    // 事前計算済み角度配列
    private static readonly float[] CenterAngles = { 45f, -45f, 135f, -135f };

    private Direction _currentState;
    private Direction _targetState;

    private float _currentAnimAngle = 0f;
    private float _targetAnimAngle = 0f;
    private bool _isFlipping = false;

    // Transformアクセスの負荷を下げるためのキャッシュ
    private Transform _myTransform;
    private Transform _cameraTransform;

    private void Start()
    {
        // 自身のTransformとメインカメラのTransformをキャッシュ
        _myTransform = transform;
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        _currentState = GetDirectionFromCamera(out _);
        _targetState = _currentState;
        UpdateActiveObject(_currentState);
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        // 1. カメラのローカル角度を取得し、目標状態を決定
        _targetState = GetDirectionFromCamera(out float localCamAngle);

        // 2. アニメーションの目標角度を設定
        if (_currentState != _targetState)
        {
            _isFlipping = true;

            float currentBaseAngle = GetClampedLocalBillboardAngle(_currentState, localCamAngle);
            
            float edge1 = localCamAngle + 90f;
            float edge2 = localCamAngle - 90f;
            
            float delta1 = Mathf.DeltaAngle(currentBaseAngle, edge1);
            float delta2 = Mathf.DeltaAngle(currentBaseAngle, edge2);
            
            _targetAnimAngle = Mathf.Abs(delta1) < Mathf.Abs(delta2) ? delta1 : delta2;
        }
        else
        {
            _isFlipping = false;
            _targetAnimAngle = 0f;
        }

        // 3. アニメーション中、または正面に戻りきっていない場合のみ計算を実行（待機中の無駄な計算を削減）
        if (_isFlipping || Mathf.Abs(_currentAnimAngle) > 0.001f)
        {
            _currentAnimAngle = Mathf.MoveTowardsAngle(_currentAnimAngle, _targetAnimAngle, _flipSpeed * Time.deltaTime);

            if (_isFlipping && Mathf.Abs(Mathf.DeltaAngle(_currentAnimAngle, _targetAnimAngle)) <= 0.001f)
            {
                float oldBaseAngle = GetClampedLocalBillboardAngle(_currentState, localCamAngle);
                float newBaseAngle = GetClampedLocalBillboardAngle(_targetState, localCamAngle);
                
                float currentRealAngle = oldBaseAngle + _currentAnimAngle;
                _currentAnimAngle = Mathf.DeltaAngle(newBaseAngle, currentRealAngle);

                _currentState = _targetState;
                UpdateActiveObject(_currentState);
                
                _isFlipping = false;
            }
        }

        // 4. 現在の状態に応じたビルボードのベース角度を計算
        float finalLocalBillboardAngle = GetClampedLocalBillboardAngle(_currentState, localCamAngle);

        // 5. _spriteRootObjに回転を適用
        _spriteRootObj.rotation = _myTransform.rotation * Quaternion.Euler(0f, finalLocalBillboardAngle + _currentAnimAngle, 0f);
    }

    private Direction GetDirectionFromCamera(out float localAngle)
    {
        // キャッシュしたTransformを使用
        Vector3 localCamPos = _myTransform.InverseTransformPoint(_cameraTransform.position);
        
        localAngle = Mathf.Atan2(localCamPos.x, localCamPos.z) * Mathf.Rad2Deg;

        if (localCamPos.z >= 0f)
        {
            return localCamPos.x >= 0f ? Direction.FrontLeft : Direction.FrontRight;
        }
        else
        {
            return localCamPos.x >= 0f ? Direction.BackLeft : Direction.BackRight;
        }
    }

    private float GetClampedLocalBillboardAngle(Direction dir, float localCamAngle)
    {
        // switch文を廃止し、配列アクセスによる高速化
        float centerAngle = CenterAngles[(int)dir];
        float deltaAngle = Mathf.DeltaAngle(centerAngle, localCamAngle);
        float clampedDelta = Mathf.Clamp(deltaAngle, -_billboardLimitAngle, _billboardLimitAngle);
        return centerAngle + clampedDelta;
    }

    private void UpdateActiveObject(Direction dir)
    {
        if (_frontRightObj) _frontRightObj.SetActive(dir == Direction.FrontLeft);
        if (_frontLeftObj) _frontLeftObj.SetActive(dir == Direction.FrontRight);
        if (_backRightObj) _backRightObj.SetActive(dir == Direction.BackLeft);
        if (_backLeftObj) _backLeftObj.SetActive(dir == Direction.BackRight);
    }
}