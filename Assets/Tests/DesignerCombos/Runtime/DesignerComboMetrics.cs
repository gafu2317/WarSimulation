using System;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

[Serializable]
public sealed class DesignerComboEventRecord
{
    public float Time;
    public string Type;
    public string Actor;
    public string Target;
    public float Value;
}

[Serializable]
public sealed class DesignerComboMatchResult
{
    public string Combo;
    public string Variant;
    public string Terrain;
    public int Seed;
    public bool SidesSwapped;
    public string Outcome;
    public float BattleSeconds;
    public float PrimaryMetric;
    public float PrimaryMetricPerSecond;
    public string PrimaryMetricName;
    public bool IsLinkedVariant;
    public bool ComboOccurred;
    public int MagicStoneDamage;
    public int DamageDealt;
    public int EffectiveHealing;
    public int EffectiveDefense;
    public int TargetChanges;
    public float LinkedSeconds;
    public float BindSeconds;
    public float PoisonSeconds;
    public float BuffSeconds;
    public float ScenarioSeconds;
    public int BoundFollowUpDamage;
    public int BoundDefeats;
    public int PoisonDamage;
    public int MaximumSimultaneousHits;
    public float AverageSimultaneousHits;
    public int AreaHitEvents;
    public int CoreEffectiveHealing;
    public int TrackedTargetDefeats;
    public int DivertedEnemiesMaximum;
    public int MaximumEnemiesTargetingCore;
    public float RelationshipBuffSeconds;
    public float DistinctTargetRatio;
    public float PoisonedEnemyReachSeconds = -1f;
    public float FirstMagicStoneAttackAt = -1f;
    public float AssaulterSurvivalAfterFirstStoneAttack;
    public float ComboBrokenAt = -1f;
    public string ComboBrokenReason;
    public float PrimaryMetricAtBreak;
    public float PrimaryMetricAfterBreak;
    public float MetricRateBeforeBreak;
    public float MetricRateAfterBreak;
    public string SurvivalTimes;
    public List<DesignerComboEventRecord> Events = new();
    public string Error;
}

public sealed class DesignerComboMetricsCollector : IDisposable
{
    private readonly DesignerComboScenarioDefinition _scenario;
    private readonly IReadOnlyList<Character> _comboMembers;
    private readonly IReadOnlyList<Character> _opponents;
    private readonly CombatTeam _comboTeam;
    private readonly Dictionary<Character, string> _lastDecisionTargets = new();
    private readonly HashSet<Character> _comboBoundTargets = new();
    private readonly HashSet<Character> _comboPoisonedTargets = new();
    private readonly Dictionary<string, bool> _statusStates = new();
    private readonly Dictionary<Character, float> _poisonAppliedAt = new();
    private readonly List<CombatHealth> _subscribedHealth = new();
    private CombatMagicStoneSystem _stoneSystem;
    private CombatCharacterSystem _characterSystem;
    private float _startedAt;
    private float _lastSampleAt;
    private float _separatedSince = -1f;
    private int _totalSimultaneousHits;
    private float _distinctTargetSeconds;
    private float _overlappingTargetSeconds;

    public DesignerComboMatchResult Result { get; }

    public DesignerComboMetricsCollector(
        DesignerComboScenarioDefinition scenario,
        DesignerComboVariantKind variant,
        DesignerComboTerrainKind terrain,
        int seed,
        bool sidesSwapped,
        IReadOnlyList<Character> comboMembers,
        IReadOnlyList<Character> opponents,
        CombatTeam comboTeam)
    {
        _scenario = scenario;
        _comboMembers = comboMembers;
        _opponents = opponents;
        _comboTeam = comboTeam;
        Result = new DesignerComboMatchResult
        {
            Combo = scenario.DisplayName,
            Variant = variant.ToString(),
            Terrain = terrain.ToString(),
            Seed = seed,
            SidesSwapped = sidesSwapped,
            PrimaryMetricName = scenario.PrimaryMetricName,
            IsLinkedVariant = variant == DesignerComboVariantKind.Linked,
        };
    }

