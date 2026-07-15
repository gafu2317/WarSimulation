using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
public sealed class CombatAiPersonalityRuntime : MonoBehaviour
{
    private const float GossiperProximity = 6f;
    private const float GossiperMultiplier = 1.25f;
    private const float LecherousProximity = 5f;
    private const float LecherousNearMultiplier = 1.2f;
    private const float LecherousFarMultiplier = 0.85f;
    private const float RelationshipEffectDuration = 2f;

    private static readonly UnstableRetaliationSkill RetaliationSkill = new UnstableRetaliationSkill();

    private readonly List<Character> _participants = new List<Character>();
    private Character _owner;
    private Character _gossipFirst;
    private Character _gossipSecond;
    private Character _companion;
    private Character _revengeTarget;

    public Character GossipFirst => _gossipFirst;
    public Character GossipSecond => _gossipSecond;
    public Character Companion => _companion;
    public Character RevengeTarget => _revengeTarget;

    private void Awake()
    {
        ResolveOwner();
    }

    private void OnEnable()
    {
        ResolveOwner();
        SubscribeDamage();
    }

    private void OnDisable()
    {
        UnsubscribeDamage();
    }

    public void ResetForBattle()
    {
        ResolveOwner();
        _gossipFirst = null;
        _gossipSecond = null;
        _companion = null;
        _revengeTarget = null;
        ClearRelationshipModifiers();
        SubscribeDamage();
    }

    public void Refresh()
    {
        ResolveOwner();
        if (_owner == null || _owner.Health == null || !_owner.Health.IsAlive) return;

        switch (_owner.PersonalityProfile != null ? _owner.PersonalityProfile.Kind : CombatAiPersonalityKind.Neutral)
        {
            case CombatAiPersonalityKind.Gossiper:
                RefreshGossiper();
                break;
            case CombatAiPersonalityKind.Lecherous:
                RefreshLecherous();
                break;
            default:
                ClearRelationshipModifiers();
                break;
        }

        if (_revengeTarget != null && !IsActive(_revengeTarget)) _revengeTarget = null;
    }

    public bool TryGetSignatureTarget(out CombatMoveTarget target)
    {
        target = CombatMoveTarget.None;
        if (_owner == null) return false;

        CombatAiPersonalityKind kind = _owner.PersonalityProfile != null
            ? _owner.PersonalityProfile.Kind
            : CombatAiPersonalityKind.Neutral;
        if (kind == CombatAiPersonalityKind.Gossiper && IsActive(_gossipFirst) && IsActive(_gossipSecond))
        {
            Vector3 midpoint = (_gossipFirst.transform.position + _gossipSecond.transform.position) * 0.5f;
            if (HorizontalDistance(_owner.transform.position, midpoint) <= 1.5f) return false;
            target = CombatMoveTarget.ForPosition(midpoint);
            return true;
        }

        if (kind == CombatAiPersonalityKind.Lecherous && IsActive(_companion))
        {
            if (HorizontalDistance(_owner.transform.position, _companion.transform.position) <= LecherousProximity) return false;
            target = CombatMoveTarget.ForCharacter(_companion);
            return true;
        }

        return false;
    }

    public bool TryBuildRevengePlan(out CombatAiPlan plan)
    {
        plan = CombatAiPlan.None;
        if (_owner == null || _owner.PersonalityProfile == null ||
            _owner.PersonalityProfile.Kind != CombatAiPersonalityKind.Unstable || !IsActive(_revengeTarget))
        {
            return false;
        }

        SkillExecutionContext context = SkillExecutionContext.ForTarget(_revengeTarget);
        plan = new CombatAiPlan(
            CombatObjective.AttackEnemy,
            CombatMoveTarget.ForCharacter(_revengeTarget),
            RetaliationSkill,
            context);
        return true;
    }

    public void NotifyPlanExecuted(CombatAiPlan plan, bool usedSkill)
    {
        if (usedSkill && ReferenceEquals(plan.Skill, RetaliationSkill)) _revengeTarget = null;
    }

    private void RefreshGossiper()
    {
        if (_gossipFirst != null || _gossipSecond != null)
        {
            if (!IsActive(_gossipFirst) || !IsActive(_gossipSecond))
            {
                ClearRelationshipModifiers();
                _owner.Health.WithdrawFromBattle();
                return;
            }
        }
        else
        {
            FindGossipPair();
        }

        if (!IsActive(_gossipFirst) || !IsActive(_gossipSecond))
        {
            ClearRelationshipModifiers();
            return;
        }

        bool nearBoth = HorizontalDistance(_owner.transform.position, _gossipFirst.transform.position) <= GossiperProximity &&
            HorizontalDistance(_owner.transform.position, _gossipSecond.transform.position) <= GossiperProximity;
        if (nearBoth) ApplyRelationshipModifier(GossiperMultiplier);
        else ClearRelationshipModifiers();
    }

    private void RefreshLecherous()
    {
        if (!IsValidCompanion(_companion)) _companion = FindNearestOppositeGenderAlly();
        bool isNear = IsValidCompanion(_companion) &&
            HorizontalDistance(_owner.transform.position, _companion.transform.position) <= LecherousProximity;
        ApplyRelationshipModifier(isNear ? LecherousNearMultiplier : LecherousFarMultiplier);
    }

