public class Shield : WeaponBase
{
    public override float Range => 1.8f;
    public override int BasePower => 6;
    public override float CooldownSeconds => 1.3f;
    public override CombatStat ScalingStat => CombatStat.STR;
}