    public void Begin()
    {
        _startedAt = Time.time;
        _lastSampleAt = _startedAt;
        Character[] participants = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        for (int i = 0; i < participants.Length; i++)
        {
            Character character = participants[i];
            CombatHealth health = character != null ? character.Health : null;
            if (health == null) continue;
            health.Defeated += OnDefeated;
            _subscribedHealth.Add(health);
        }

        CombatDamageEvents.Resolved += OnDamageResolved;
        CombatHealingEvents.Resolved += OnHealingResolved;
        CombatStatusEffectEvents.Changed += OnStatusChanged;
        CombatSkillActionEvents.Completed += OnSkillCompleted;
        CombatAiDecisionEvents.PlanSelected += OnPlanSelected;

        _stoneSystem = CombatMagicStoneSystemResolver.Resolve();
        _characterSystem = CombatSceneContext.Instance != null ? CombatSceneContext.Instance.CharacterSystem : UnityEngine.Object.FindAnyObjectByType<CombatCharacterSystem>();
        if (_stoneSystem != null) _stoneSystem.Damaged += OnStoneDamaged;
    }

    public void Sample()
    {
        float now = Time.time;
        float delta = Mathf.Max(0f, now - _lastSampleAt);
        _lastSampleAt = now;
        Result.BattleSeconds = Mathf.Max(0f, now - _startedAt);

        SampleStatuses(delta);
        SampleLinks(delta);
        SampleScenarioMetric(delta);
        SamplePoisonArrival();
        DetectBrokenCombo(now);
    }

    public DesignerComboMatchResult Complete(CombatBattleState state, bool timedOut)
    {
        Sample();
        Result.Outcome = timedOut ? "時間切れ" : IsComboVictory(state) ? "勝利" : "敗北";
        Result.AverageSimultaneousHits = Result.AreaHitEvents > 0 ? _totalSimultaneousHits / (float)Result.AreaHitEvents : 0f;
        float targetAssignmentSeconds = _distinctTargetSeconds + _overlappingTargetSeconds;
        Result.DistinctTargetRatio = targetAssignmentSeconds > 0f ? _distinctTargetSeconds / targetAssignmentSeconds : 0f;
        Result.PrimaryMetric = ResolvePrimaryMetric();
        Result.PrimaryMetricPerSecond = Result.PrimaryMetric / Mathf.Max(0.01f, Result.BattleSeconds);
        Result.SurvivalTimes = BuildSurvivalTimes();
        if (Result.FirstMagicStoneAttackAt >= 0f && _comboMembers.Count > 0)
        {
            Result.AssaulterSurvivalAfterFirstStoneAttack = Mathf.Max(0f, FindDefeatTime(_comboMembers[_scenario.Kind == DesignerComboKind.DiversionMagicStoneAssault ? 1 : 0]) - Result.FirstMagicStoneAttackAt);
        }
        if (Result.ComboBrokenAt >= 0f)
        {
            Result.PrimaryMetricAfterBreak = Mathf.Max(0f, Result.PrimaryMetric - Result.PrimaryMetricAtBreak);
            Result.MetricRateBeforeBreak = Result.PrimaryMetricAtBreak / Mathf.Max(0.01f, Result.ComboBrokenAt);
            Result.MetricRateAfterBreak = Result.PrimaryMetricAfterBreak / Mathf.Max(0.01f, Result.BattleSeconds - Result.ComboBrokenAt);
        }
        Record("戦闘終了", null, null, Result.PrimaryMetric);
        return Result;
    }

    public void Dispose()
    {
        for (int i = 0; i < _subscribedHealth.Count; i++)
        {
            CombatHealth health = _subscribedHealth[i];
            if (health == null) continue;
            health.Defeated -= OnDefeated;
        }

        CombatDamageEvents.Resolved -= OnDamageResolved;
        CombatHealingEvents.Resolved -= OnHealingResolved;
        CombatStatusEffectEvents.Changed -= OnStatusChanged;
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatAiDecisionEvents.PlanSelected -= OnPlanSelected;
        if (_stoneSystem != null) _stoneSystem.Damaged -= OnStoneDamaged;
        _subscribedHealth.Clear();
    }

