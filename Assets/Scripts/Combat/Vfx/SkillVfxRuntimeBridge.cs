using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 戦闘中の詠唱・完了・キャンセルをVFXへ接続する。
/// EffectTest のシーン内 Player とは別に、DontDestroyOnLoad の専用ホストを使う。
/// </summary>
public static class SkillVfxRuntimeBridge
{
    private const string RuntimeHostName = "SkillVfxRuntime";
    private static SkillVfxPlayer _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        CombatSkillActionEvents.Started -= OnSkillStarted;
        CombatSkillActionEvents.Cancelled -= OnSkillCancelled;
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        _player = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!Application.isPlaying) return;

        CombatSkillActionEvents.Started -= OnSkillStarted;
        CombatSkillActionEvents.Cancelled -= OnSkillCancelled;
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatSkillActionEvents.Started += OnSkillStarted;
        CombatSkillActionEvents.Cancelled += OnSkillCancelled;
        CombatSkillActionEvents.Completed += OnSkillCompleted;
        EnsurePlayer();
    }

    private static void OnSkillStarted(CombatSkillActionInfo action)
    {
        if (!Application.isPlaying) return;

        EnsurePlayer();
        if (_player != null) _player.PlayCast(action);
    }

    private static void OnSkillCancelled(CombatSkillActionResult result)
    {
        if (Application.isPlaying && _player != null) _player.CancelCast(result);
    }

    private static void OnSkillCompleted(CombatSkillActionResult result)
    {
        if (!Application.isPlaying) return;

        EnsurePlayer();
        if (_player != null) _player.PlayAction(result);
    }

    public static long BeginPlayerMagicCast(CombatPlayerMagicRequest request)
    {
        if (!Application.isPlaying) return 0;
        EnsurePlayer();
        return _player != null ? _player.BeginPlayerMagicCast(request) : 0;
    }

    public static void CancelPlayerMagicCast(long id)
    {
        if (_player != null) _player.CancelPlayerMagicCast(id);
    }

    public static void PlayPlayerMagic(CombatPlayerMagicRequest request, IReadOnlyList<Character> targets)
    {
        if (!Application.isPlaying) return;
        EnsurePlayer();
        if (_player != null) _player.PlayPlayerMagic(request, targets);
    }

    private static void EnsurePlayer()
    {
        if (!Application.isPlaying) return;

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

    }
}
