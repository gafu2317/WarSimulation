using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CombatCharacterBody))]
[RequireComponent(typeof(CombatVision))]
[RequireComponent(typeof(CombatHealth))]
[RequireComponent(typeof(CombatStatusEffects))]
[RequireComponent(typeof(CombatSkillCooldowns))]
[RequireComponent(typeof(CombatSkillCaster))]
public class Character : MonoBehaviour
{
    private static readonly ProfilerMarker RebuildSkillsMarker =
        new("CombatLoading.RebuildCharacterSkills");
    private static readonly Color AllyCharacterColor = new(0.3f, 0.7f, 1f, 1f);
    private static readonly Color EnemyCharacterColor = new(1f, 0.3f, 0.25f, 1f);

    [SerializeField] private CombatTeam _team = CombatTeam.Ally;
    [SerializeField] private WeaponConfig _initialWeaponConfig;
    [SerializeField] private CombatAiPersonalityProfile _personalityProfile;
    [SerializeField] private CombatSkillCatalog _skillCatalogOverride;
    [SerializeField] private List<SkillId> _learnedSkillIds = new();
    [SerializeField] private bool _unlockAllCatalogSkillsForKindWhenLearnedEmpty = true;
    [SerializeField, Min(0)] private int _baseSTR;
    [SerializeField, Min(0)] private int _baseINT;
    [SerializeField, Min(0)] private int _baseFAI;
    [SerializeField, Min(0)] private int _baseAGI;

    // キャラクターの基礎データ
    [field: SerializeField] public CharacterData CharacterData { private set; get; }
    public string DisplayName =>
        CharacterData != null && !string.IsNullOrWhiteSpace(CharacterData.CharacterName)
            ? CharacterData.CharacterName
            : gameObject.name;
    public int BattleParticipantId { get; private set; }
    public CombatTeam Team => _team;
    public CombatVision Vision => _vision != null ? _vision : GetComponent<CombatVision>();
    public CombatHealth Health => _health != null ? _health : GetComponent<CombatHealth>();
    public CombatStatusEffects StatusEffects => ResolveStatusEffects();
    public CombatSkillCooldowns SkillCooldowns => ResolveSkillCooldowns();
    public CombatSkillCaster SkillCaster => _skillCaster != null ? _skillCaster : GetComponent<CombatSkillCaster>();

    // パラメータ
    public int MaxHP => Health != null ? Health.MaxHP : 0;
    public int HP => Health != null ? Health.HP : 0;
    public int CP { private set; get; }
    public int STR
    {
        get
        {
            EnsureRuntimeStatsInitialized();
            return _str;
        }
        private set
        {
            EnsureRuntimeStatsInitialized();
            _str = value;
        }
    }
    public int INT
    {
        get
        {
            EnsureRuntimeStatsInitialized();
            return _int;
        }
        private set
        {
            EnsureRuntimeStatsInitialized();
            _int = value;
        }
    }
    public int FAI
    {
        get
        {
            EnsureRuntimeStatsInitialized();
            return _fai;
        }
        private set
        {
            EnsureRuntimeStatsInitialized();
            _fai = value;
        }
    }
    public int AGI
    {
        get
        {
            EnsureRuntimeStatsInitialized();
            return _agi;
        }
        private set
        {
            EnsureRuntimeStatsInitialized();
            _agi = value;
        }
    }

