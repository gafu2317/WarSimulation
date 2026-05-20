public class Wand : WeaponBase
{
    public override float Range => 8f;
    public override int BasePower => 10;
    public override float CooldownSeconds => 1.4f;
    public override CombatStat ScalingStat => CombatStat.INT;
}
