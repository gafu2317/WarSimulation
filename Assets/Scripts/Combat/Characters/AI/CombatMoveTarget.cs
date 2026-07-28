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
    public int AssaultRouteKey { get; }
    public bool HasDestination => Kind == CombatMoveTargetKind.Position || Kind == CombatMoveTargetKind.Character;

    private CombatMoveTarget(CombatMoveTargetKind kind)
    {
        Kind = kind;
        Destination = default;
        TargetCharacter = null;
        HasAssaultRouteKey = false;
        AssaultRouteKey = 0;
    }

    private CombatMoveTarget(Vector3 destination, bool hasAssaultRouteKey, int assaultRouteKey)
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
        AssaultRouteKey = 0;
    }

    public static CombatMoveTarget ForPosition(Vector3 destination)
    {
        return new CombatMoveTarget(destination, false, 0);
    }

    public static CombatMoveTarget ForPosition(Vector3 destination, int assaultRouteKey)
    {
        return new CombatMoveTarget(destination, true, assaultRouteKey);
    }

    public static CombatMoveTarget ForCharacter(Character target)
    {
        return target != null ? new CombatMoveTarget(target) : None;
    }
}
