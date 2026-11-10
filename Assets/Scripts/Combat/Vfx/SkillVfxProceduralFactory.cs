using UnityEngine;

public static class SkillVfxProceduralFactory
{
    public static bool TryCreate(SkillId skillId, Vector3 self, Vector3? target, Vector3? point,
        Transform parent, out GameObject root, out float lifetime)
    {
        SkillBase skill = CombatSkillFactory.Create(skillId);
        if (skill == null && !CombatPlayerMagicSettings.IsPlayerMagic(skillId)) { root = null; lifetime = 0; return false; }
        root = new GameObject($"Vfx_{skillId}");
        root.transform.SetParent(parent, false);
        var effect = root.AddComponent<SkillVfxEffect>();
        effect.Prepare(skillId, self, target ?? self, point ?? target ?? self, radius: skill != null ? skill.AreaRadius : CombatPlayerMagicSettings.Default.AreaRadius);
        lifetime = effect.Lifetime;
        return true;
    }
}
