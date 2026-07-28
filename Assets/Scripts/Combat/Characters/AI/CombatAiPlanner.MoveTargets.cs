using System.Collections.Generic;
using UnityEngine;

public static partial class CombatAiPlanner
{
    private const float StoneApproachDistance = 2.5f;

    private static CombatMoveTarget CreateAttentionSeekerTarget(CombatAiContext context)
    {
        Vector3 enemyCenter = Vector3.zero;
        int enemyCount = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
            enemyCenter += enemy.KnownPosition;
            enemyCount++;
        }

        if (enemyCount == 0) return CombatMoveTarget.None;
        enemyCenter /= enemyCount;
        Vector3 direction = Flatten(context.Owner.transform.position - enemyCenter);
        if (direction.sqrMagnitude <= 0.01f) direction = Vector3.back;
        direction.Normalize();
        float range = context.Owner.EquippedWeapon != null
            ? Mathf.Max(2.5f, context.Owner.EquippedWeapon.Range * 0.85f)
            : 2.5f;
        Vector3 destination = enemyCenter + direction * range;
        destination.y = context.Owner.transform.position.y;
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateClumsyTarget(CombatAiContext context)
    {
        int interval = CombatBattleRandom.GetDecisionInterval(context.Owner, 4f);
        bool makesMistake = CombatBattleRandom.Choose(context.Owner, "ClumsyMove", interval, 10) == 0;
        return makesMistake ? CreateOwnStoneTarget(context) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateEnemyStoneTarget(CombatAiContext context)
    {
        return CreateEnemyStoneTarget(context, hasAssaultRouteKey: false, assaultRouteKey: 0);
    }

    private static CombatMoveTarget CreateEnemyStoneTarget(
        CombatAiContext context,
        bool hasAssaultRouteKey,
        int assaultRouteKey)
    {
        if (!context.HasEnemyStonePosition || context.Owner == null) return CombatMoveTarget.None;

        Vector3 ownerPosition = context.Owner.transform.position;
        Vector3 awayFromStone = Flatten(ownerPosition - context.EnemyStonePosition);
        if (awayFromStone.sqrMagnitude <= 0.01f) awayFromStone = Vector3.back;
        awayFromStone.Normalize();
        if (HorizontalDistance(ownerPosition, context.EnemyStonePosition) <= StoneApproachDistance)
        {
            return CombatMoveTarget.None;
        }

        Vector3 bestDestination = default;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * awayFromStone;
            Vector3 candidate = context.EnemyStonePosition + direction * StoneApproachDistance;
            candidate.y = ownerPosition.y;
            if (!CombatAiMoveScorer.IsReachable(context.Owner, candidate)) continue;

            float distance = HorizontalDistance(ownerPosition, candidate);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestDestination = candidate;
        }

        if (float.IsPositiveInfinity(bestDistance)) return CombatMoveTarget.None;
        return hasAssaultRouteKey
            ? CombatMoveTarget.ForPosition(bestDestination, assaultRouteKey)
            : CombatMoveTarget.ForPosition(bestDestination);
    }

    private static void CreateAssaultRouteAdvanceCandidate(
        CombatAiContext context,
        CombatAiAssaultRoute route,
        out string code,
        out string japanese,
        out CombatMoveTarget target)
    {
        code = CombatAiMoveCode.AdvanceEnemyStone;
        japanese = "敵魔石へ前進";
        target = CombatMoveTarget.None;
        if (context == null || context.Owner == null) return;

        const float arriveThreshold = 2f;
        Vector3 ownerPosition = context.Owner.transform.position;
        int routeKey = route.BridgeFeatureIndex;

        if (route.HasBridgeWaypoints)
        {
            if (HorizontalDistance(ownerPosition, route.EnterWorld) > arriveThreshold)
            {
                code = CombatAiMoveCode.AdvanceViaBridge;
                japanese = "橋を経由して敵魔石へ前進";
                target = CombatMoveTarget.ForPosition(route.EnterWorld, routeKey);
                return;
            }

            if (HorizontalDistance(ownerPosition, route.ExitWorld) > arriveThreshold)
            {
                code = CombatAiMoveCode.AdvanceViaBridge;
                japanese = "橋を経由して敵魔石へ前進";
                target = CombatMoveTarget.ForPosition(route.ExitWorld, routeKey);
                return;
            }
        }

        code = CombatAiMoveCode.AdvanceEnemyStone;
        japanese = "敵魔石へ前進";
        target = CreateEnemyStoneTarget(context, hasAssaultRouteKey: true, assaultRouteKey: routeKey);
    }

