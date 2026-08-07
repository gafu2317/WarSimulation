using UnityEngine;

/// <summary>
/// 戦闘中のスキル完了イベントを購読し、プロシージャル / Catalog VFX を再生する。
/// EffectTest のシーン内 Player とは別に、DontDestroyOnLoad の専用ホストを使う。
/// </summary>
public static class SkillVfxRuntimeBridge
{
    private const string CatalogResourcePath = "Combat/Vfx/SkillVfxCatalog";
    private const string RuntimeHostName = "SkillVfxRuntime";
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
        EnsurePlayer();
    }

    private static void OnSkillCompleted(CombatSkillActionResult result)
    {
        EnsurePlayer();
        _player?.PlayAction(result);
    }

    private static void EnsurePlayer()
    {
        if (_player != null) return;

        SkillVfxPlayer[] players = Object.FindObjectsByType<SkillVfxPlayer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            SkillVfxPlayer candidate = players[i];
            if (candidate != null && candidate.gameObject.name == RuntimeHostName)
            {
                _player = candidate;
                break;
            }
        }

        if (_player == null)
        {
            GameObject host = new(RuntimeHostName);
            Object.DontDestroyOnLoad(host);
            _player = host.AddComponent<SkillVfxPlayer>();
        }

        _player.SetCatalog(Resources.Load<SkillVfxCatalog>(CatalogResourcePath));
    }
}
