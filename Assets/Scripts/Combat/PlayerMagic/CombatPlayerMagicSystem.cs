using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatPlayerMagicRequest
{
    public CombatPlayerMagicRequest(CombatPlayerMagicKind kind, Vector3 center, float radius, int damage, float seconds)
    { Kind = kind; Center = center; Radius = radius; Damage = damage; Seconds = seconds; }
    public CombatPlayerMagicKind Kind { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public int Damage { get; }
    public float Seconds { get; }
    public SkillId SkillId => Kind == CombatPlayerMagicKind.Area ? SkillId.Player_AreaMagic : SkillId.Player_AllInvulnerable;
    public static bool PositiveFinite(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
    public bool IsValid => Kind == CombatPlayerMagicKind.Area
        ? PositiveFinite(Radius) && Damage >= 1 && float.IsFinite(Center.x) && float.IsFinite(Center.y) && float.IsFinite(Center.z)
        : Kind == CombatPlayerMagicKind.AllInvulnerable && PositiveFinite(Seconds);
}

public sealed class CombatPlayerMagicSystem
{
    public const string InvulnerableKey = "PlayerAllInvulnerable";
    private readonly CombatCharacterSystem _characters;
    public CombatPlayerMagicSystem(CombatCharacterSystem characters) => _characters = characters;

    public List<Character> Execute(CombatPlayerMagicRequest request)
    {
        var targets = new List<Character>();
        if (!request.IsValid || _characters == null) return targets;
        var source = new CombatEffectSource(null, request.SkillId, CombatPlayerMagicSettings.Label(request.SkillId));
        if (request.Kind == CombatPlayerMagicKind.Area)
        {
            foreach (Character character in _characters.EnemyCharacters)
            {
                if (character == null || !character.gameObject.activeInHierarchy || !character.Health.IsTargetable) continue;
                Vector3 delta = character.transform.position - request.Center;
                if (delta.x * delta.x + delta.z * delta.z > (double)request.Radius * request.Radius) continue;
                targets.Add(character);
                character.Health.TakeDamage(request.Damage, source);
            }
        }
        else
        {
            AddInvulnerable(_characters.AllyCharacters, request.Seconds, source, targets);
            AddInvulnerable(_characters.EnemyCharacters, request.Seconds, source, targets);
        }
        return targets;
    }

    public static int CountInvulnerabilityTargets(CombatCharacterSystem characters)
    {
        if (characters == null) return 0;
        int count = 0;
        foreach (Character character in characters.AllyCharacters)
            if (CanReceiveInvulnerability(character)) count++;
        foreach (Character character in characters.EnemyCharacters)
            if (CanReceiveInvulnerability(character)) count++;
        return count;
    }

    public static bool CanReceiveInvulnerability(Character character) => character != null && character.gameObject.activeInHierarchy &&
        (character.Health.IsAlive || character.Health.LifeState == LifeState.Retreating && !character.Health.IsWithdrawn);
    private static void AddInvulnerable(IReadOnlyList<Character> characters, float seconds, CombatEffectSource source, List<Character> targets)
    {
        foreach (Character character in characters)
        {
            if (!CanReceiveInvulnerability(character)) continue;
            character.StatusEffects.ApplyInvulnerable(seconds, InvulnerableKey, source);
            targets.Add(character);
        }
    }

    public void Clear()
    {
        if (_characters == null) return;
        foreach (Character character in _characters.AllyCharacters)
            if (character != null) character.StatusEffects.ClearEffect(InvulnerableKey);
        foreach (Character character in _characters.EnemyCharacters)
            if (character != null) character.StatusEffects.ClearEffect(InvulnerableKey);
    }
}
