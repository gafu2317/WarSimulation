using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

internal sealed class CombatAiNavigationQueryCache
{
    private readonly Dictionary<Vector3, CombatAiNavigationQuery> _queries = new();
    private readonly Dictionary<CombatAiRouteKey, float> _routeRisks = new();

    public bool IsActive { get; private set; }
    public int Count => _queries.Count;

    public void BeginTick()
    {
        _queries.Clear();
        _routeRisks.Clear();
        IsActive = true;
    }

    public void EndTick()
    {
        IsActive = false;
        _queries.Clear();
        _routeRisks.Clear();
    }

    public bool TryGet(Vector3 destination, out CombatAiNavigationQuery query)
    {
        query = default;
        return IsActive && _queries.TryGetValue(destination, out query);
    }

    public void Store(Vector3 destination, CombatAiNavigationQuery query)
    {
        if (IsActive) _queries[destination] = query;
    }

    public bool TryGetRouteRisk(Vector3 start, Vector3 destination, out float risk)
    {
        risk = 0f;
        return IsActive && _routeRisks.TryGetValue(new CombatAiRouteKey(start, destination), out risk);
    }

    public void StoreRouteRisk(Vector3 start, Vector3 destination, float risk)
    {
        if (IsActive) _routeRisks[new CombatAiRouteKey(start, destination)] = risk;
    }
}

internal readonly struct CombatAiRouteKey : System.IEquatable<CombatAiRouteKey>
{
    private readonly Vector3 _start;
    private readonly Vector3 _destination;

    public CombatAiRouteKey(Vector3 start, Vector3 destination)
    {
        _start = start;
        _destination = destination;
    }

    public bool Equals(CombatAiRouteKey other)
    {
        return _start.Equals(other._start) && _destination.Equals(other._destination);
    }

    public override bool Equals(object obj)
    {
        return obj is CombatAiRouteKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (_start.GetHashCode() * 397) ^ _destination.GetHashCode();
        }
    }
}

internal readonly struct CombatAiNavigationQuery
{
    public bool IsReachable { get; }
    public Vector3 Destination { get; }
    public NavMeshPath Path { get; }

    public CombatAiNavigationQuery(bool isReachable, Vector3 destination, NavMeshPath path)
    {
        IsReachable = isReachable;
        Destination = destination;
        Path = path;
    }
}
