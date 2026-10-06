using UnityEngine;

public sealed class CombatAudioPlayer : MonoBehaviour
{
    private static CombatAudioPlayer _instance;
    private AudioSource _bgm;
    private AudioSource _se;
    private AudioSource _preview;
    public CombatAudioSettings Settings { get; private set; }

    public static CombatAudioPlayer Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                var host = new GameObject("CombatAudio");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<CombatAudioPlayer>();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        Settings = Resources.Load<CombatAudioSettings>("Combat/Audio/CombatAudioSettings");
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.loop = true;
        _se = gameObject.AddComponent<AudioSource>();
        _preview = gameObject.AddComponent<AudioSource>();
        _bgm.playOnAwake = _se.playOnAwake = _preview.playOnAwake = false;
        CombatSkillActionEvents.Completed += OnSkillCompleted;
    }

    private void OnDestroy() => CombatSkillActionEvents.Completed -= OnSkillCompleted;

    public void StartBattle()
    {
        StopBattle();
        if (Settings == null) return;
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();
        _bgm.clip = Settings.BattleBgm;
        _bgm.volume = Settings.BgmVolume;
        if (_bgm.clip != null) _bgm.Play();
    }

    public void StopBattle()
    {
        _bgm.Stop();
        _se.Stop();
        _preview.Stop();
    }

    public void Preview(AudioClip clip, bool bgm)
    {
        _preview.Stop();
        _preview.clip = clip;
        _preview.loop = bgm;
        _preview.volume = bgm ? Settings.BgmVolume : Settings.SeVolume;
        if (clip != null) _preview.Play();
    }

    public void StopPreview() => _preview.Stop();

    private void OnSkillCompleted(CombatSkillActionResult result)
    {
        if (!CombatBattleFlow.IsRunning || Settings == null ||
            result.Outcome == CombatSkillActionOutcome.Failed) return;
        AudioClip clip = Settings.GetClip(result.Action.SkillId);
        if (clip != null) _se.PlayOneShot(clip, Settings.SeVolume);
    }
}
