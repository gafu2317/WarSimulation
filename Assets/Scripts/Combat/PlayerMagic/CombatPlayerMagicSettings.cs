using UnityEngine;

public enum CombatPlayerMagicKind { Area, AllInvulnerable }

[CreateAssetMenu(menuName = "WarSimulation/Combat/Player Magic Settings")]
public sealed class CombatPlayerMagicSettings : ScriptableObject
{
    public float AreaRadius = 10;
    public int AreaDamage = 40;
    public float InvulnerableSeconds = 10;
    public Color AreaColor = new(.2f, .65f, 1f);
    private static CombatPlayerMagicSettings _default;
    public static CombatPlayerMagicSettings Default
    {
        get
        {
            if (_default == null)
            {
                _default = Resources.Load<CombatPlayerMagicSettings>("Combat/PlayerMagicSettings");
                if (_default == null)
                {
                    _default = CreateInstance<CombatPlayerMagicSettings>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return _default;
        }
    }
    public static float CastSeconds(SkillId id) => id == SkillId.Player_AreaMagic ? new WandGodsHandSkill().CastTimeSeconds : 0;
    public static bool IsPlayerMagic(SkillId id) => id is SkillId.Player_AreaMagic or SkillId.Player_AllInvulnerable;
    public static string Label(SkillId id) => id == SkillId.Player_AreaMagic ? "広範囲魔法" : "全員無敵";
}
