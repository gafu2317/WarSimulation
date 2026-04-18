using System.Collections.Generic;
using UnityEngine;

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
        // ターゲットがアサインされていない、または破棄された場合は false
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
        RaycastHit hit;
        Debug.DrawRay(headPos, dirToTarget * distanceToTarget, Color.red, 1f);

        // レイがヒット => 視線を遮るオブジェクトがある
        // レイのヒット無し => 視線を遮るオブジェクトがない
        return !Physics.Raycast(headPos, dirToTarget, out hit, distanceToTarget, layerMask);
    }
}