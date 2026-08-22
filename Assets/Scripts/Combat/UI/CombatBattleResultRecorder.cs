using System;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatBattleResult
{
    public CombatBattleResult(
        CombatBattleState outcome,
        float durationSeconds,
        CombatBattleTeamResult allies,
        CombatBattleTeamResult enemies)
    {
        Outcome = outcome;
        DurationSeconds = durationSeconds;
        Allies = allies;
        Enemies = enemies;
    }

    public CombatBattleState Outcome { get; }
    public float DurationSeconds { get; }
    public CombatBattleTeamResult Allies { get; }
    public CombatBattleTeamResult Enemies { get; }
}

public sealed class CombatBattleStatusEffectResult
{
    public CombatBattleStatusEffectResult(
        CombatStatusEffects.StatKind stat,
        bool isBuff,
        int activationCount,
        int targetCount,
        float durationSeconds)
    {
        Stat = stat;
        IsBuff = isBuff;
        ActivationCount = activationCount;
        TargetCount = targetCount;
        DurationSeconds = durationSeconds;
    }

    public CombatStatusEffects.StatKind Stat { get; }
    public bool IsBuff { get; }
    public int ActivationCount { get; }
    public int TargetCount { get; }
    public float DurationSeconds { get; }
}

public sealed class CombatBattleSupportSummary
{
    public static readonly CombatBattleSupportSummary Empty =
        new CombatBattleSupportSummary(0, 0, 0f, 0, 0, 0f);

    public CombatBattleSupportSummary(
        int buffActivationCount,
        int buffTargetCount,
        float buffDurationSeconds,
        int debuffActivationCount,
        int debuffTargetCount,
        float debuffDurationSeconds)
    {
        BuffActivationCount = buffActivationCount;
        BuffTargetCount = buffTargetCount;
        BuffDurationSeconds = buffDurationSeconds;
        DebuffActivationCount = debuffActivationCount;
        DebuffTargetCount = debuffTargetCount;
        DebuffDurationSeconds = debuffDurationSeconds;
    }

    public int BuffActivationCount { get; }
    public int BuffTargetCount { get; }
    public float BuffDurationSeconds { get; }
    public int DebuffActivationCount { get; }
    public int DebuffTargetCount { get; }
    public float DebuffDurationSeconds { get; }
}

public sealed class CombatBattleTeamResult
{
    public CombatBattleTeamResult(
        CombatTeam team,
        int participantCount,
        int aliveCount,
        int damageDealt,
        int damageTaken,
        int healingDone,
        int damagePrevented,
        int magicStoneDamage,
        int magicStoneHp,
        int magicStoneMaxHp,
        IReadOnlyList<CombatBattleCharacterResult> characters)
        : this(
            team,
            participantCount,
            aliveCount,
            damageDealt,
            damageTaken,
            healingDone,
            damagePrevented,
            magicStoneDamage,
            magicStoneHp,
            magicStoneMaxHp,
            characters,
            CombatBattleSupportSummary.Empty)
    {
    }

    public CombatBattleTeamResult(
        CombatTeam team,
        int participantCount,
        int aliveCount,
        int damageDealt,
        int damageTaken,
        int healingDone,
        int damagePrevented,
        int magicStoneDamage,
        int magicStoneHp,
        int magicStoneMaxHp,
        IReadOnlyList<CombatBattleCharacterResult> characters,
        CombatBattleSupportSummary supportSummary)
    {
        Team = team;
        ParticipantCount = participantCount;
        AliveCount = aliveCount;
        DamageDealt = damageDealt;
        DamageTaken = damageTaken;
        HealingDone = healingDone;
        DamagePrevented = damagePrevented;
        MagicStoneDamage = magicStoneDamage;
        MagicStoneHp = magicStoneHp;
        MagicStoneMaxHp = magicStoneMaxHp;
        Characters = characters;
        SupportSummary = supportSummary ?? CombatBattleSupportSummary.Empty;
    }

    public CombatTeam Team { get; }
    public int ParticipantCount { get; }
    public int AliveCount { get; }
    public int DamageDealt { get; }
    public int DamageTaken { get; }
    public int HealingDone { get; }
    public int DamagePrevented { get; }
    public int MagicStoneDamage { get; }
    public int MagicStoneHp { get; }
    public int MagicStoneMaxHp { get; }
    public IReadOnlyList<CombatBattleCharacterResult> Characters { get; }
    public CombatBattleSupportSummary SupportSummary { get; }
}