    private void OnDamageResolved(CombatDamageEvent damage)
    {
        if (damage.WasPrevented)
        {
            if (Contains(_comboMembers, damage.PreventionSource.Character))
            {
                Result.EffectiveDefense += damage.Amount;
            }
            return;
        }
        Character victim = damage.Target;
        Character attacker = damage.Source.Character;
        int amount = damage.Amount;
        if (amount <= 0) return;
        if (attacker != null && Contains(_comboMembers, attacker))
        {
            Result.DamageDealt += amount;
            if (_scenario.Kind == DesignerComboKind.BindFollowUp &&
                _comboMembers.Count > 1 &&
                attacker == _comboMembers[1] &&
                IsBoundComboTarget(victim))
            {
                Result.BoundFollowUpDamage += amount;
                MarkComboOccurred(attacker, victim, amount);
            }

            if (damage.Source.SkillId == SkillId.Grimoire_Poison) Result.PoisonDamage += amount;
            if (_scenario.Kind == DesignerComboKind.PoisonFortress &&
                _comboMembers.Count > 1 &&
                attacker == _comboMembers[0] &&
                damage.Source.SkillId == SkillId.Grimoire_Poison &&
                IsActive(_comboMembers[1]) &&
                ResolveTarget(victim) == _comboMembers[1])
            {
                MarkComboOccurred(attacker, victim, amount);
            }
        }
    }

    private void OnDefeated(Character victim, Character killer)
    {
        if (victim == null) return;
        Record("撃破", killer, victim, 0f);
        if (_scenario.Kind == DesignerComboKind.BindFollowUp &&
            _comboMembers.Count > 1 &&
            killer == _comboMembers[1] &&
            IsBoundComboTarget(victim))
        {
            Result.BoundDefeats++;
        }
        if (_comboMembers.Count > 0 && killer == _comboMembers[0] &&
            (_scenario.Kind != DesignerComboKind.RemoteSupportLoneWolf || _comboMembers.Count > 1 && HorizontalDistance(_comboMembers[0], _comboMembers[1]) > _scenario.LinkDistance))
        {
            Result.TrackedTargetDefeats++;
        }
    }

    private void OnStoneDamaged(int featureIndex, int amount, Character attacker)
    {
        if (amount <= 0 || attacker == null || !IsPrimaryStoneAttacker(attacker) || _stoneSystem == null) return;
        if (!_stoneSystem.TryGetState(featureIndex, out MagicStoneRuntimeState state)) return;
        bool damagesEnemyStone = _comboTeam == CombatTeam.Ally
            ? state.Type == FeatureType.EnemyMainStone || state.Type == FeatureType.EnemySubStone
            : state.Type == FeatureType.OwnMainStone || state.Type == FeatureType.OwnSubStone;
        if (damagesEnemyStone)
        {
            if (_scenario.Kind == DesignerComboKind.DiversionMagicStoneAssault && CountDivertedEnemies(_comboMembers[0]) == 0) return;
            if (Result.FirstMagicStoneAttackAt < 0f) Result.FirstMagicStoneAttackAt = Mathf.Max(0f, Time.time - _startedAt);
            Result.MagicStoneDamage += amount;
            Record("魔石攻撃", attacker, null, amount);
            if (_scenario.Kind == DesignerComboKind.DiversionMagicStoneAssault ||
                (_scenario.Kind == DesignerComboKind.MagicStoneAssault &&
                 _comboMembers.Count > 1 &&
                 IsActive(_comboMembers[1]) &&
                 HorizontalDistance(attacker, _comboMembers[1]) <= _scenario.LinkDistance))
            {
                MarkComboOccurred(attacker, null, amount);
            }
        }
    }

