public class Rosary : WeaponBase
{
    public override float Range => 5f;
    public override int BasePower => 8;
    public override float CooldownSeconds => 1.2f;
    public override CombatStat ScalingStat => CombatStat.FAI;
}
