using UnityEngine;
using UnityEngine.AI;

public static class CombatAiNavigation
{
    public static bool IsReachable(Character owner, Vector3 destination)
    {
        if (owner == null) return false;

        NavMeshAgent agent = owner.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh) return true;

        CombatCharacterBody body = owner.GetComponent<CombatCharacterBody>();
        if (body != null) return body.CanReachDestination(destination);

        var path = new NavMeshPath();
        return agent.CalculatePath(destination, path) &&
            path.status == NavMeshPathStatus.PathComplete &&
            path.corners.Length >= 2;
    }

    public static bool IsReachableVia(Character owner, Vector3 waypoint, Vector3 destination)
    {
        return IsReachable(owner, waypoint) && IsReachable(owner, destination);
    }

    public static float EvaluateRouteRisk(CombatAiContext context, Vector3 start, Vector3 destination)
    {
        if (context == null || context.EnemyIntel.Count == 0) return 0f;

        var path = new NavMeshPath();
        Vector3[] corners = NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, path) && path.corners.Length >= 2
            ? path.corners
            : new[] { start, destination };
        float danger = 0f;
        int sampleCount = 0;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 from = corners[i - 1];
            Vector3 to = corners[i];
            float length = HorizontalDistance(from, to);
            int segmentSamples = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
            for (int sample = 1; sample <= segmentSamples; sample++)
            {
                Vector3 point = Vector3.Lerp(from, to, sample / (float)segmentSamples);
                danger += EvaluatePointDanger(context, point);
                sampleCount++;
            }
        }

        return sampleCount > 0 ? Mathf.Clamp(danger / sampleCount * 100f, 0f, 100f) : 0f;
    }

    private static float EvaluatePointDanger(CombatAiContext context, Vector3 point)
    {
        float danger = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition || !enemy.CanAct) continue;

            float dangerRadius = Mathf.Max(1.5f, enemy.WeaponRange + 2f);
            float distance = HorizontalDistance(point, enemy.KnownPosition);
            if (distance > dangerRadius || !HasLineOfSight(enemy, point)) continue;
            danger += Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(distance / dangerRadius));
        }

        int supportingAllies = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.CanAct && HorizontalDistance(point, ally.CurrentPosition) <= 6f) supportingAllies++;
        }

        return Mathf.Clamp01(danger / (1f + supportingAllies * 0.25f));
    }

    private static bool HasLineOfSight(CombatCharacterIntel enemy, Vector3 point)
    {
        Vector3 from = enemy.KnownPosition + Vector3.up;
        Vector3 to = point + Vector3.up;
        if (!Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        Character hitCharacter = hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
        return hitCharacter == enemy.Character;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