    private void OnHealingResolved(CombatHealingEvent healing)
    {
        Character target = healing.Target;
        int amount = healing.Amount;
        if (!Contains(_comboMembers, healing.Source.Character)) return;
        if (amount <= 0 || !Contains(_comboMembers, target)) return;
        Result.EffectiveHealing += amount;
        if (_comboMembers.Count > 0 && target == _comboMembers[0]) Result.CoreEffectiveHealing += amount;
        if (_scenario.Kind == DesignerComboKind.DecoySustain &&
            _comboMembers.Count > 1 &&
            healing.Source.Character == _comboMembers[1] &&
            target == _comboMembers[0] &&
            CountTargeting(target, _opponents) >= 2)
        {
            MarkComboOccurred(healing.Source.Character, target, amount);
        }
        if (_scenario.Kind == DesignerComboKind.RemoteSupportLoneWolf &&
            _comboMembers.Count > 1 &&
            healing.Source.Character == _comboMembers[1] &&
            target == _comboMembers[0] &&
            HorizontalDistance(_comboMembers[0], _comboMembers[1]) > _scenario.LinkDistance)
        {
            MarkComboOccurred(healing.Source.Character, target, amount);
        }
    }

    private void OnStatusChanged(CombatStatusEffectChange change)
    {
        Character target = change.Target;
        Character source = change.Source.Character;
        CombatStatusEffects.EffectType type = change.Type;
        if (target == null) return;
        bool started = change.Kind == CombatStatusEffectChangeKind.Applied ||
            change.Kind == CombatStatusEffectChangeKind.Refreshed;
        bool ended = change.Kind == CombatStatusEffectChangeKind.Removed ||
            change.Kind == CombatStatusEffectChangeKind.Expired;
        if (type == CombatStatusEffects.EffectType.Bind &&
            _scenario.Kind == DesignerComboKind.BindFollowUp && _comboMembers.Count > 0)
        {
            if (started && source == _comboMembers[0]) _comboBoundTargets.Add(target);
            else if (started || ended) _comboBoundTargets.Remove(target);
        }
        if (source == null || !Contains(_comboMembers, source)) return;
        if (started && type == CombatStatusEffects.EffectType.Poison)
        {
            _comboPoisonedTargets.Add(target);
            _poisonAppliedAt[target] = Mathf.Max(0f, Time.time - _startedAt);
        }
        if (type != CombatStatusEffects.EffectType.Bind && type != CombatStatusEffects.EffectType.Poison) return;
        string key = StatusKey(target, type);
        if (started)
        {
            if (!_statusStates.TryGetValue(key, out bool active) || !active) Record("状態開始:" + type, source, target, 0f);
            _statusStates[key] = true;
        }
        else if (ended && _statusStates.TryGetValue(key, out bool active) && active)
        {
            _statusStates[key] = false;
            Record("状態終了:" + type, source, target, 0f);
        }
    }

    private void OnSkillCompleted(CombatSkillActionResult result)
    {
        Character actor = result?.Action.Actor;
        if (actor == null || !Contains(_comboMembers, actor)) return;

        if (_scenario.Kind != DesignerComboKind.DecoyBombardment ||
            _comboMembers.Count <= 1 ||
            actor != _comboMembers[1] ||
            result.Action.Skill == null ||
            result.Action.Skill.AreaRadius <= 0f) return;

        var hitTargets = new HashSet<Character>();
        for (int i = 0; i < result.Effects.Count; i++)
        {
            CombatActionEffect effect = result.Effects[i];
            if (effect.Kind == CombatActionEffectKind.Damage && effect.Target != null)
            {
                hitTargets.Add(effect.Target);
            }
        }

        if (hitTargets.Count == 0) return;
        Result.AreaHitEvents++;
        _totalSimultaneousHits += hitTargets.Count;
        Result.MaximumSimultaneousHits = Mathf.Max(Result.MaximumSimultaneousHits, hitTargets.Count);
        if (hitTargets.Count >= 2 && CountTargeting(_comboMembers[0], _opponents) >= 2)
        {
            MarkComboOccurred(actor, _comboMembers[0], hitTargets.Count);
        }
    }

