using System.Collections.Generic;
using UnityEngine;

public static partial class CombatAiPlanner
{
    private const float AssaultRouteParticipantDistance = 4f;

    private enum AssaultRouteSelectionMode
    {
        LeastParticipants,
        MostParticipants,
        Nearest,
    }

    private static CombatMoveTarget SelectAssaultRouteTarget(
        CombatAiContext context,
        AssaultRouteSelectionMode mode,
        bool requireParticipants,
        out string actionCode)
    {
        CombatMoveTarget best = CombatMoveTarget.None;
        int bestParticipants = mode == AssaultRouteSelectionMode.MostParticipants ? -1 : int.MaxValue;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < context.AssaultRoutes.Count; i++)
        {
            CombatAiAssaultRoute route = context.AssaultRoutes[i];
            CreateAssaultRouteAdvanceCandidate(context, route, out _, out _, out CombatMoveTarget target);
            if (!IsUsableMove(context, target)) continue;

            int participants = CountAlliesUsingRoute(context, route);
            if (requireParticipants && participants <= 0) continue;

            float distance = HorizontalDistance(context.Owner.transform.position, target.Destination);
            bool isBetter = mode switch
            {
                AssaultRouteSelectionMode.LeastParticipants =>
                    participants < bestParticipants ||
                    participants == bestParticipants && distance < bestDistance,
                AssaultRouteSelectionMode.MostParticipants =>
                    participants > bestParticipants ||
                    participants == bestParticipants && distance < bestDistance,
                _ => distance < bestDistance,
            };
            if (!isBetter) continue;

            bestParticipants = participants;
            bestDistance = distance;
            best = target;
        }

        actionCode = best.HasAssaultRouteKey
            ? CombatAiMoveCode.AdvanceAssaultRoute
            : CombatAiMoveCode.AdvanceEnemyStone;
        return best;
    }

    private static int CountAlliesUsingRoute(CombatAiContext context, CombatAiAssaultRoute route)
    {
        int count = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || !ally.CanAct || !IsGatherableMovementRole(ally.MovementRole)) continue;
            if (IsAllyAssignedToRoute(context, ally, route)) count++;
        }

        return count;
    }

    private static bool IsAllyAssignedToRoute(
        CombatAiContext context,
        CombatCharacterIntel ally,
        CombatAiAssaultRoute route)
    {
        if (ally.HasAssaultRouteKey) return ally.AssaultRouteKey == route.RouteId;

        Vector3 point = ally.HasIntendedDestination
            ? ally.IntendedDestination
            : ally.CurrentPosition;
        float distance = DistanceToRoute(point, route.Corners);
        if (distance > AssaultRouteParticipantDistance) return false;

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.AssaultRoutes.Count; i++)
        {
            float candidateDistance = DistanceToRoute(point, context.AssaultRoutes[i].Corners);
            if (candidateDistance < nearestDistance) nearestDistance = candidateDistance;
        }

        return distance <= nearestDistance + 0.01f;
    }

    private static float DistanceToRoute(Vector3 point, IReadOnlyList<Vector3> corners)
    {
        if (corners == null || corners.Count == 0) return float.PositiveInfinity;
        if (corners.Count == 1) return HorizontalDistance(point, corners[0]);

        float bestDistance = float.PositiveInfinity;
        Vector2 position = new(point.x, point.z);
        for (int i = 1; i < corners.Count; i++)
        {
            Vector2 start = new(corners[i - 1].x, corners[i - 1].z);
            Vector2 end = new(corners[i].x, corners[i].z);
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float progress = lengthSquared > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(position - start, segment) / lengthSquared)
                : 0f;
            Vector2 closest = start + segment * progress;
            bestDistance = Mathf.Min(bestDistance, Vector2.Distance(position, closest));
        }

        return bestDistance;
    }
}
