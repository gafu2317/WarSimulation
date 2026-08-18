using UnityEngine;

public enum CombatMoveTargetKind
{
    None = 0,
    Position = 1,
    Character = 2,
}

public readonly struct CombatMoveTarget
{
    public static readonly CombatMoveTarget None = new CombatMoveTarget(CombatMoveTargetKind.None);

    public CombatMoveTargetKind Kind { get; }
    public Vector3 Destination { get; }
    public Character TargetCharacter { get; }
    public bool HasAssaultRouteKey { get; }
    public string AssaultRouteKey { get; }
    public bool HasDestination => Kind == CombatMoveTargetKind.Position || Kind == CombatMoveTargetKind.Character;

    private CombatMoveTarget(CombatMoveTargetKind kind)
    {
        Kind = kind;
        Destination = default;
        TargetCharacter = null;
        HasAssaultRouteKey = false;
        AssaultRouteKey = null;
    }

    private CombatMoveTarget(Vector3 destination, bool hasAssaultRouteKey, string assaultRouteKey)
    {
        Kind = CombatMoveTargetKind.Position;
        Destination = destination;
        TargetCharacter = null;
        HasAssaultRouteKey = hasAssaultRouteKey;
        AssaultRouteKey = assaultRouteKey;
    }

    private CombatMoveTarget(Character target)
    {
        Kind = CombatMoveTargetKind.Character;
        TargetCharacter = target;
        Destination = target != null ? target.transform.position : default;
        HasAssaultRouteKey = false;
        AssaultRouteKey = null;
    }

    public static CombatMoveTarget ForPosition(Vector3 destination)
    {
        return new CombatMoveTarget(destination, false, null);
    }

    public static CombatMoveTarget ForPosition(Vector3 destination, string assaultRouteKey)
    {
        return new CombatMoveTarget(destination, true, assaultRouteKey);
    }

    public static CombatMoveTarget ForCharacter(Character target)
    {
        return target != null ? new CombatMoveTarget(target) : None;
    }
}
