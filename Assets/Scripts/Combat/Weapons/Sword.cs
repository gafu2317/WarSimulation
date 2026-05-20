public class Sword : WeaponBase
{
    public override float Range => 2f;
    public override int BasePower => 12;
    public override float CooldownSeconds => 1f;
    public override CombatStat ScalingStat => CombatStat.STR;
}