    private void FindGossipPair()
    {
        CollectParticipants();
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < _participants.Count; i++)
        {
            Character first = _participants[i];
            if (first == _owner || !IsActive(first) || first.CharacterData == null || first.CharacterData.Lover == null) continue;
            for (int j = i + 1; j < _participants.Count; j++)
            {
                Character second = _participants[j];
                if (second == _owner || !IsActive(second) || second.CharacterData == null) continue;
                if (first.CharacterData.Lover != second.CharacterData || second.CharacterData.Lover != first.CharacterData) continue;

                Vector3 midpoint = (first.transform.position + second.transform.position) * 0.5f;
                float distance = HorizontalDistance(_owner.transform.position, midpoint);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                _gossipFirst = first;
                _gossipSecond = second;
            }
        }
    }

    private Character FindNearestOppositeGenderAlly()
    {
        if (_owner.CharacterData == null || _owner.CharacterData.Gender == CharacterGender.Unspecified) return null;

        CollectParticipants();
        Character best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < _participants.Count; i++)
        {
            Character candidate = _participants[i];
            if (!IsValidCompanion(candidate)) continue;
            float distance = HorizontalDistance(_owner.transform.position, candidate.transform.position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    private bool IsValidCompanion(Character candidate)
    {
        if (!IsActive(candidate) || candidate == _owner || candidate.Team != _owner.Team || candidate.CharacterData == null) return false;
        CharacterGender ownerGender = _owner.CharacterData != null ? _owner.CharacterData.Gender : CharacterGender.Unspecified;
        CharacterGender candidateGender = candidate.CharacterData.Gender;
        return ownerGender != CharacterGender.Unspecified &&
            candidateGender != CharacterGender.Unspecified &&
            ownerGender != candidateGender;
    }

    private void OnDamaged(int amount, Character attacker)
    {
        if (amount <= 0 || attacker == null || _owner == null || attacker.Team == _owner.Team) return;
        if (_owner.PersonalityProfile == null || _owner.PersonalityProfile.Kind != CombatAiPersonalityKind.Unstable) return;
        if (_revengeTarget != null) return;

        IReadOnlyList<Character> allies = CombatSkillTargeting.GetAllAllies(_owner);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (!CouldHaveProtected(ally, attacker, out float distance) || distance >= bestDistance) continue;
            bestDistance = distance;
            _revengeTarget = ally;
        }
    }

    private bool CouldHaveProtected(Character ally, Character attacker, out float distance)
    {
        distance = float.PositiveInfinity;
        if (!IsActive(ally) || ally.EquippedWeapon == null || ally.EquippedWeapon.Kind != WeaponKind.Shield) return false;
        if (ally.SkillCaster == null || ally.SkillCaster.IsCasting) return false;

        ShieldShoulderGuardSkill guardSkill = null;
        IReadOnlyList<SkillBase> skills = ally.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] is ShieldShoulderGuardSkill candidate)
            {
                guardSkill = candidate;
                break;
            }
        }

        if (guardSkill == null || ally.SkillCooldowns == null || !ally.SkillCooldowns.IsReady(guardSkill)) return false;
        distance = HorizontalDistance(_owner.transform.position, ally.transform.position);
        if (distance > guardSkill.MaxRange) return false;

        CombatVision vision = ally.Vision;
        vision?.UpdateVision();
        return vision == null || vision.IsVisible(attacker);
    }

    private void ApplyRelationshipModifier(float multiplier)
    {
        if (_owner.StatusEffects == null) return;
        for (int value = (int)CombatStatusEffects.StatKind.STR; value <= (int)CombatStatusEffects.StatKind.AGI; value++)
        {
            CombatStatusEffects.StatKind stat = (CombatStatusEffects.StatKind)value;
            _owner.StatusEffects.Apply(
                stat,
                multiplier,
                RelationshipEffectDuration,
                GetEffectKey(stat),
                _owner);
        }
    }

    private void ClearRelationshipModifiers()
    {
        if (_owner == null || _owner.StatusEffects == null) return;
        for (int value = (int)CombatStatusEffects.StatKind.STR; value <= (int)CombatStatusEffects.StatKind.AGI; value++)
        {
            _owner.StatusEffects.ClearEffect(GetEffectKey((CombatStatusEffects.StatKind)value));
        }
    }

    private static string GetEffectKey(CombatStatusEffects.StatKind stat)
    {
        return "PersonalityRelationship_" + stat;
    }

    private void CollectParticipants()
    {
        _participants.Clear();
        CombatCharacterSystem system = CombatSceneContext.Instance != null ? CombatSceneContext.Instance.CharacterSystem : null;
        if (system != null)
        {
            AddParticipants(system.AllyCharacters);
            AddParticipants(system.EnemyCharacters);
            return;
        }

        Character[] characters = FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        AddParticipants(characters);
    }

    private void AddParticipants(IReadOnlyList<Character> characters)
    {
        if (characters == null) return;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character != null && !_participants.Contains(character)) _participants.Add(character);
        }
    }

    private void SubscribeDamage()
    {
        if (_owner == null || _owner.Health == null) return;
        _owner.Health.Damaged -= OnDamaged;
        _owner.Health.Damaged += OnDamaged;
    }

    private void UnsubscribeDamage()
    {
        if (_owner != null && _owner.Health != null) _owner.Health.Damaged -= OnDamaged;
    }

    private void ResolveOwner()
    {
        if (_owner == null) _owner = GetComponent<Character>();
    }

    private static bool IsActive(Character character)
    {
        return character != null && character.Health != null && character.Health.LifeState == LifeState.Active && character.Health.IsAlive;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
