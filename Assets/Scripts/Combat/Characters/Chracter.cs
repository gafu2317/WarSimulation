using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CombatCharacterBody))]
[RequireComponent(typeof(CombatVision))]
[RequireComponent(typeof(CombatHealth))]
[RequireComponent(typeof(CombatAttack))]
[RequireComponent(typeof(CombatStatusEffects))]
[RequireComponent(typeof(CombatSkillCooldowns))]
public class Character : MonoBehaviour
{
    [SerializeField] private CombatTeam _team = CombatTeam.Ally;
    [SerializeField] private WeaponConfig _initialWeaponConfig;

    // キャラクターの基礎データ
    public CharacterData CharacterData { private set; get; }
    public CombatTeam Team => _team;
    public CombatVision Vision => _vision != null ? _vision : GetComponent<CombatVision>();
    public CombatHealth Health => _health != null ? _health : GetComponent<CombatHealth>();
    public CombatAttack Attack => _attack != null ? _attack : GetComponent<CombatAttack>();
    public CombatStatusEffects StatusEffects => ResolveStatusEffects();
    public CombatSkillCooldowns SkillCooldowns => ResolveSkillCooldowns();

    // パラメータ
    public int MaxHP => Health != null ? Health.MaxHP : 0;
    public int HP => Health != null ? Health.HP : 0;
    public int CP { private set; get; }
    public int STR { private set; get; }
    public int INT { private set; get; }
    public int FAI { private set; get; }
    public int AGI { private set; get; }

    // バフ・デバフ率
    public float STRBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.STR);
    public float INTBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.INT);
    public float FAIBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.FAI);
    public float AGIBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.AGI);

    // 性格
    public PersonalityBase Personality => ResolvePersonality();

    // 装備中の武器
    public WeaponBase EquippedWeapon { private set; get; }

    private NavMeshAgent _agent;
    private CombatCharacterBody _body;
    private CombatVision _vision;
    private CombatHealth _health;
    private CombatAttack _attack;
    private CombatStatusEffects _statusEffects;
    private CombatSkillCooldowns _skillCooldowns;
    private PersonalityBase _personality;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _body = GetComponent<CombatCharacterBody>();
        if (_body == null)
        {
            _body = gameObject.AddComponent<CombatCharacterBody>();
        }

        _vision = GetComponent<CombatVision>();
        if (_vision == null)
        {
            _vision = gameObject.AddComponent<CombatVision>();
        }

        _health = GetComponent<CombatHealth>();
        if (_health == null)
        {
            _health = gameObject.AddComponent<CombatHealth>();
        }

        _attack = GetComponent<CombatAttack>();
        if (_attack == null)
        {
            _attack = gameObject.AddComponent<CombatAttack>();
        }

        _statusEffects = GetComponent<CombatStatusEffects>();
        if (_statusEffects == null)
        {
            _statusEffects = gameObject.AddComponent<CombatStatusEffects>();
        }

        _skillCooldowns = GetComponent<CombatSkillCooldowns>();
        if (_skillCooldowns == null)
        {
            _skillCooldowns = gameObject.AddComponent<CombatSkillCooldowns>();
        }

        ResolvePersonality();

        ApplyInitialWeaponFromConfig();
    }

    private CombatStatusEffects ResolveStatusEffects()
    {
        if (_statusEffects != null) return _statusEffects;

        _statusEffects = GetComponent<CombatStatusEffects>();
        if (_statusEffects == null)
        {
            _statusEffects = gameObject.AddComponent<CombatStatusEffects>();
        }

        return _statusEffects;
    }

    private CombatSkillCooldowns ResolveSkillCooldowns()
    {
        if (_skillCooldowns != null) return _skillCooldowns;

        _skillCooldowns = GetComponent<CombatSkillCooldowns>();
        if (_skillCooldowns == null)
        {
            _skillCooldowns = gameObject.AddComponent<CombatSkillCooldowns>();
        }

        return _skillCooldowns;
    }

    private PersonalityBase ResolvePersonality()
    {
        if (_personality != null) return _personality;

        _personality = GetComponent<PersonalityBase>();
        if (_personality == null)
        {
            _personality = gameObject.AddComponent<PlainPersonality>();
        }

        return _personality;
    }

    public void SetTeam(CombatTeam team)
    {
        _team = team;
    }

    // ステータス設定
    public void SetCharacterStatus(CharacterData characterData, Country country, SpiritData spirit)
    {
        // TODO: パラメータ計算の実装
        // 簡易的に基礎パラメータを設定
        CharacterData = characterData;
        CP = characterData.CP;
        STR = characterData.STR;
        INT = characterData.INT;
        FAI = characterData.FAI;
        AGI = characterData.AGI;
        _personality = spirit != null && spirit.Personality != null
            ? ResolvePersonality(spirit.Personality.GetType())
            : ResolvePersonality();
        _health ??= GetComponent<CombatHealth>();
        _health?.Initialize(characterData.MaxHP);

        // AGIパラメータを基準速度に反映させたい場合は CombatCharacterBody.BaseSpeed を更新する。
    }

    public void ApplyInitialWeaponFromConfig()
    {
        if (_initialWeaponConfig == null) return;

        EquipWeapon(_initialWeaponConfig.CreateWeapon());
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
        ApplyInitialWeaponFromConfig();
        _vision ??= GetComponent<CombatVision>();
        _vision?.Initialize();
    }

    // ==========================================
    // 移動制御メソッド
    // ==========================================

    /// <summary>
    /// 指定した目標地点へNavMeshを使用して移動を開始します
    /// </summary>
    public void MoveToTarget(Vector3 destination)
    {
        if (_body != null)
        {
            _body.TrySetDestination(destination);
            return;
        }

        if (_agent == null || !_agent.isOnNavMesh) return;
        
        _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    /// <summary>
    /// 現在の移動を停止します
    /// </summary>
    public void StopMoving()
    {
        if (_body != null)
        {
            _body.Stop();
            return;
        }

        if (_agent == null || !_agent.isOnNavMesh) return;
        
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    // ==========================================
    // 視界・記憶関連メソッド
    // ==========================================

    // 敵味方のキャラの位置についての記憶を更新する
    protected void UpdateMemoryOfEnemies()
    {
        _vision ??= GetComponent<CombatVision>();
        _vision?.UpdateVision();
    }

    private PersonalityBase ResolvePersonality(System.Type personalityType)
    {
        if (personalityType == null || !typeof(PersonalityBase).IsAssignableFrom(personalityType))
        {
            return ResolvePersonality();
        }

        if (_personality != null && _personality.GetType() == personalityType)
        {
            return _personality;
        }

        PersonalityBase personality = GetComponent(personalityType) as PersonalityBase;
        if (personality == null)
        {
            personality = gameObject.AddComponent(personalityType) as PersonalityBase;
        }

        _personality = personality != null ? personality : ResolvePersonality();
        return _personality;
    }
}
