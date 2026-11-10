using UnityEngine;

public sealed class CombatAudioPlayer : MonoBehaviour
{
    private static CombatAudioPlayer _instance;
    private static bool _quitting;
    public static CombatAudioPlayer Existing => _instance != null ? _instance : null;
    public bool IsPreviewPlaying => _preview != null && _preview.isPlaying;
    private AudioSource _bgm;
    private AudioSource _se;
    private AudioSource _preview;
    private bool _previewBgm;
    private SkillId? _previewSkill;
    private CombatAudioSettings.BgmScene? _previewScene;
    public CombatAudioSettings Settings { get; private set; }

    public static CombatAudioPlayer Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying && !_quitting)
            {
                var host = new GameObject("CombatAudio");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<CombatAudioPlayer>();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitting = false;
    }

    private void Awake()
    {
        Settings = Resources.Load<CombatAudioSettings>("Combat/Audio/CombatAudioSettings");
        if (Settings != null) Settings.InitializePlayerMagicSounds();
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.loop = true;
        _se = gameObject.AddComponent<AudioSource>();
        _preview = gameObject.AddComponent<AudioSource>();
        _bgm.playOnAwake = _se.playOnAwake = _preview.playOnAwake = false;
        CombatSkillActionEvents.Completed += OnSkillCompleted;
        CombatDamageEvents.Resolved += OnDamageResolved;
    }

    private void OnApplicationQuit() => _quitting = true;

    private void OnDestroy()
    {
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatDamageEvents.Resolved -= OnDamageResolved;
        if (_instance == this) _instance = null;
    }

    public void StartBattle()
    {
        StopBattle();
        if (Settings == null) return;
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();
        _bgm.clip = Settings.BattleBgm;
        _bgm.volume = Settings.BgmVolume * Settings.GetVolume(SkillId.None) * Settings.GetClipVolume(_bgm.clip);
        if (_bgm.clip != null) _bgm.Play();
    }

    public void StopBattle()
    {
        _bgm.Stop();
        _se.Stop();
        _preview.Stop();
    }

    public void Preview(AudioClip clip, bool bgm, SkillId? skill = null, CombatAudioSettings.BgmScene? scene = null)
    {
        _preview.Stop();
        _preview.clip = clip;
        _preview.loop = bgm;
        _previewBgm = bgm;
        _previewSkill = skill;
        _previewScene = scene;
        RefreshVolumes();
        if (clip != null) _preview.Play();
    }

    public void RefreshVolumes()
    {
        if (Settings == null) return;
        _bgm.volume = Settings.BgmVolume * Settings.GetVolume(SkillId.None) * Settings.GetClipVolume(_bgm.clip);
        _preview.volume = (_previewBgm ? Settings.BgmVolume : Settings.SeVolume) *
            (_previewScene.HasValue ? Settings.GetBgmVolume(_previewScene.Value) :
                _previewSkill.HasValue ? Settings.GetVolume(_previewSkill.Value) : 1f) * Settings.GetClipVolume(_preview.clip);
    }

    public void StopPreview() => _preview.Stop();

    private void OnSkillCompleted(CombatSkillActionResult result)
    {
        if (!CombatBattleFlow.IsRunning || Settings == null ||
            result.Outcome == CombatSkillActionOutcome.Failed) return;
        SkillId skill = result.Action.SkillId;
        if (skill is SkillId.Bible_Gotsume or SkillId.Shield_ShoulderGuard or SkillId.Shield_IronWall) return;
        PlaySkillSound(skill);
    }

    private void OnDamageResolved(CombatDamageEvent damage)
    {
        if (damage.WasPrevented && damage.PreventionSource.SkillId is
            SkillId.Shield_ShoulderGuard or SkillId.Shield_IronWall)
            PlaySkillSound(damage.PreventionSource.SkillId);
        else if (!damage.WasPrevented && damage.AttackSource.IsReactiveDamage &&
            damage.AttackSource.SkillId == SkillId.Bible_Gotsume)
            PlaySkillSound(SkillId.Bible_Gotsume);
    }

    public void PlayPlayerMagicSound(SkillId skill)
    {
        if (CombatPlayerMagicSettings.IsPlayerMagic(skill)) PlaySkillSound(skill);
    }

    private void PlaySkillSound(SkillId skill)
    {
        if (!CombatBattleFlow.IsRunning || Settings == null) return;
        AudioClip clip = Settings.GetClip(skill);
        if (clip != null) _se.PlayOneShot(clip, Settings.SeVolume * Settings.GetVolume(skill) * Settings.GetClipVolume(clip));
    }
}