public sealed class CombatBattleCharacterResult
{
    public CombatBattleCharacterResult(
        string displayName,
        string personalityDisplayName,
        string weaponDisplayName,
        bool isAlive,
        int damageDealt,
        int magicStoneDamage,
        int damageTaken,
        int healingDone,
        int defeats,
        int deaths)
        : this(
            displayName,
            personalityDisplayName,
            weaponDisplayName,
            isAlive,
            damageDealt,
            magicStoneDamage,
            damageTaken,
            healingDone,
            defeats,
            deaths,
            CombatBattleSupportSummary.Empty,
            Array.Empty<CombatBattleStatusEffectResult>())
    {
    }

    public CombatBattleCharacterResult(
        string displayName,
        string personalityDisplayName,
        string weaponDisplayName,
        bool isAlive,
        int damageDealt,
        int magicStoneDamage,
        int damageTaken,
        int healingDone,
        int defeats,
        int deaths,
        CombatBattleSupportSummary supportSummary,
        IReadOnlyList<CombatBattleStatusEffectResult> supportEffects)
    {
        DisplayName = displayName;
        PersonalityDisplayName = personalityDisplayName;
        WeaponDisplayName = weaponDisplayName;
        IsAlive = isAlive;
        DamageDealt = damageDealt;
        MagicStoneDamage = magicStoneDamage;
        DamageTaken = damageTaken;
        HealingDone = healingDone;
        Defeats = defeats;
        Deaths = deaths;
        SupportSummary = supportSummary ?? CombatBattleSupportSummary.Empty;
        SupportEffects = supportEffects ?? Array.Empty<CombatBattleStatusEffectResult>();
    }

    public string DisplayName { get; }
    public string PersonalityDisplayName { get; }
    public string WeaponDisplayName { get; }
    public bool IsAlive { get; }
    public int DamageDealt { get; }
    public int MagicStoneDamage { get; }
    public int DamageTaken { get; }
    public int HealingDone { get; }
    public int Defeats { get; }
    public int Deaths { get; }
    public CombatBattleSupportSummary SupportSummary { get; }
    public IReadOnlyList<CombatBattleStatusEffectResult> SupportEffects { get; }
}

[DisallowMultipleComponent]
public sealed class CombatBattleResultRecorder : MonoBehaviour
{
    private sealed class MutableCharacterResult
    {
        public MutableCharacterResult(Character character, CombatTeam team)
        {
            Character = character;
            Team = team;
        }

        public Character Character { get; }
        public CombatTeam Team { get; }
        public int DamageDealt { get; set; }
        public int MagicStoneDamage { get; set; }
        public int DamageTaken { get; set; }
        public int HealingDone { get; set; }
        public int DamagePrevented { get; set; }
        public int Defeats { get; set; }
        public int Deaths { get; set; }
    }

    private readonly Dictionary<Character, MutableCharacterResult> _characterResults =
        new Dictionary<Character, MutableCharacterResult>();
    private readonly List<Character> _allyCharacters = new List<Character>();
    private readonly List<Character> _enemyCharacters = new List<Character>();
    private readonly List<CombatHealth> _subscribedHealth = new List<CombatHealth>();
    private readonly CombatBattleStatusEffectTracker _statusEffectTracker =
        new CombatBattleStatusEffectTracker();

    private CombatMagicStoneSystem _magicStoneSystem;
    private CombatBattleResult _currentResult;
    private float _battleStartTime;
    private bool _isRecording;
    private int _allyMagicStoneDamage;
    private int _enemyMagicStoneDamage;

    public CombatBattleResult CurrentResult => _currentResult;

    private void OnDisable()
    {
        Clear();
    }

    public void Begin(
        IReadOnlyList<Character> allies,
        IReadOnlyList<Character> enemies)
    {
        Clear();
        _battleStartTime = Time.time;
        AddCharacters(allies, CombatTeam.Ally, _allyCharacters);
        AddCharacters(enemies, CombatTeam.Enemy, _enemyCharacters);
        _statusEffectTracker.Begin(_allyCharacters, _enemyCharacters, _battleStartTime);
        SubscribeCombatEvents();
        SubscribeCharacterHealth();

        _magicStoneSystem = CombatMagicStoneSystemResolver.Resolve();
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.Damaged += OnMagicStoneDamaged;
        }