    private void OnPlanSelected(Character actor, CombatAiPlan previous, CombatAiPlan next)
    {
        if (actor == null || !Contains(_comboMembers, actor)) return;

        string targetKey = ResolveDecisionTargetKey(next);
        if (string.IsNullOrEmpty(targetKey)) targetKey = "対象なし";

        if (_lastDecisionTargets.TryGetValue(actor, out string previousKey) && previousKey != targetKey)
        {
            Result.TargetChanges++;
            RecordTargetChange(actor, targetKey);
        }
        _lastDecisionTargets[actor] = targetKey;
    }

    private void SampleStatuses(float delta)
    {
        for (int i = 0; i < _opponents.Count; i++)
        {
            CombatStatusEffects effects = _opponents[i] != null ? _opponents[i].StatusEffects : null;
            if (effects == null) continue;
            if (_comboBoundTargets.Contains(_opponents[i]) && effects.HasActiveEffectImmediate(CombatStatusEffects.EffectType.Bind)) Result.BindSeconds += delta;
            if (_comboPoisonedTargets.Contains(_opponents[i]) && effects.HasActiveEffectImmediate(CombatStatusEffects.EffectType.Poison)) Result.PoisonSeconds += delta;
        }

        for (int i = 0; i < _comboMembers.Count; i++)
        {
            CombatStatusEffects effects = _comboMembers[i] != null ? _comboMembers[i].StatusEffects : null;
            if (effects == null) continue;
            IReadOnlyList<CombatStatusEffectSnapshot> snapshots = effects.GetActiveEffectSnapshots();
            bool hasRelationshipBuff = false;
            bool hasBuff = false;
            for (int j = 0; j < snapshots.Count; j++)
            {
                if (snapshots[j].Key.StartsWith("PersonalityRelationship_", StringComparison.Ordinal)) hasRelationshipBuff = true;
                if (snapshots[j].IsBuff) hasBuff = true;
            }
            if (hasBuff || hasRelationshipBuff) Result.BuffSeconds += delta;
            if (hasRelationshipBuff) Result.RelationshipBuffSeconds += delta;
        }

        if ((_scenario.Kind == DesignerComboKind.LoversFollowUnit ||
             _scenario.Kind == DesignerComboKind.OppositeGenderEscort) &&
            Result.RelationshipBuffSeconds >= 1f)
        {
            MarkComboOccurred(null, null, Result.RelationshipBuffSeconds);
        }
    }

    private void SampleLinks(float delta)
    {
        if (_comboMembers.Count < 2 || !AllActive(_comboMembers)) return;
        if (MaximumPairDistance(_comboMembers) <= _scenario.LinkDistance) Result.LinkedSeconds += delta;
    }

    private void SampleScenarioMetric(float delta)
    {
        if (_comboMembers.Count > 0)
        {
            Result.MaximumEnemiesTargetingCore = Mathf.Max(Result.MaximumEnemiesTargetingCore, CountTargeting(_comboMembers[0], _opponents));
        }
        if (_scenario.Kind == DesignerComboKind.DecoySustain && _comboMembers.Count > 0 && CountTargeting(_comboMembers[0], _opponents) >= 2)
        {
            Result.ScenarioSeconds += delta;
        }

        if (_scenario.Kind == DesignerComboKind.DistributedHunt && HaveDistinctTargets(_comboMembers))
        {
            Result.ScenarioSeconds += delta;
            _distinctTargetSeconds += delta;
            if (Result.ScenarioSeconds >= 1f) MarkComboOccurred(null, null, Result.ScenarioSeconds);
        }
        else if (_scenario.Kind == DesignerComboKind.DistributedHunt && HaveMultipleSwordTargets(_comboMembers))
        {
            _overlappingTargetSeconds += delta;
        }

        if (_scenario.Kind == DesignerComboKind.RemoteSupportLoneWolf && _comboMembers.Count > 1 && CountTargeting(_comboMembers[0], _opponents) > 0)
        {
            if (HorizontalDistance(_comboMembers[0], _comboMembers[1]) > _scenario.LinkDistance) Result.ScenarioSeconds += delta;
        }

        if (_scenario.Kind == DesignerComboKind.FrontlineBreakthrough && _comboMembers.Count > 1 &&
            IsActive(_comboMembers[1]) &&
            ResolveObjective(_comboMembers[0]) == CombatObjective.AttackEnemy &&
            HorizontalDistance(_comboMembers[0], _comboMembers[1]) <= _scenario.LinkDistance)
        {
            Result.ScenarioSeconds += delta;
            if (Result.ScenarioSeconds >= 1f) MarkComboOccurred(_comboMembers[0], _comboMembers[1], Result.ScenarioSeconds);
        }

        if (_scenario.Kind == DesignerComboKind.DiversionMagicStoneAssault && _comboMembers.Count > 0)
        {
            Result.DivertedEnemiesMaximum = Mathf.Max(Result.DivertedEnemiesMaximum, CountDivertedEnemies(_comboMembers[0]));
        }
    }

