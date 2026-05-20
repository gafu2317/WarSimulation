public class WeaponBase
{
    public static readonly WeaponBase Unarmed = new WeaponBase();

    // 品質
    public int Quality { private set; get; }

    // 使用可能回数
    public int UsableCount { private set; get; } = 5;

    public virtual float Range => 1.5f;
    public virtual int BasePower => 4;
    public virtual float CooldownSeconds => 1.2f;
    public virtual CombatStat ScalingStat => CombatStat.STR;
}