    // バフ・デバフ率
    public float STRBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.STR);
    public float INTBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.INT);
    public float FAIBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.FAI);
    public float AGIBuff => ResolveStatusEffects().GetMultiplier(CombatStatusEffects.StatKind.AGI);

    public CombatAiPersonalityProfile PersonalityProfile => _runtimePersonalityProfile != null
        ? _runtimePersonalityProfile
        : _personalityProfile;
    public Character TagalongTarget => _runtimeTagalongTarget;

    // 装備中の武器
    public WeaponBase EquippedWeapon { private set; get; }
    public WeaponConfig EquippedWeaponConfig { get; private set; }
    public IReadOnlyList<SkillBase> AvailableCombatSkills => _availableCombatSkills;
    public IReadOnlyList<SkillId> LearnedSkillIds => _learnedSkillIds;

    private readonly List<SkillBase> _availableCombatSkills = new();
    private NavMeshAgent _agent;
    private CombatCharacterBody _body;
    private CombatVision _vision;
    private CombatHealth _health;
    private CombatStatusEffects _statusEffects;
    private CombatSkillCooldowns _skillCooldowns;
    private CombatSkillCaster _skillCaster;
    private CombatAiPersonalityProfile _runtimePersonalityProfile;
    private Character _runtimeTagalongTarget;
    private WeaponConfig _runtimeWeaponConfig;
    private int _str;
    private int _int;
    private int _fai;
    private int _agi;
    private bool _runtimeStatsInitialized;
    private readonly Dictionary<CombatStat, int> _runtimeStatAdjustments = new();

    private void Awake()
    {
        STR = _baseSTR;
        INT = _baseINT;
        FAI = _baseFAI;
        AGI = _baseAGI;

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

        _skillCaster = GetComponent<CombatSkillCaster>();

        ApplyInitialWeaponFromConfig();
    }

    private void EnsureRuntimeStatsInitialized()
    {
        if (_runtimeStatsInitialized) return;

        _str = _baseSTR;
        _int = _baseINT;
        _fai = _baseFAI;
        _agi = _baseAGI;
        _runtimeStatsInitialized = true;
    }

    private void Start()
    {
        EnsureHealthBar();
    }

    private void EnsureHealthBar()
    {
        CombatWorldHealthBar bar = GetComponent<CombatWorldHealthBar>();
        if (bar == null)
        {
            bar = gameObject.AddComponent<CombatWorldHealthBar>();
        }

        _health ??= GetComponent<CombatHealth>();
        if (_health != null)
        {
            bar.Configure(_health);
        }
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

    public void SetTeam(CombatTeam team)
    {
        _team = team;
        ApplyTeamColor();
    }

    private void ApplyTeamColor()
    {
        Color teamColor = _team == CombatTeam.Enemy
            ? EnemyCharacterColor
            : AllyCharacterColor;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].color = teamColor;
        }
    }

    public void SetBattleParticipantId(int participantId)
    {
        BattleParticipantId = participantId;
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
        ClearRuntimeStatAdjustments();
        _runtimePersonalityProfile = spirit != null ? spirit.PersonalityProfile : null;
        _health ??= GetComponent<CombatHealth>();
        _health?.Initialize(characterData.MaxHP);

        // AGIパラメータを基準速度に反映させたい場合は CombatCharacterBody.BaseSpeed を更新する。
    }

    public void SetCharacterDataForBattle(CharacterData characterData)
    {
        SetCharacterStatus(characterData, null, null);
    }

    public void ApplyInitialWeaponFromConfig()
    {
        WeaponConfig weaponConfig = _runtimeWeaponConfig != null
            ? _runtimeWeaponConfig
            : _initialWeaponConfig;
        if (weaponConfig == null) return;
        if (ReferenceEquals(EquippedWeaponConfig, weaponConfig) && EquippedWeapon != null) return;

        EquipWeapon(weaponConfig.CreateWeapon(), weaponConfig);
    }

    public void ConfigureForBattle(
        WeaponConfig weaponConfig,
        CombatAiPersonalityProfile personalityProfile,
        float movementSpeedMultiplier = 1f,
        Character tagalongTarget = null,
        IReadOnlyDictionary<CombatStat, int> statAdjustments = null)
    {
        _runtimeWeaponConfig = weaponConfig;
        _runtimePersonalityProfile = personalityProfile;
        _runtimeTagalongTarget = tagalongTarget;
        SetRuntimeStatAdjustments(statAdjustments);
        ApplyInitialWeaponFromConfig();
        _body ??= GetComponent<CombatCharacterBody>();
        if (_body != null) _body.MovementSpeedMultiplier = movementSpeedMultiplier;
    }

    public void SetRuntimeStatAdjustments(IReadOnlyDictionary<CombatStat, int> statAdjustments)
    {
        _runtimeStatAdjustments.Clear();
        if (statAdjustments == null) return;

        foreach (KeyValuePair<CombatStat, int> adjustment in statAdjustments)
        {
            if (adjustment.Value != 0)
            {
                _runtimeStatAdjustments[adjustment.Key] = adjustment.Value;
            }
        }
    }

    public void ClearRuntimeStatAdjustments()
    {
        _runtimeStatAdjustments.Clear();
    }

    // 武器装備
    public void EquipWeapon(WeaponBase weapon, WeaponConfig sourceConfig = null)
    {
        EquippedWeapon = weapon;
        EquippedWeaponConfig = sourceConfig;
        RebuildCombatSkills();
    }

    // 武器解除
    public void UnEquipWeapon()
    {
        EquippedWeapon = null;
        EquippedWeaponConfig = null;
        RebuildCombatSkills();
    }

    public float GetEffectiveStat(CombatStat stat)
    {
        WeaponBase weapon = EquippedWeapon ?? WeaponBase.Unarmed;
        return stat switch
        {
            CombatStat.STR => GetConfiguredStat(stat, weapon) * STRBuff,
            CombatStat.INT => GetConfiguredStat(stat, weapon) * INTBuff,
            CombatStat.FAI => GetConfiguredStat(stat, weapon) * FAIBuff,
            CombatStat.AGI => GetConfiguredStat(stat, weapon) * AGIBuff,
            _ => 0f,
        };
    }

    private int GetConfiguredStat(CombatStat stat, WeaponBase weapon)
    {
        int baseValue = stat switch
        {
            CombatStat.STR => STR,
            CombatStat.INT => INT,
            CombatStat.FAI => FAI,
            CombatStat.AGI => AGI,
            _ => 0,
        };
        int adjustment = _runtimeStatAdjustments.TryGetValue(stat, out int value) ? value : 0;
        return Mathf.Max(1, baseValue + weapon.GetStatBonus(stat) + adjustment);
    }

    public void SetLearnedSkillIds(IEnumerable<SkillId> skillIds)
    {
        _learnedSkillIds.Clear();
        if (skillIds == null)
        {
            RebuildCombatSkills();
            return;
        }

        foreach (SkillId skillId in skillIds)
        {
            if (skillId == SkillId.None) continue;
            _learnedSkillIds.Add(skillId);
        }

        RebuildCombatSkills();
    }

    public void RebuildCombatSkills()
    {
        using var _ = RebuildSkillsMarker.Auto();
        _availableCombatSkills.Clear();

        WeaponBase weapon = EquippedWeapon;
        if (weapon == null || weapon.Kind == WeaponKind.Unarmed)
        {
            return;
        }

        CombatSkillCatalog catalog = ResolveSkillCatalog();
        if (catalog == null)
        {
            return;
        }

        IReadOnlyList<SkillBase> builtSkills = CombatSkillLoadoutBuilder.Build(
            catalog,
            weapon.Kind,
            _learnedSkillIds,
            weapon.GrantedSkillIds,
            _unlockAllCatalogSkillsForKindWhenLearnedEmpty,
            weapon);

        for (int i = 0; i < builtSkills.Count; i++)
        {
            SkillBase skill = builtSkills[i];
            if (skill != null)
            {
                _availableCombatSkills.Add(skill);
            }
        }
    }

    private CombatSkillCatalog ResolveSkillCatalog()
    {
        if (_skillCatalogOverride != null)
        {
            return _skillCatalogOverride;
        }

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.SkillCatalog != null)
        {
            return context.SkillCatalog;
        }

        return CombatSkillCatalog.CreateDefaultRuntimeCatalog();
    }

    // バトル開始時の初期化処理
    public void InitializeOnBattleStart()
    {
        ApplyInitialWeaponFromConfig();
        _vision ??= GetComponent<CombatVision>();
        _vision?.Initialize();
        GetComponent<CombatAiBrain>()?.ResetForBattle();
    }

    // ==========================================
    // 移動制御メソッド
    // ==========================================

    /// <summary>
    /// 指定した目標地点へNavMeshを使用して移動を開始します
    /// </summary>
    public bool MoveToTarget(Vector3 destination)
    {
        if (_body != null)
        {
            return _body.TrySetDestination(destination);
        }

        if (_agent == null || !_agent.isOnNavMesh) return false;
        
        _agent.isStopped = false;
        return _agent.SetDestination(destination);
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

    /// <summary>水平方向だけ目標へ向く。魔石攻撃前の視線合わせに使う。</summary>
    public void FaceHorizontalToward(Vector3 worldPosition)
    {
        Vector3 delta = worldPosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f) return;

        Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        transform.rotation = rotation;
        if (_agent != null && _agent.enabled)
        {
            _agent.nextPosition = transform.position;
        }
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
}