    private void SamplePoisonArrival()
    {
        if (_scenario.Kind != DesignerComboKind.PoisonFortress || Result.PoisonedEnemyReachSeconds >= 0f || _comboMembers.Count == 0) return;
        Character grimoire = _comboMembers[0];
        foreach (KeyValuePair<Character, float> pair in _poisonAppliedAt)
        {
            Character target = pair.Key;
            if (!IsActive(target) || grimoire == null) continue;
            Vector3 a = target.transform.position;
            Vector3 b = grimoire.transform.position;
            a.y = 0f;
            b.y = 0f;
            if (Vector3.Distance(a, b) <= 3f)
            {
                Result.PoisonedEnemyReachSeconds = Mathf.Max(0f, Time.time - _startedAt - pair.Value);
                return;
            }
        }
    }

    private void DetectBrokenCombo(float now)
    {
        if (Result.ComboBrokenAt >= 0f) return;
        int requiredMembers = Mathf.Min(_scenario.Roles.Length, _comboMembers.Count);
        for (int i = 0; i < requiredMembers; i++)
        {
            if (IsActive(_comboMembers[i])) continue;
            Result.ComboBrokenAt = Mathf.Max(0f, now - _startedAt);
            Result.ComboBrokenReason = _comboMembers[i] != null ? _comboMembers[i].name + "が離脱" : "構成員が不在";
            Result.PrimaryMetricAtBreak = ResolvePrimaryMetric();
            Record("連携崩壊", _comboMembers[i], null, 0f);
            return;
        }

        bool separated = IsDistanceBreakConditionMet();
        if (!separated)
        {
            _separatedSince = -1f;
            return;
        }

        if (_separatedSince < 0f) _separatedSince = now;
        if (now - _separatedSince < 2f) return;
        Result.ComboBrokenAt = Mathf.Max(0f, _separatedSince - _startedAt);
        Result.ComboBrokenReason = "連携距離を超過";
        Result.PrimaryMetricAtBreak = ResolvePrimaryMetric();
        Record("連携崩壊", null, null, 0f);
    }

    private string BuildSurvivalTimes()
    {
        var builder = new System.Text.StringBuilder();
        AppendSurvivalTimes(builder, _comboMembers, "連携");
        AppendSurvivalTimes(builder, _opponents, "対戦相手");
        return builder.ToString();
    }