    private static CombatMoveTarget CreateOwnStoneTarget(CombatAiContext context)
    {
        return context.HasOwnStonePosition ? CombatMoveTarget.ForPosition(context.OwnStonePosition) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateAllyRelativeTarget(CombatAiContext context, bool towardEnemy)
    {
        Character ally = FindBestAllyCharacter(context);
        CombatCharacterIntel enemy = FindNearestKnownEnemyIntel(context, ally != null ? ally.transform.position : context.Owner.transform.position);
        if (ally == null || enemy.Character == null) return CombatMoveTarget.None;

        Vector3 direction = Flatten(enemy.KnownPosition - ally.transform.position);
        if (direction.sqrMagnitude <= 0.01f) return CombatMoveTarget.None;
        direction.Normalize();
        Vector3 destination = ally.transform.position + direction * (towardEnemy ? 2f : -2.5f);
        destination.y = context.Owner.transform.position.y;
        return HorizontalDistance(context.Owner.transform.position, destination) > 1.25f
            ? CombatMoveTarget.ForPosition(destination)
            : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateOrbitTarget(CombatAiContext context)
    {
        CombatCharacterIntel enemy = FindNearestKnownEnemyIntel(context, context.Owner.transform.position);
        if (enemy.Character == null) return CombatMoveTarget.None;

        Vector3 radial = Flatten(context.Owner.transform.position - enemy.KnownPosition);
        if (radial.sqrMagnitude <= 0.01f) radial = Vector3.forward;
        radial.Normalize();
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
        Vector3 destination = enemy.KnownPosition + radial * 5f + tangent * 3f;
        destination.y = context.Owner.transform.position.y;
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateLoneWolfTarget(CombatAiContext context)
    {
        Character best = null;
        int fewestAllies = int.MaxValue;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) continue;

            int nearbyAllies = 0;
            for (int j = 0; j < context.AllyIntel.Count; j++)
            {
                if (context.AllyIntel[j].IsAlive &&
                    HorizontalDistance(context.AllyIntel[j].CurrentPosition, enemy.KnownPosition) <= 6f)
                {
                    nearbyAllies++;
                }
            }

            float distance = HorizontalDistance(context.Owner.transform.position, enemy.KnownPosition);
            if (nearbyAllies > fewestAllies || nearbyAllies == fewestAllies && distance >= bestDistance) continue;
            fewestAllies = nearbyAllies;
            bestDistance = distance;
            best = enemy.Character;
        }

        return best != null ? CombatMoveTarget.ForCharacter(best) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateEccentricTarget(CombatAiContext context)
    {
        int interval = CombatBattleRandom.GetDecisionInterval(context.Owner, 3f);
        int choice = CombatBattleRandom.Choose(context.Owner, "EccentricMove", interval, 3);
        return choice switch
        {
            0 => CreateBestEnemyTarget(context, null, 0f),
            1 => CreateBestAllyTarget(context),
            _ => CreateEnemyStoneTarget(context),
        };
    }

    private static CombatMoveTarget CreateBestEnemyTarget(CombatAiContext context, Character focusEnemy, float focusCommitmentRemainingSeconds)
    {
        Character enemy = FindBestEnemyCharacter(context, focusEnemy, focusCommitmentRemainingSeconds);
        if (enemy == null)
        {
            return CombatMoveTarget.None;
        }

        Character owner = context != null ? context.Owner : null;
        WeaponBase weapon = owner != null ? owner.EquippedWeapon : null;
        if (owner != null && weapon != null && (weapon.Kind == WeaponKind.Wand || weapon.Kind == WeaponKind.Grimoire))
        {
            return CreateRangedAttackTarget(context, owner, enemy);
        }

        return CombatMoveTarget.ForCharacter(enemy);
    }

    private static CombatMoveTarget CreateBestAllyTarget(CombatAiContext context)
    {
        Character owner = context != null ? context.Owner : null;
        WeaponBase weapon = owner != null ? owner.EquippedWeapon : null;
        if (owner != null && weapon != null && weapon.Kind == WeaponKind.Shield)
        {
            return CreateShieldProtectTarget(context, owner);
        }

        Character ally = FindBestAllyCharacter(context);
        if (ally == null)
        {
            return CombatMoveTarget.None;
        }

        if (owner != null && weapon != null && weapon.Kind == WeaponKind.Rosary)
        {
            return CreateRosarySupportTarget(context, owner, ally);
        }

        if (owner != null && weapon != null && weapon.Kind == WeaponKind.Bible)
        {
            return CreateBibleSupportTarget(context, owner, ally);
        }

        return CombatMoveTarget.ForCharacter(ally);
    }

    private static CombatMoveTarget CreateBestBodyBlockTarget(CombatAiContext context)
    {
        Character owner = context != null ? context.Owner : null;
        if (owner == null || owner.EquippedWeapon == null || owner.EquippedWeapon.Kind != WeaponKind.Shield)
        {
            return CombatMoveTarget.None;
        }

        float bestValue = 0f;
        Vector3 bestPosition = default;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition || !enemy.CanAct) continue;

            for (int j = 0; j < context.AllyIntel.Count; j++)
            {
                CombatCharacterIntel ally = context.AllyIntel[j];
                if (!ally.CanAct || !IsFrontlineAlly(context, ally)) continue;
                TrySelectBodyBlockPosition(owner, enemy, ally.CurrentPosition, GetProtectedAllyValue(context, ally), ref bestValue, ref bestPosition);
            }

            if (context.HasOwnStonePosition)
            {
                TrySelectBodyBlockPosition(owner, enemy, context.OwnStonePosition, 70f, ref bestValue, ref bestPosition);
            }
        }

        return bestValue > 0f ? CombatMoveTarget.ForPosition(bestPosition) : CombatMoveTarget.None;
    }