        _isRecording = true;
    }

    public CombatBattleResult Complete(CombatBattleState outcome)
    {
        if (!_isRecording ||
            (outcome != CombatBattleState.Victory && outcome != CombatBattleState.Defeat))
        {
            return _currentResult;
        }

        float battleEndTime = Time.time;
        _statusEffectTracker.Complete(battleEndTime);
        CombatBattleTeamResult allies = BuildTeamResult(CombatTeam.Ally, _allyCharacters);
        CombatBattleTeamResult enemies = BuildTeamResult(CombatTeam.Enemy, _enemyCharacters);
        _currentResult = new CombatBattleResult(
            outcome,
            Mathf.Max(0f, battleEndTime - _battleStartTime),
            allies,
            enemies);

        _isRecording = false;
        UnsubscribeCombatEvents();
        UnsubscribeCharacterHealth();
        UnsubscribeMagicStoneEvents();
        return _currentResult;
    }

    public void Clear()
    {
        _isRecording = false;
        UnsubscribeCombatEvents();
        UnsubscribeCharacterHealth();
        UnsubscribeMagicStoneEvents();
        _characterResults.Clear();
        _allyCharacters.Clear();
        _enemyCharacters.Clear();
        _statusEffectTracker.Clear();
        _currentResult = null;
        _battleStartTime = 0f;
        _allyMagicStoneDamage = 0;
        _enemyMagicStoneDamage = 0;
    }

    private void SubscribeCombatEvents()
    {
        UnsubscribeCombatEvents();
        CombatDamageEvents.Resolved += OnDamageResolved;
        CombatHealingEvents.Resolved += OnHealingResolved;
        CombatStatusEffectEvents.Changed += OnStatusEffectChanged;
    }

    private void UnsubscribeCombatEvents()
    {
        CombatDamageEvents.Resolved -= OnDamageResolved;
        CombatHealingEvents.Resolved -= OnHealingResolved;
        CombatStatusEffectEvents.Changed -= OnStatusEffectChanged;
    }

    private void AddCharacters(
        IReadOnlyList<Character> characters,
        CombatTeam team,
        List<Character> destination)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || _characterResults.ContainsKey(character)) continue;

            destination.Add(character);
            _characterResults.Add(character, new MutableCharacterResult(character, team));
        }
    }

    private void SubscribeCharacterHealth()
    {
        UnsubscribeCharacterHealth();
        foreach (KeyValuePair<Character, MutableCharacterResult> pair in _characterResults)
        {
            CombatHealth health = pair.Key != null ? pair.Key.Health : null;
            if (health == null) continue;

            health.Defeated += OnCharacterDefeated;
            _subscribedHealth.Add(health);
        }
    }

    private void UnsubscribeCharacterHealth()
    {
        for (int i = 0; i < _subscribedHealth.Count; i++)
        {
            CombatHealth health = _subscribedHealth[i];
            if (health != null)
            {
                health.Defeated -= OnCharacterDefeated;
            }
        }

        _subscribedHealth.Clear();
    }

    private void UnsubscribeMagicStoneEvents()
    {
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.Damaged -= OnMagicStoneDamaged;
        }

        _magicStoneSystem = null;
    }

    private void OnDamageResolved(CombatDamageEvent damage)
    {
        if (!_isRecording || damage.Target == null) return;
        if (!_characterResults.TryGetValue(damage.Target, out MutableCharacterResult targetResult)) return;

        if (damage.WasPrevented)
        {
            targetResult.DamagePrevented += damage.Amount;
            return;
        }

        targetResult.DamageTaken += damage.Amount;
        if (damage.Source.Character != null &&
            _characterResults.TryGetValue(damage.Source.Character, out MutableCharacterResult sourceResult))
        {
            sourceResult.DamageDealt += damage.Amount;
        }
    }

    private void OnHealingResolved(CombatHealingEvent healing)
    {
        if (!_isRecording || healing.Target == null) return;

        if (healing.Source.Character != null &&
            _characterResults.TryGetValue(healing.Source.Character, out MutableCharacterResult sourceResult))
        {
            sourceResult.HealingDone += healing.Amount;
            return;
        }

        if (_characterResults.TryGetValue(healing.Target, out MutableCharacterResult targetResult))
        {
            targetResult.HealingDone += healing.Amount;
        }
    }

    private void OnStatusEffectChanged(CombatStatusEffectChange change)
    {
        if (!_isRecording) return;

        _statusEffectTracker.Record(change, Time.time);
    }

    private void OnCharacterDefeated(Character victim, Character killer)
    {
        if (!_isRecording) return;

        if (victim != null && _characterResults.TryGetValue(victim, out MutableCharacterResult victimResult))
        {
            victimResult.Deaths++;
        }

        if (killer != null && _characterResults.TryGetValue(killer, out MutableCharacterResult killerResult))
        {
            killerResult.Defeats++;
        }
    }

    private void OnMagicStoneDamaged(int featureIndex, int amount, Character attacker)
    {
        if (!_isRecording || amount <= 0 || attacker == null) return;
        if (!_characterResults.TryGetValue(attacker, out MutableCharacterResult attackerResult)) return;

        if (attackerResult.Team == CombatTeam.Ally)
        {
            _allyMagicStoneDamage += amount;
        }
        else
        {
            _enemyMagicStoneDamage += amount;
        }

        attackerResult.MagicStoneDamage += amount;
    }

    private CombatBattleTeamResult BuildTeamResult(
        CombatTeam team,
        List<Character> characters)
    {
        var characterResults = new List<CombatBattleCharacterResult>(characters.Count);
        int aliveCount = 0;
        int damageDealt = 0;
        int damageTaken = 0;
        int healingDone = 0;
        int damagePrevented = 0;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (!_characterResults.TryGetValue(character, out MutableCharacterResult stats)) continue;

            CombatHealth health = character != null ? character.Health : null;
            bool isAlive = health != null && health.IsAlive;
            if (isAlive) aliveCount++;

            damageDealt += stats.DamageDealt;
            damageTaken += stats.DamageTaken;
            healingDone += stats.HealingDone;
            damagePrevented += stats.DamagePrevented;
            characterResults.Add(new CombatBattleCharacterResult(
                character != null ? character.DisplayName : string.Empty,
                PersonalityDisplayName(character),
                WeaponDisplayName(character),
                isAlive,
                stats.DamageDealt,
                stats.MagicStoneDamage,
                stats.DamageTaken,
                stats.HealingDone,
                stats.Defeats,
                stats.Deaths,
                _statusEffectTracker.GetSupportSummary(character),
                _statusEffectTracker.GetEffectResults(character)));
        }

        int stoneHp = 0;
        int stoneMaxHp = 0;
        if (_magicStoneSystem != null)
        {
            FeatureType stoneType = team == CombatTeam.Ally
                ? FeatureType.OwnMainStone
                : FeatureType.EnemyMainStone;
            if (_magicStoneSystem.TryGetState(stoneType, out MagicStoneRuntimeState stoneState) &&
                stoneState != null)
            {
                stoneHp = stoneState.HP;
                stoneMaxHp = stoneState.MaxHP;
            }
        }

        int magicStoneDamage = team == CombatTeam.Ally
            ? _allyMagicStoneDamage
            : _enemyMagicStoneDamage;
        return new CombatBattleTeamResult(
            team,
            characters.Count,
            aliveCount,
            damageDealt,
            damageTaken,
            healingDone,
            damagePrevented,
            magicStoneDamage,
            stoneHp,
            stoneMaxHp,
            characterResults,
            _statusEffectTracker.GetTeamSupportSummary(team));
    }

    private static string WeaponDisplayName(Character character)
    {
        if (character == null ||
            character.EquippedWeapon == null ||
            character.EquippedWeapon.Kind == WeaponKind.Unarmed)
        {
            return "なし";
        }

        return CombatAiDebugLabels.WeaponShort(character.EquippedWeapon);
    }

    private static string PersonalityDisplayName(Character character)
    {
        return character != null
            ? CombatAiDebugLabels.PersonalityShort(character.PersonalityProfile)
            : "なし";
    }
}
