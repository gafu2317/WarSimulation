using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatBattleStatusEffectTracker
{
    private readonly Dictionary<Character, CombatTeam> _participants =
        new Dictionary<Character, CombatTeam>();
    private readonly Dictionary<Character, MutableSupportSummary> _characterSummaries =
        new Dictionary<Character, MutableSupportSummary>();
    private readonly Dictionary<CombatTeam, MutableSupportSummary> _teamSummaries =
        new Dictionary<CombatTeam, MutableSupportSummary>();
    private readonly Dictionary<SourceStatKey, MutableStatusEffectResult> _effectResults =
        new Dictionary<SourceStatKey, MutableStatusEffectResult>();
    private readonly Dictionary<ActiveEffectKey, ActiveEffect> _activeEffects =
        new Dictionary<ActiveEffectKey, ActiveEffect>();
    private readonly List<ActiveEffectKey> _activeEffectKeys = new List<ActiveEffectKey>();

    private float _startTime;
    private bool _isTracking;

    public void Begin(
        IReadOnlyList<Character> allies,
        IReadOnlyList<Character> enemies,
        float startTime)
    {
        Clear();
        _startTime = startTime;
        AddParticipants(allies, CombatTeam.Ally);
        AddParticipants(enemies, CombatTeam.Enemy);
        _teamSummaries[CombatTeam.Ally] = new MutableSupportSummary();
        _teamSummaries[CombatTeam.Enemy] = new MutableSupportSummary();
        _isTracking = true;
    }

    public void Record(CombatStatusEffectChange change, float time)
    {
        if (!_isTracking || change.Target == null ||
            !_participants.ContainsKey(change.Target))
        {
            return;
        }

        ActiveEffectKey activeKey = new ActiveEffectKey(change.Target, change.Key);
        switch (change.Kind)
        {
            case CombatStatusEffectChangeKind.Applied:
            case CombatStatusEffectChangeKind.Refreshed:
                CloseActiveEffect(activeKey, time);
                if (!TryResolveStatEffect(change, out bool isBuff, out Character source)) return;

                MutableStatusEffectResult effectResult = GetEffectResult(source, change.Stat, isBuff);
                MutableSupportSummary characterSummary = GetCharacterSummary(source);
                MutableSupportSummary teamSummary = _teamSummaries[_participants[source]];
                effectResult.RecordActivation(change.Target);
                characterSummary.RecordActivation(isBuff, change.Target);
                teamSummary.RecordActivation(isBuff, change.Target);
                float startedAt = NormalizeTime(time);
                float expiresAt = startedAt + Mathf.Max(0f, change.RemainingSeconds);
                _activeEffects[activeKey] = new ActiveEffect(
                    startedAt,
                    expiresAt,
                    effectResult,
                    characterSummary,
                    teamSummary,
                    isBuff);
                return;

            case CombatStatusEffectChangeKind.Removed:
            case CombatStatusEffectChangeKind.Expired:
                CloseActiveEffect(activeKey, time);
                return;

            default:
                return;
        }
    }

    public void Complete(float endTime)
    {
        if (!_isTracking) return;

        _activeEffectKeys.Clear();
        foreach (ActiveEffectKey key in _activeEffects.Keys)
        {
            _activeEffectKeys.Add(key);
        }

        for (int i = 0; i < _activeEffectKeys.Count; i++)
        {
            CloseActiveEffect(_activeEffectKeys[i], endTime);
        }

        _activeEffectKeys.Clear();
        _isTracking = false;
    }

    public CombatBattleSupportSummary GetSupportSummary(Character source)
    {
        return source != null && _characterSummaries.TryGetValue(source, out MutableSupportSummary summary)
            ? summary.ToResult()
            : CombatBattleSupportSummary.Empty;
    }

    public IReadOnlyList<CombatBattleStatusEffectResult> GetEffectResults(Character source)
    {
        if (source == null) return Array.Empty<CombatBattleStatusEffectResult>();

        var results = new List<CombatBattleStatusEffectResult>();
        foreach (KeyValuePair<SourceStatKey, MutableStatusEffectResult> pair in _effectResults)
        {
            if (!pair.Key.Source.Equals(source)) continue;

            MutableStatusEffectResult result = pair.Value;
            results.Add(result.ToResult());
        }

        results.Sort(CompareEffectResults);
        return results;
    }

    public CombatBattleSupportSummary GetTeamSupportSummary(CombatTeam team)
    {
        return _teamSummaries.TryGetValue(team, out MutableSupportSummary summary)
            ? summary.ToResult()
            : CombatBattleSupportSummary.Empty;
    }

    public void Clear()
    {
        _participants.Clear();
        _characterSummaries.Clear();
        _teamSummaries.Clear();
        _effectResults.Clear();
        _activeEffects.Clear();
        _activeEffectKeys.Clear();
        _startTime = 0f;
        _isTracking = false;
    }

    private void AddParticipants(IReadOnlyList<Character> characters, CombatTeam team)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null || _participants.ContainsKey(character)) continue;

            _participants.Add(character, team);
        }
    }

    private bool TryResolveStatEffect(
        CombatStatusEffectChange change,
        out bool isBuff,
        out Character source)
    {
        isBuff = false;
        source = change.Source.Character;
        if (change.Type != CombatStatusEffects.EffectType.StatModifier ||
            source == null || !_participants.ContainsKey(source))
        {
            return false;
        }

        if (change.Multiplier > 1f)
        {
            isBuff = true;
            return true;
        }

        return change.Multiplier < 1f;
    }

    private MutableStatusEffectResult GetEffectResult(
        Character source,
        CombatStatusEffects.StatKind stat,
        bool isBuff)
    {
        var key = new SourceStatKey(source, stat, isBuff);
        if (_effectResults.TryGetValue(key, out MutableStatusEffectResult result)) return result;

        result = new MutableStatusEffectResult(stat, isBuff);
        _effectResults.Add(key, result);
        return result;
    }

    private MutableSupportSummary GetCharacterSummary(Character source)
    {
        if (_characterSummaries.TryGetValue(source, out MutableSupportSummary summary)) return summary;

        summary = new MutableSupportSummary();
        _characterSummaries.Add(source, summary);
        return summary;
    }

    private void CloseActiveEffect(ActiveEffectKey key, float time)
    {
        if (!_activeEffects.TryGetValue(key, out ActiveEffect activeEffect)) return;

        float endTime = Mathf.Min(NormalizeTime(time), activeEffect.ExpiresAt);
        float duration = Mathf.Max(0f, endTime - activeEffect.StartedAt);
        activeEffect.EffectResult.AddDuration(duration);
        activeEffect.CharacterSummary.AddDuration(activeEffect.IsBuff, duration);
        activeEffect.TeamSummary.AddDuration(activeEffect.IsBuff, duration);
        _activeEffects.Remove(key);
    }

    private float NormalizeTime(float time)
    {
        return Mathf.Max(_startTime, time);
    }

    private static int CompareEffectResults(
        CombatBattleStatusEffectResult left,
        CombatBattleStatusEffectResult right)
    {
        if (left.IsBuff != right.IsBuff) return left.IsBuff ? -1 : 1;
        return left.Stat.CompareTo(right.Stat);
    }

    private readonly struct ActiveEffectKey : IEquatable<ActiveEffectKey>
    {
        public ActiveEffectKey(Character target, string key)
        {
            Target = target;
            Key = key;
        }

        public Character Target { get; }
        public string Key { get; }

        public bool Equals(ActiveEffectKey other)
        {
            return EqualityComparer<Character>.Default.Equals(Target, other.Target) && Key == other.Key;
        }

        public override bool Equals(object obj)
        {
            return obj is ActiveEffectKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<Character>.Default.GetHashCode(Target) * 397) ^ (Key != null ? Key.GetHashCode() : 0);
            }
        }
    }

    private readonly struct SourceStatKey : IEquatable<SourceStatKey>
    {
        public SourceStatKey(Character source, CombatStatusEffects.StatKind stat, bool isBuff)
        {
            Source = source;
            Stat = stat;
            IsBuff = isBuff;
        }

        public Character Source { get; }
        public CombatStatusEffects.StatKind Stat { get; }
        public bool IsBuff { get; }

        public bool Equals(SourceStatKey other)
        {
            return EqualityComparer<Character>.Default.Equals(Source, other.Source) &&
                   Stat == other.Stat &&
                   IsBuff == other.IsBuff;
        }

        public override bool Equals(object obj)
        {
            return obj is SourceStatKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EqualityComparer<Character>.Default.GetHashCode(Source);
                hash = (hash * 397) ^ (int)Stat;
                return (hash * 397) ^ IsBuff.GetHashCode();
            }
        }
    }

    private readonly struct ActiveEffect
    {
        public ActiveEffect(
            float startedAt,
            float expiresAt,
            MutableStatusEffectResult effectResult,
            MutableSupportSummary characterSummary,
            MutableSupportSummary teamSummary,
            bool isBuff)
        {
            StartedAt = startedAt;
            ExpiresAt = expiresAt;
            EffectResult = effectResult;
            CharacterSummary = characterSummary;
            TeamSummary = teamSummary;
            IsBuff = isBuff;
        }

        public float StartedAt { get; }
        public float ExpiresAt { get; }
        public MutableStatusEffectResult EffectResult { get; }
        public MutableSupportSummary CharacterSummary { get; }
        public MutableSupportSummary TeamSummary { get; }
        public bool IsBuff { get; }
    }

    private sealed class MutableStatusEffectResult
    {
        public MutableStatusEffectResult(CombatStatusEffects.StatKind stat, bool isBuff)
        {
            Stat = stat;
            IsBuff = isBuff;
        }

        public CombatStatusEffects.StatKind Stat { get; }
        public bool IsBuff { get; }
        public int ActivationCount { get; private set; }
        public float DurationSeconds { get; private set; }
        public HashSet<Character> Targets { get; } = new HashSet<Character>();

        public void RecordActivation(Character target)
        {
            ActivationCount++;
            Targets.Add(target);
        }

        public void AddDuration(float duration)
        {
            DurationSeconds += duration;
        }

        public CombatBattleStatusEffectResult ToResult()
        {
            return new CombatBattleStatusEffectResult(
                Stat,
                IsBuff,
                ActivationCount,
                Targets.Count,
                DurationSeconds);
        }
    }

    private sealed class MutableSupportSummary
    {
        private readonly HashSet<Character> _buffTargets = new HashSet<Character>();
        private readonly HashSet<Character> _debuffTargets = new HashSet<Character>();

        public int BuffActivationCount { get; private set; }
        public int DebuffActivationCount { get; private set; }
        public float BuffDurationSeconds { get; private set; }
        public float DebuffDurationSeconds { get; private set; }

        public void RecordActivation(bool isBuff, Character target)
        {
            if (isBuff)
            {
                BuffActivationCount++;
                _buffTargets.Add(target);
                return;
            }

            DebuffActivationCount++;
            _debuffTargets.Add(target);
        }

        public void AddDuration(bool isBuff, float duration)
        {
            if (isBuff)
            {
                BuffDurationSeconds += duration;
                return;
            }

            DebuffDurationSeconds += duration;
        }

        public CombatBattleSupportSummary ToResult()
        {
            return new CombatBattleSupportSummary(
                BuffActivationCount,
                _buffTargets.Count,
                BuffDurationSeconds,
                DebuffActivationCount,
                _debuffTargets.Count,
                DebuffDurationSeconds);
        }
    }
}
