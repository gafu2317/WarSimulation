using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Character : MonoBehaviour
{
    // キャラクターの基礎データ
    public CharacterData CharacterData { private set; get; }

    // パラメータ
    public int MaxHP { private set; get; }
    public int HP { private set; get; }
    public int CP { private set; get; }
    public int STR { private set; get; }
    public int INT { private set; get; }
    public int FAI { private set; get; }
    public int AGI { private set; get; }

    // バフ・デバフ率
    public float STRBuff { private set; get; } = 1f;
    public float INTBuff { private set; get; } = 1f;
    public float FAIBuff { private set; get; } = 1f;
    public float AGIBuff { private set; get; } = 1f;

    // 性格
    public PersonalityBase Personality { private set; get; }

    // 装備中の武器
    public WeaponBase EquippedWeapon { private set; get; }

    // 敵キャラの視認情報
    private Dictionary<Character, Vector3?> _lastSeenPositions = new Dictionary<Character, Vector3?>();
    private Dictionary<Character, float> _lastSeenTime = new Dictionary<Character, float>();

    // 視界処理用定数
    private readonly Vector3 HeadOffsetFromFoot = new Vector3(0, 1f, 0);
    private const float VerticalFOV = 90f;
    private const float HorizontalFOV = 120f;
    private const float MaxSightDistance = 30f;
    private const float SearchTimeout = 10f;

    // --- 移動・環境影響 用の変数 ---
    private NavMeshAgent _agent;
    private float _baseSpeed; // 風の影響がないときの基準速度
    
    [Header("移動設定")]
    [Tooltip("風の影響をどれくらい受けるかの係数")]
    [SerializeField] private float _windEffectMultiplier = 0.5f;
    [Tooltip("向かい風で遅くなる際の最低速度倍率")]
    [SerializeField] private float _minSpeedRatio = 0.2f;

    private void Awake()
    {
        // NavMeshAgentの取得と初期設定
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            // インスペクターで設定されたSpeedを基準速度として記憶
            _baseSpeed = _agent.speed;
        }
    }

    private void Update()
    {
        // 毎フレーム風の影響を計算して速度を更新
        UpdateWindEffect();
    }

    // ステータス設定
    public void SetCharacterStatus(CharacterData characterData, Country country, SpiritData spirit)
    {
        // TODO: パラメータ計算の実装
        // 簡易的に基礎パラメータを設定
        CharacterData = characterData;
        MaxHP = characterData.MaxHP;
        HP = MaxHP;
        CP = characterData.CP;
        STR = characterData.STR;
        INT = characterData.INT;
        FAI = characterData.FAI;
        AGI = characterData.AGI;
        Personality = spirit.Personality;

        // AGIパラメータを基準速度(_baseSpeed)に反映させたい場合はここで設定します
        // 例: _baseSpeed = AGI * 0.5f; 
        //     if(_agent != null) _agent.speed = _baseSpeed;
    }

    // 武器装備
    public void EquipWeapon(WeaponBase weapon)
    {
        EquippedWeapon = weapon;
    }

    // 武器解除
    public void UnEquipWeapon()
    {
        EquippedWeapon = null;
    }

    // バトル開始時の初期化処理
    public void InitializeOnBattleStart()
    {
        _lastSeenPositions.Clear();
        _lastSeenTime.Clear();

        foreach (Character character in CombatSceneContext.Instance.CharacterSystem.EnemyCharacters)
        {
            _lastSeenPositions.Add(character, null);
            _lastSeenTime.Add(character, Time.time - SearchTimeout - 1f); // タイムアウトを即座に満たす
        }
    }

    // ==========================================
    // 移動制御メソッド
    // ==========================================

    /// <summary>
    /// 指定した目標地点へNavMeshを使用して移動を開始します
    /// </summary>
    public void MoveToTarget(Vector3 destination)
    {
        if (_agent == null || !_agent.isOnNavMesh) return;
        
        _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    /// <summary>
    /// 現在の移動を停止します
    /// </summary>
    public void StopMoving()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;
        
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    /// <summary>
    /// マップ情報の風ベクトルを取得し、進行方向との内積から移動速度を調整します
    /// </summary>
    private void UpdateWindEffect()
    {
        // エージェントが存在しない、または移動指示が出ていない場合は処理しない
        if (_agent == null || !_agent.hasPath) return;

        // CombatSceneContextからマップシステムへのアクセス
        if (CombatSceneContext.Instance != null && CombatSceneContext.Instance.MapSystem != null)
        {
            Vector3 windVector = CombatSceneContext.Instance.MapSystem.WindVector;
            float windMagnitude = windVector.magnitude;

            // 風が吹いていない（または極めて弱い）場合は基準速度に戻す
            if (windMagnitude < Mathf.Epsilon)
            {
                _agent.speed = _baseSpeed;
                return;
            }

            // エージェントが向かおうとしている方向と風向きの内積を計算
            Vector3 moveDir = _agent.desiredVelocity.normalized;
            Vector3 windDir = windVector.normalized;
            
            // 内積: 追い風ならプラス(最大1)、向かい風ならマイナス(最小-1)、横風なら0
            float dotProduct = Vector3.Dot(moveDir, windDir);

            // 速度の倍率を計算 (1.0 を基準に増減)
            // 例: 内積が 1.0(完全な追い風)、風力 2.0、係数 0.5 の場合 => 1f + (1.0 * 2.0 * 0.5) = 2.0倍の速度
            float speedRatio = 1f + (dotProduct * windMagnitude * _windEffectMultiplier);

            // 向かい風で極端に遅くなったり、マイナスになって逆走するのを防ぐ
            speedRatio = Mathf.Max(_minSpeedRatio, speedRatio);

            // 最終的な速度を適用
            _agent.speed = _baseSpeed * speedRatio;
        }
    }

    // ==========================================
    // 視界・記憶関連メソッド
    // ==========================================

    // 敵キャラの位置についての記憶を更新する
    protected void UpdateMemoryOfEnemies()
    {
        // 全ての敵キャラに対して見えているかを判定
        // 見えている場合は記憶を更新
        foreach (Character character in _lastSeenPositions.Keys)
        {
            if (HasLineOfSight(character.transform))
            {
                _lastSeenPositions[character] = character.transform.position;
                _lastSeenTime[character] = Time.time;
            }
            // 存在可能性考慮のタイムアウト
            else if (Time.time - _lastSeenTime[character] > SearchTimeout)
            {
                _lastSeenPositions[character] = null;
            }
        }
    }
    
    // ターゲットが視野角（FOV）内にあり、かつ視界を遮る障害物がないか判定
    private bool HasLineOfSight(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("ターゲットがアサインされていません");
            return false;
        }

        Vector3 headPos = transform.TransformPoint(HeadOffsetFromFoot);
        Vector3 targetHeadPos = target.TransformPoint(HeadOffsetFromFoot); 
        
        Vector3 diff = targetHeadPos - headPos;
        float distanceToTarget = diff.magnitude;
        
        // 完全に同位置（重なっている）場合は見えていると判定
        if (distanceToTarget < Mathf.Epsilon) 
        {
            // Debug.Log("完全に同位置");
            return true;
        }

        Vector3 dirToTarget = diff / distanceToTarget;

        // 1. 視認可能距離の判定
        if (distanceToTarget > MaxSightDistance)
        {
            return false;
        }

        // 2. 視野角の判定 (ローカル座標に変換)
        Vector3 localDir = transform.InverseTransformDirection(dirToTarget);

        // 水平視野角 (XZ平面)
        float horizontalAngle = Mathf.Abs(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg);
        if (horizontalAngle > HorizontalFOV * 0.5f)
        {
            // Debug.Log("水平視野角超過");
            return false;
        }

        // 垂直視野角 (YZ平面)
        float verticalAngle = Mathf.Abs(Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg);
        if (verticalAngle > VerticalFOV * 0.5f) 
        {
            // Debug.Log("垂直視野角超過");
            return false;
        }

        // 3. 障害物の判定（レイキャスト）
        // "Character" レイヤーのみを無視するレイヤーマスクを作成
        // 自分自身と他キャラ、見る対象自体を判定から除外
        int layerMask = ~LayerMask.GetMask("Character");
        Debug.DrawRay(headPos, dirToTarget * distanceToTarget, Color.red, 1f);

        // レイがヒット => 視線を遮るオブジェクトがある
        // レイのヒット無し => 視線を遮るオブジェクトがない
        return !Physics.Raycast(headPos, dirToTarget, distanceToTarget, layerMask);
    }
}