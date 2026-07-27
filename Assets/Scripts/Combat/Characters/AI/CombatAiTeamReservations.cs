using System.Collections.Generic;

public sealed class CombatAiTeamReservations
{
    private readonly Dictionary<Character, CombatAiPlan> _plans = new();
    private readonly List<ReservedAmount> _damage = new();
    private readonly List<ReservedAmount> _healing = new();

    public void Clear()
    {
        _plans.Clear();
        _damage.Clear();
        _healing.Clear();
    }

    public bool TryGetPlan(Character character, out CombatAiPlan plan)
    {
        plan = CombatAiPlan.None;
        return character != null && _plans.TryGetValue(character, out plan);
    }

    public void Reserve(Character owner, CombatAiPlan plan)
    {
        if (owner == null) return;

        _plans[owner] = plan;
        if (plan.Skill == null) return;

        SkillExecutionContext context = plan.SkillContext.Capture(owner);
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            if (target == null || target.Health == null || !target.Health.IsAlive) continue;

            int damage = plan.Skill.EstimateDamage(owner, context, target);
            if (damage > 0)
            {
                _damage.Add(new ReservedAmount(owner.Team, target, damage));
            }

            int healing = plan.Skill.EstimateHealing(owner, context, target);
            if (healing > 0)
            {
                _healing.Add(new ReservedAmount(owner.Team, target, healing));
            }
        }
    }

    public void AppendPendingDamage(
        CombatTeam observerTeam,
        List<CombatAiPendingDamage> allyDamage,
        List<CombatAiPendingDamage> enemyDamage)
    {
        for (int i = 0; i < _damage.Count; i++)
        {
            ReservedAmount reservation = _damage[i];
            List<CombatAiPendingDamage> destination = reservation.SourceTeam == observerTeam
                ? allyDamage
                : enemyDamage;
            destination.Add(new CombatAiPendingDamage(reservation.Target, reservation.Amount));
        }
    }

    public void AppendPendingHealing(
        CombatTeam observerTeam,
        List<CombatAiPendingHealing> allyHealing,
        List<CombatAiPendingHealing> enemyHealing)
    {
        for (int i = 0; i < _healing.Count; i++)
        {
            ReservedAmount reservation = _healing[i];
            List<CombatAiPendingHealing> destination = reservation.SourceTeam == observerTeam
                ? allyHealing
                : enemyHealing;
            destination.Add(new CombatAiPendingHealing(reservation.Target, reservation.Amount));
        }
    }

    private readonly struct ReservedAmount
    {
        public CombatTeam SourceTeam { get; }
        public Character Target { get; }
        public int Amount { get; }

        public ReservedAmount(CombatTeam sourceTeam, Character target, int amount)
        {
            SourceTeam = sourceTeam;
            Target = target;
            Amount = amount;
        }
    }
}
