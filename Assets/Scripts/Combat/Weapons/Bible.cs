public class Bible : WeaponBase
{
    public override float Range => 6f;
    public override int BasePower => 10;
    public override float CooldownSeconds => 1.6f;
    public override CombatStat ScalingStat => CombatStat.FAI;
}