    private static void TrySelectBodyBlockPosition(
        Character owner,
        CombatCharacterIntel enemy,
        Vector3 protectedPosition,
        float protectedValue,
        ref float bestValue,
        ref Vector3 bestPosition)
    {
        Vector3 threatDirection = Flatten(enemy.KnownPosition - protectedPosition);
        float enemyDistance = threatDirection.magnitude;
        if (enemyDistance <= 0.1f || enemyDistance > 12f) return;

        threatDirection /= enemyDistance;
        Vector3 candidate = protectedPosition + threatDirection * Mathf.Min(2f, enemyDistance * 0.5f);
        candidate.y = owner.transform.position.y;
        float ownerArrival = HorizontalDistance(owner.transform.position, candidate) /
            Mathf.Max(0.1f, owner.GetComponent<UnityEngine.AI.NavMeshAgent>()?.speed ?? 3.5f);
        float enemyArrival = Mathf.Max(0f, enemyDistance - 1.5f) / enemy.MoveSpeed;
        if (ownerArrival >= enemyArrival) return;

        float value = protectedValue + Mathf.Clamp((enemyArrival - ownerArrival) * 8f, 0f, 24f);
        if (value <= bestValue) return;
        bestValue = value;
        bestPosition = candidate;
    }

    private static float GetProtectedAllyValue(CombatAiContext context, CombatCharacterIntel ally)
    {
        if (ally.MaxHP <= 0) return 20f;
        float missingHpRatio = 1f - ally.HP / (float)ally.MaxHP;
        float swordBonus = ally.WeaponKind == WeaponKind.Sword ? 15f : 0f;
        return 30f + missingHpRatio * 45f
            + CombatAiPositioning.GetAdvanceProgress(context, ally.CurrentPosition) * 40f
            + swordBonus;
    }