    private void AppendSurvivalTimes(System.Text.StringBuilder builder, IReadOnlyList<Character> characters, string group)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (builder.Length > 0) builder.Append(';');
            Character character = characters[i];
            float seconds = character != null && character.Health != null && character.Health.IsAlive
                ? Result.BattleSeconds
                : FindDefeatTime(character);
            builder.Append(group).Append(':').Append(character != null ? character.name : "不明").Append('=').Append(seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private float FindDefeatTime(Character character)
    {
        string targetName = character != null ? character.name : string.Empty;
        for (int i = 0; i < Result.Events.Count; i++)
        {
            DesignerComboEventRecord record = Result.Events[i];
            if (record.Type == "撃破" && record.Target == targetName) return record.Time;
        }
        return Result.BattleSeconds;
    }

    private void Record(string type, Character actor, Character target, float value)
    {
        Result.Events.Add(new DesignerComboEventRecord
        {
            Time = Mathf.Max(0f, Time.time - _startedAt),
            Type = type,
            Actor = actor != null ? actor.name : string.Empty,
            Target = target != null ? target.name : string.Empty,
            Value = value,
        });
    }

    private void RecordTargetChange(Character actor, string target)
    {
        Result.Events.Add(new DesignerComboEventRecord
        {
            Time = Mathf.Max(0f, Time.time - _startedAt),
            Type = "対象変更",
            Actor = actor != null ? actor.name : string.Empty,
            Target = target,
        });
    }

    private void MarkComboOccurred(Character actor, Character target, float value)
    {
        if (Result.ComboOccurred) return;
        Result.ComboOccurred = true;
        Record("連携成立", actor, target, value);
    }

    private bool IsBoundComboTarget(Character target)
    {
        return target != null &&
            _comboBoundTargets.Contains(target) &&
            target.StatusEffects != null &&
            target.StatusEffects.TryGetActiveEffectSourceImmediate(
                CombatStatusEffects.EffectType.Bind,
                out CombatEffectSource source) &&
            _comboMembers.Count > 0 && source.Character == _comboMembers[0];
    }

    private static string ResolveDecisionTargetKey(CombatAiPlan plan)
    {
        Character target = plan.SkillTarget != null
            ? plan.SkillTarget
            : plan.MoveTarget.TargetCharacter;
        if (target != null) return "キャラクター:" + target.BattleParticipantId + ":" + target.name;
        if (plan.Objective == CombatObjective.DestroyEnemyStone) return "敵魔石";
        if (plan.Objective == CombatObjective.DefendOwnStone) return "自軍魔石";
        return string.Empty;
    }

    private bool IsDistanceBreakConditionMet()
    {
        float limit = _scenario.LinkDistance * 2f;
        return _scenario.Kind switch
        {
            DesignerComboKind.BindFollowUp => RoleDistanceExceeds(0, 1, limit),
            DesignerComboKind.PoisonFortress => RoleDistanceExceeds(0, 1, limit),
            DesignerComboKind.MagicStoneAssault => RoleDistanceExceeds(0, 1, limit),
            DesignerComboKind.FrontlineBreakthrough => RoleDistanceExceeds(0, 1, limit),
            DesignerComboKind.DecoySustain => RoleDistanceExceeds(0, 1, limit),
            DesignerComboKind.LoversFollowUnit => RoleDistanceExceeds(2, 0, limit) || RoleDistanceExceeds(2, 1, limit),
            DesignerComboKind.OppositeGenderEscort => RoleDistanceExceeds(0, 1, limit),
            _ => false,
        };
    }

    private bool RoleDistanceExceeds(int firstRole, int secondRole, float limit)
    {
        return firstRole >= _comboMembers.Count || secondRole >= _comboMembers.Count ||
            HorizontalDistance(_comboMembers[firstRole], _comboMembers[secondRole]) > limit;
    }

    private static string StatusKey(Character target, CombatStatusEffects.EffectType type)
    {
        return (target != null ? target.name : string.Empty) + ":" + type;
    }

    private float ResolvePrimaryMetric()
    {
        return _scenario.Kind switch
        {
            DesignerComboKind.BindFollowUp => Result.BoundFollowUpDamage,
            DesignerComboKind.PoisonFortress => Result.PoisonDamage,
            DesignerComboKind.MagicStoneAssault => Result.MagicStoneDamage,
            DesignerComboKind.DecoyBombardment => Result.AverageSimultaneousHits,
            DesignerComboKind.DiversionMagicStoneAssault => Result.MagicStoneDamage,
            DesignerComboKind.LoversFollowUnit => Result.RelationshipBuffSeconds,
            DesignerComboKind.OppositeGenderEscort => Result.RelationshipBuffSeconds,
            DesignerComboKind.DecoySustain => Result.ScenarioSeconds,
            DesignerComboKind.RemoteSupportLoneWolf => Result.ScenarioSeconds,
            DesignerComboKind.DistributedHunt => Result.ScenarioSeconds,
            DesignerComboKind.FrontlineBreakthrough => Result.ScenarioSeconds,
            _ => Result.LinkedSeconds,
        };
    }

    private bool IsPrimaryStoneAttacker(Character attacker)
    {
        int roleIndex = _scenario.Kind == DesignerComboKind.DiversionMagicStoneAssault ? 1 : 0;
        return roleIndex < _comboMembers.Count && _comboMembers[roleIndex] == attacker;
    }

    private bool IsComboVictory(CombatBattleState state)
    {
        return (_comboTeam == CombatTeam.Ally && state == CombatBattleState.Victory) ||
            (_comboTeam == CombatTeam.Enemy && state == CombatBattleState.Defeat);
    }

    private static Character ResolveTarget(Character character)
    {
        CombatAiBrain brain = character != null ? character.GetComponent<CombatAiBrain>() : null;
        if (brain == null) return null;
        if (brain.LastPlan.SkillTarget != null) return brain.LastPlan.SkillTarget;
        return brain.LastPlan.MoveTarget.TargetCharacter;
    }

    private static CombatObjective ResolveObjective(Character character)
    {
        CombatAiBrain brain = character != null ? character.GetComponent<CombatAiBrain>() : null;
        return brain != null ? brain.LastPlan.Objective : CombatObjective.Search;
    }

    private static float HorizontalDistance(Character first, Character second)
    {
        if (first == null || second == null) return float.PositiveInfinity;
        Vector3 a = first.transform.position;
        Vector3 b = second.transform.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static int CountTargeting(Character target, IReadOnlyList<Character> characters)
    {
        int count = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            if (ResolveTarget(characters[i]) == target) count++;
        }

        return count;
    }

    private int CountDivertedEnemies(Character decoy)
    {
        if (_characterSystem == null) return CountTargeting(decoy, _opponents);
        int count = 0;
        for (int i = 0; i < _opponents.Count; i++)
        {
            Character opponent = _opponents[i];
            if (ResolveTarget(opponent) != decoy || !_characterSystem.TryGetHomePosition(opponent, out Vector3 home)) continue;
            Vector3 position = opponent.transform.position;
            position.y = 0f;
            home.y = 0f;
            if (Vector3.Distance(position, home) >= 8f) count++;
        }
        return count;
    }

    private static bool HaveDistinctTargets(IReadOnlyList<Character> members)
    {
        var targets = new HashSet<Character>();
        int swords = 0;
        for (int i = 0; i < members.Count; i++)
        {
            Character member = members[i];
            if (member == null || member.EquippedWeapon == null || member.EquippedWeapon.Kind != WeaponKind.Sword) continue;
            Character target = ResolveTarget(member);
            if (target == null || !targets.Add(target)) return false;
            swords++;
        }

        return swords >= 2;
    }

    private static bool HaveMultipleSwordTargets(IReadOnlyList<Character> members)
    {
        int swordsWithTargets = 0;
        for (int i = 0; i < members.Count; i++)
        {
            Character member = members[i];
            if (member != null && member.EquippedWeapon != null && member.EquippedWeapon.Kind == WeaponKind.Sword && ResolveTarget(member) != null) swordsWithTargets++;
        }
        return swordsWithTargets >= 2;
    }

    private static float MaximumPairDistance(IReadOnlyList<Character> characters)
    {
        float maximum = 0f;
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == null) return float.PositiveInfinity;
            for (int j = i + 1; j < characters.Count; j++)
            {
                if (characters[j] == null) return float.PositiveInfinity;
                Vector3 a = characters[i].transform.position;
                Vector3 b = characters[j].transform.position;
                a.y = 0f;
                b.y = 0f;
                maximum = Mathf.Max(maximum, Vector3.Distance(a, b));
            }
        }

        return maximum;
    }

    private static bool AllActive(IReadOnlyList<Character> characters)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (!IsActive(characters[i])) return false;
        }

        return true;
    }

    private static bool IsActive(Character character)
    {
        return character != null && character.Health != null && character.Health.LifeState == LifeState.Active && character.Health.IsAlive;
    }

    private static bool Contains(IReadOnlyList<Character> characters, Character target)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == target) return true;
        }

        return false;
    }

}
