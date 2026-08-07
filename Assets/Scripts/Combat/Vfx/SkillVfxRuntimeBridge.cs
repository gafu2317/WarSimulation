using UnityEngine;

public static class SkillVfxRuntimeBridge
{
    private const string CatalogResourcePath = "Combat/Vfx/SkillVfxCatalog";
    private static SkillVfxPlayer _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        _player = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatSkillActionEvents.Completed += OnSkillCompleted;

        _player = Object.FindAnyObjectByType<SkillVfxPlayer>();
        if (_player == null)
        {
            GameObject host = new("SkillVfxRuntime");
            Object.DontDestroyOnLoad(host);
            _player = host.AddComponent<SkillVfxPlayer>();
        }
        _player.SetCatalog(Resources.Load<SkillVfxCatalog>(CatalogResourcePath));
    }

    private static void OnSkillCompleted(CombatSkillActionResult result)
    {
        if (_player == null)
        {
            Initialize();
        }

        _player?.PlayAction(result);
    }
}