    private static CombatMoveTarget CreateRosarySupportTarget(CombatAiContext context, Character owner, Character ally)
    {
        if (context == null || owner == null || ally == null)
        {
            return CombatMoveTarget.None;
        }

        CombatCharacterIntel allyIntel = FindAllyIntel(context, ally);
        if (allyIntel.Character == null || allyIntel.MaxHP <= 0)
        {
            return CombatMoveTarget.ForCharacter(ally);
        }

        int missingHp = Mathf.Max(0, allyIntel.MaxHP - allyIntel.HP);
        float currentDistance = HorizontalDistance(owner.transform.position, ally.transform.position);
        int currentHealAmount = EstimateCurrentRosaryHealAmount(owner, ally, currentDistance);
        float desiredDistance = missingHp > currentHealAmount
            ? RosaryCloseHealDistance
            : RosaryPreferredSupportDistance;
        CombatCharacterIntel nearestThreat = FindNearestKnownEnemyIntel(context, owner.transform.position);
        bool threatTooClose = nearestThreat.Character != null &&
            HorizontalDistance(owner.transform.position, nearestThreat.KnownPosition) < RosaryEnemyClearanceDistance;

        if (Mathf.Abs(currentDistance - desiredDistance) <= 0.75f && !threatTooClose)
        {
            return CombatMoveTarget.None;
        }

        Vector3 destination = ResolveSupportStandoffPositionWithCoverBias(context, owner, ally, desiredDistance);
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateRangedAttackTarget(CombatAiContext context, Character owner, Character enemy)
    {
        if (owner == null || enemy == null)
        {
            return CombatMoveTarget.None;
        }

        float desiredDistance = EstimatePreferredWandAttackDistance(owner);
        if (desiredDistance <= 0f)
        {
            return CombatMoveTarget.ForCharacter(enemy);
        }

        float currentDistance = HorizontalDistance(owner.transform.position, enemy.transform.position);
        float minimumHoldDistance = Mathf.Max(0f, desiredDistance - WandRangeSlack);
        float maximumHoldDistance = desiredDistance + WandRangeSlack;
        CombatCharacterIntel nearestThreat = FindNearestKnownEnemyIntel(context, owner.transform.position);
        bool threatTooClose = nearestThreat.Character != null &&
            HorizontalDistance(owner.transform.position, nearestThreat.KnownPosition) < WandEnemyClearanceDistance;
        if (currentDistance >= minimumHoldDistance && currentDistance <= maximumHoldDistance && !threatTooClose)
        {
            return CombatMoveTarget.None;
        }

        Character standoffEnemy = threatTooClose && nearestThreat.Character != null ? nearestThreat.Character : enemy;
        Vector3 destination = ResolveEnemyStandoffPositionWithCoverBias(context, owner, standoffEnemy, desiredDistance);
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateShieldProtectTarget(CombatAiContext context, Character owner)
    {
        if (context == null || owner == null) return CombatMoveTarget.None;

        CombatCharacterIntel ally = FindBestShieldProtectTarget(context, owner);
        if (ally.Character == null) return CombatMoveTarget.None;

        bool isFrontline = IsFrontlineAlly(context, ally);
        Vector3 allyPos = isFrontline && ally.HasIntendedDestination
            ? ally.IntendedDestination
            : ally.CurrentPosition;
        Vector3 enemyDirection = Vector3.zero;
        float nearestEnemyDist = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
            float d = HorizontalDistance(allyPos, enemy.KnownPosition);
            if (d >= nearestEnemyDist) continue;
            nearestEnemyDist = d;
            enemyDirection = Flatten(enemy.KnownPosition - allyPos);
        }

        if (enemyDirection.sqrMagnitude <= 0.01f)
        {
            return HorizontalDistance(owner.transform.position, allyPos) > 2f
                ? CombatMoveTarget.ForPosition(allyPos)
                : CombatMoveTarget.None;
        }

        enemyDirection.Normalize();
        const float protectOffset = 1.5f;
        Vector3 destination = allyPos + enemyDirection * protectOffset;
        destination.y = owner.transform.position.y;

        if (HorizontalDistance(owner.transform.position, destination) < 1.5f) return CombatMoveTarget.None;
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatCharacterIntel FindBestShieldProtectTarget(CombatAiContext context, Character owner)
    {
        CombatCharacterIntel best = default;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null || !ally.CanAct || !IsFrontlineAlly(context, ally)) continue;

            float score = GetProtectedAllyValue(context, ally);
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f)) score += 20f;
            score -= HorizontalDistance(owner.transform.position, ally.CurrentPosition) * 0.5f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = ally;
        }

        return best;
    }

    private static bool IsFrontlineAlly(CombatAiContext context, CombatCharacterIntel ally)
    {
        return CombatAiPositioning.IsAdvancingAlly(context, ally);
    }

    private static CombatMoveTarget CreateBibleSupportTarget(CombatAiContext context, Character owner, Character ally)
    {
        if (context == null || owner == null || ally == null) return CombatMoveTarget.None;

        const float bibleDesiredDistance = 2.5f;
        float currentDistance = HorizontalDistance(owner.transform.position, ally.transform.position);
        if (currentDistance <= bibleDesiredDistance + 0.75f) return CombatMoveTarget.None;

        Vector3 destination = ResolveSupportStandoffPositionWithCoverBias(context, owner, ally, bibleDesiredDistance);
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateLastKnownEnemyTarget(CombatAiContext context)
    {
        float bestScore = float.NegativeInfinity;
        Vector3 bestPosition = default;
        bool found = false;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;

            float score = enemy.HasDirectSight ? 1000f : 0f;
            score -= enemy.HasMemory ? enemy.MemoryAgeSeconds : 0f;
            score += enemy.MaxHP > 0 ? (1f - enemy.HP / (float)enemy.MaxHP) * 10f : 0f;
            score -= HorizontalDistance(context.Owner.transform.position, enemy.KnownPosition) * 0.05f;
            if (score <= bestScore) continue;

            bestScore = score;
            bestPosition = enemy.KnownPosition;
            found = true;
        }

        return found ? CombatMoveTarget.ForPosition(bestPosition) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateNearestPositionTarget(Character owner, IReadOnlyList<Vector3> positions)
    {
        if (owner == null || positions == null || positions.Count == 0) return CombatMoveTarget.None;

        const float minimumMeaningfulDistance = 2f;
        float bestDistance = float.PositiveInfinity;
        Vector3 best = default;
        bool found = false;
        for (int i = 0; i < positions.Count; i++)
        {
            float distance = HorizontalDistance(owner.transform.position, positions[i]);
            if (distance <= minimumMeaningfulDistance) continue;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = positions[i];
            found = true;
        }

        return found ? CombatMoveTarget.ForPosition(best) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreatePositionTargetIfMeaningful(Character owner, Vector3 position)
    {
        return owner != null && HorizontalDistance(owner.transform.position, position) > 2f
            ? CombatMoveTarget.ForPosition(position)
            : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateCoverPositionTarget(CombatAiContext context, Character owner)
    {
        if (owner == null || context == null || context.ForestCandidates.Count == 0) return CombatMoveTarget.None;

        WeaponKind weaponKind = owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
        Character supportTarget = weaponKind == WeaponKind.Bible || weaponKind == WeaponKind.Rosary
            ? FindBestAllyCharacter(context)
            : null;
        float supportRange = supportTarget != null ? GetSupportRange(owner) : 0f;
        float offensiveRange = weaponKind == WeaponKind.Wand || weaponKind == WeaponKind.Grimoire
            ? GetReadyOffensiveRange(owner)
            : 0f;
        if ((weaponKind == WeaponKind.Bible || weaponKind == WeaponKind.Rosary) && supportRange <= 0f)
        {
            return CombatMoveTarget.None;
        }

        const float minMeaningfulDistance = 2f;
        Vector3 ownerPos = owner.transform.position;
        float bestScore = float.NegativeInfinity;
        Vector3 best = default;
        bool found = false;

        for (int i = 0; i < context.ForestCandidates.Count; i++)
        {
            Vector3 candidate = context.ForestCandidates[i];
            float ownerDist = HorizontalDistance(ownerPos, candidate);
            if (ownerDist <= minMeaningfulDistance) continue;

            if (supportTarget != null &&
                (HorizontalDistance(candidate, supportTarget.transform.position) > supportRange ||
                 !HasLineOfSightFrom(candidate, supportTarget)))
            {
                continue;
            }

            float usableEnemyValue = 0f;
            if (offensiveRange > 0f)
            {
                for (int j = 0; j < context.EnemyIntel.Count; j++)
                {
                    CombatCharacterIntel enemy = context.EnemyIntel[j];
                    if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) continue;
                    if (HorizontalDistance(candidate, enemy.KnownPosition) > offensiveRange ||
                        !HasLineOfSightFrom(candidate, enemy.Character)) continue;
                    usableEnemyValue = Mathf.Max(usableEnemyValue, 20f);
                }
            }

            float nearestEnemyDist = float.PositiveInfinity;
            for (int j = 0; j < context.EnemyIntel.Count; j++)
            {
                CombatCharacterIntel enemy = context.EnemyIntel[j];
                if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
                float d = HorizontalDistance(candidate, enemy.KnownPosition);
                if (d < nearestEnemyDist) nearestEnemyDist = d;
            }

            if (nearestEnemyDist == float.PositiveInfinity) nearestEnemyDist = 0f;

            float score = -ownerDist * 0.5f + Mathf.Min(nearestEnemyDist, 20f) * 0.8f + usableEnemyValue;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
            found = true;
        }

        return found ? CombatMoveTarget.ForPosition(best) : CombatMoveTarget.None;
    }

    private static bool IsRangedOrSupportWeapon(WeaponKind kind)
    {
        return kind == WeaponKind.Wand || kind == WeaponKind.Grimoire
            || kind == WeaponKind.Bible || kind == WeaponKind.Rosary;
    }

    private static float GetReadyOffensiveRange(Character owner)
    {
        return GetSkillRange(owner, requireSupport: false, requireReady: true);
    }

    private static float GetSupportRange(Character owner)
    {
        return GetSkillRange(owner, requireSupport: true, requireReady: false);
    }

    private static float GetSkillRange(Character owner, bool requireSupport, bool requireReady)
    {
        if (owner == null) return 0f;

        float range = 0f;
        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = owner.SkillCooldowns;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || requireReady && cooldowns != null && !cooldowns.IsReady(skill)) continue;
            bool matches = requireSupport
                ? CombatAiSkillClassifier.IsSupport(skill)
                : CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill);
            if (matches) range = Mathf.Max(range, skill.MaxRange);
        }

        return range;
    }

    private static bool HasLineOfSightFrom(Vector3 position, Character target)
    {
        if (target == null) return false;

        Vector3 from = position + Vector3.up;
        Vector3 to = target.transform.position + Vector3.up;
        if (!Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Character hitCharacter = hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
        return hitCharacter == target;
    }

    private static Character FindBestEnemyCharacter(CombatAiContext context, Character focusEnemy, float focusCommitmentRemainingSeconds)
    {
        Character focusCandidate = CombatAiFocusTargeting.IsValid(context, focusEnemy) ? focusEnemy : null;
        Character best = focusCandidate;
        float bestScore = focusCandidate != null
            ? ScoreEnemyTarget(context, focusCandidate) + CombatAiFocusTargeting.GetSelectionBonus(context, focusCandidate, focusCommitmentRemainingSeconds)
            : float.NegativeInfinity;

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) continue;
            float score = ScoreEnemyTarget(context, enemy.Character);
            if (enemy.Character == focusCandidate)
            {
                score += CombatAiFocusTargeting.GetSelectionBonus(context, enemy.Character, focusCommitmentRemainingSeconds);
            }

            float requiredScore = bestScore;
            if (focusCandidate != null && enemy.Character != focusCandidate && focusCommitmentRemainingSeconds > 0f)
            {
                requiredScore += SwordRetargetThreshold;
            }

            if (score <= requiredScore) continue;
            bestScore = score;
            best = enemy.Character;
        }

        return best;
    }

    private static float ScoreEnemyTarget(CombatAiContext context, Character enemyCharacter)
    {
        CombatCharacterIntel enemy = FindEnemyIntel(context, enemyCharacter);
        if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) return float.NegativeInfinity;

        int predictedHp = enemy.HP - GetAllyPendingDamage(context, enemyCharacter);
        if (predictedHp <= 0) return float.NegativeInfinity;
        float hpRatio = enemy.MaxHP > 0 ? predictedHp / (float)enemy.MaxHP : 1f;
        return (1f - hpRatio) * 60f + (enemy.HasDirectSight ? 25f : enemy.HasMemory ? 10f : 0f);
    }

    private static Character FindBestAllyCharacter(CombatAiContext context)
    {
        Character best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null || !ally.IsAlive) continue;
            int projectedHP = Mathf.Min(
                ally.MaxHP,
                ally.HP + GetAllyPendingHealing(context, ally.Character));
            float hpRatio = ally.MaxHP > 0 ? projectedHP / (float)ally.MaxHP : 1f;
            float score = (1f - hpRatio) * 60f
                + (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f) ? 20f : 0f)
                + CombatAiPositioning.GetAdvanceProgress(context, ally.CurrentPosition) * 10f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = ally.Character;
        }

        return best;
    }

    private static CombatCharacterIntel FindNearestKnownEnemyIntel(CombatAiContext context, Vector3 position)
    {
        if (context == null)
        {
            return default;
        }

        CombatCharacterIntel best = default;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition)
            {
                continue;
            }

            float distance = HorizontalDistance(position, enemy.KnownPosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = enemy;
        }

        return best;
    }

    private static int EstimateCurrentRosaryHealAmount(Character owner, Character ally, float distance)
    {
        if (owner == null || ally == null)
        {
            return 0;
        }

        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = owner.SkillCooldowns;
        int bestHeal = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || !CombatAiSkillClassifier.IsHeal(skill))
            {
                continue;
            }

            if (cooldowns != null && !cooldowns.IsReady(skill))
            {
                continue;
            }

            int estimate = EstimateRosaryHealAmount(owner, skill, distance);
            if (estimate > bestHeal)
            {
                bestHeal = estimate;
            }
        }

        return bestHeal;
    }

    private static float EstimatePreferredWandAttackDistance(Character owner)
    {
        if (owner == null)
        {
            return 0f;
        }

        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = owner.SkillCooldowns;
        float bestRange = 0f;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || !CombatAiSkillClassifier.IsDamage(skill))
            {
                continue;
            }

            if (cooldowns != null && !cooldowns.IsReady(skill))
            {
                continue;
            }

            bestRange = Mathf.Max(bestRange, skill.MaxRange);
        }

        if (bestRange <= 0f)
        {
            WeaponBase weapon = owner.EquippedWeapon;
            bestRange = weapon != null ? weapon.Range : 0f;
        }

        return Mathf.Max(0f, bestRange - 1f);
    }

    private static int EstimateRosaryHealAmount(Character owner, SkillBase skill, float distance)
    {
        if (owner == null || skill == null)
        {
            return 0;
        }

        if (distance > skill.MaxRange)
        {
            return 0;
        }

        float fai = owner.GetEffectiveStat(CombatStat.FAI);
        if (skill is IdentifiedSkill identified)
        {
            switch (identified.SkillId)
            {
                case SkillId.Rosary_DistantHeal:
                {
                    int baseHeal = Mathf.Max(1, Mathf.RoundToInt(fai * 0.4f));
                    return ComputeDistanceScaledAmount(baseHeal, distance, skill.MaxRange, 1.4f, 0.8f);
                }
                case SkillId.Rosary_CloseHeal:
                    return Mathf.Max(1, Mathf.RoundToInt(fai * 0.9f));
                case SkillId.Rosary_Regeneration:
                    return 35;
                case SkillId.Rosary_HealingArea:
                    return 20;
                default:
                    return 0;
            }
        }

        return 0;
    }

    private static Vector3 ResolveSupportStandoffPosition(
        CombatAiContext context,
        Character owner,
        Character ally,
        float desiredDistance)
    {
        Vector3 allyPosition = ally.transform.position;
        Vector3 direction = Vector3.zero;
        float nearestEnemyDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition)
            {
                continue;
            }

            float distance = HorizontalDistance(enemy.KnownPosition, allyPosition);
            if (distance >= nearestEnemyDistance)
            {
                continue;
            }

            nearestEnemyDistance = distance;
            direction = Flatten(allyPosition - enemy.KnownPosition);
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Flatten(owner.transform.position - allyPosition);
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Vector3.back;
        }

        direction.Normalize();
        Vector3 destination = allyPosition + direction * desiredDistance;
        destination.y = owner.transform.position.y;
        return destination;
    }

    private static Vector3 ResolveEnemyStandoffPosition(Character owner, Character enemy, float desiredDistance)
    {
        Vector3 enemyPosition = enemy.transform.position;
        Vector3 direction = Flatten(owner.transform.position - enemyPosition);
        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Vector3.back;
        }

        direction.Normalize();
        Vector3 destination = enemyPosition + direction * desiredDistance;
        destination.y = owner.transform.position.y;
        return destination;
    }

    private static Vector3 ResolveEnemyStandoffPositionWithCoverBias(
        CombatAiContext context, Character owner, Character enemy, float desiredDistance)
    {
        Vector3 basePosition = ResolveEnemyStandoffPosition(owner, enemy, desiredDistance);
        if (context == null || context.ForestCandidates.Count == 0) return basePosition;

        float slack = desiredDistance * 0.35f;
        float minDist = Mathf.Max(1f, desiredDistance - slack);
        float maxDist = desiredDistance + slack;
        Vector3 enemyPos = enemy.transform.position;
        float bestScore = float.NegativeInfinity;
        Vector3 best = basePosition;

        for (int i = 0; i < context.ForestCandidates.Count; i++)
        {
            Vector3 candidate = context.ForestCandidates[i];
            float distToEnemy = HorizontalDistance(candidate, enemyPos);
            if (distToEnemy < minDist || distToEnemy > maxDist) continue;
            if (!HasLineOfSightFrom(candidate, enemy)) continue;

            float distToOwner = HorizontalDistance(candidate, owner.transform.position);
            if (distToOwner > desiredDistance * 2.5f) continue;

            float score = -distToOwner;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        best.y = owner.transform.position.y;
        return best;
    }

    private static Vector3 ResolveSupportStandoffPositionWithCoverBias(
        CombatAiContext context, Character owner, Character ally, float desiredDistance)
    {
        Vector3 basePosition = ResolveSupportStandoffPosition(context, owner, ally, desiredDistance);
        if (context == null || context.ForestCandidates.Count == 0) return basePosition;

        float supportRange = GetSupportRange(owner);
        if (supportRange <= 0f) return basePosition;
        float slack = desiredDistance * 0.5f + 2f;
        float maxDistFromAlly = Mathf.Min(supportRange, desiredDistance + slack);
        Vector3 allyPos = ally.transform.position;
        float bestScore = float.NegativeInfinity;
        Vector3 best = basePosition;

        for (int i = 0; i < context.ForestCandidates.Count; i++)
        {
            Vector3 candidate = context.ForestCandidates[i];
            float distToAlly = HorizontalDistance(candidate, allyPos);
            if (distToAlly > maxDistFromAlly) continue;
            if (!HasLineOfSightFrom(candidate, ally)) continue;

            float nearestEnemyDist = float.PositiveInfinity;
            for (int j = 0; j < context.EnemyIntel.Count; j++)
            {
                CombatCharacterIntel enemy = context.EnemyIntel[j];
                if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
                float d = HorizontalDistance(candidate, enemy.KnownPosition);
                if (d < nearestEnemyDist) nearestEnemyDist = d;
            }

            if (nearestEnemyDist == float.PositiveInfinity) nearestEnemyDist = 0f;
            float distToOwner = HorizontalDistance(candidate, owner.transform.position);
            float score = -distToOwner * 0.5f + Mathf.Min(nearestEnemyDist, 15f) * 0.6f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        best.y = owner.transform.position.y;
        return best;
    }
}
