public class Grimoire : WeaponBase
{
    public override float Range => 7f;
    public override int BasePower => 14;
    public override float CooldownSeconds => 2f;
    public override CombatStat ScalingStat => CombatStat.INT;
}
