using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Combat/Audio Settings")]
public sealed class CombatAudioSettings : ScriptableObject
{
    public enum BgmScene { Formation, Battle, Victory, Defeat }

    [Serializable]
    public sealed class SceneMusic
    {
        public BgmScene Scene;
        public AudioClip Clip;
        [Range(0, 1)] public float Volume = 1f;
    }

    public List<SceneMusic> SceneBgm = new();

    public AudioClip GetBgm(BgmScene scene) => scene == BgmScene.Battle
        ? BattleBgm : SceneBgm.Find(x => x.Scene == scene)?.Clip;

    public float GetBgmVolume(BgmScene scene) => scene == BgmScene.Battle
        ? BattleBgmVolume : SceneBgm.Find(x => x.Scene == scene)?.Volume ?? 1f;

    public void SetBgm(BgmScene scene, AudioClip clip)
    {
        if (scene == BgmScene.Battle) BattleBgm = clip;
        else GetSceneMusic(scene).Clip = clip;
        if (clip != null && !Library.Contains(clip)) Library.Add(clip);
        Save();
    }

    public void SetBgmVolume(BgmScene scene, float volume)
    {
        if (scene == BgmScene.Battle) BattleBgmVolume = Mathf.Clamp01(volume);
        else GetSceneMusic(scene).Volume = Mathf.Clamp01(volume);
        Save();
    }

    private SceneMusic GetSceneMusic(BgmScene scene)
    {
        var music = SceneBgm.Find(x => x.Scene == scene);
        if (music == null) { music = new SceneMusic { Scene = scene }; SceneBgm.Add(music); }
        return music;
    }

    [Serializable]
    public sealed class SkillSound
    {
        public SkillId Skill;
        public AudioClip Clip;
        [Range(0, 1)] public float Volume = 1f;
    }

    [Serializable]
    public sealed class ClipVolume
    {
        public AudioClip Clip;
        [Range(0, 1)] public float Volume = 1f;
    }

    public List<ClipVolume> ClipVolumes = new();
    [Range(0, 1)] public float BattleBgmVolume = 1f;
    public TMP_FontAsset Font;
    public AudioClip BattleBgm;
    public List<SkillSound> Skills = new();
    public List<AudioClip> Library = new();
    [Range(0, 1)] public float BgmVolume = 0.5f;
    [Range(0, 1)] public float SeVolume = 0.8f;

    public bool InitializePlayerMagicSounds()
    {
        bool changed = false;
        foreach (var pair in new[] { (SkillId.Player_AreaMagic, SkillId.Wand_GodsHand),
            (SkillId.Player_AllInvulnerable, SkillId.Bible_Invulnerable) })
        {
            if (Skills.Exists(sound => sound.Skill == pair.Item1)) continue;
            Skills.Add(new SkillSound { Skill = pair.Item1, Clip = GetClip(pair.Item2), Volume = GetVolume(pair.Item2) });
            changed = true;
        }
        return changed;
    }

    public AudioClip GetClip(SkillId skill) => Skills.Find(x => x.Skill == skill)?.Clip;

    public void SetClip(SkillId skill, AudioClip clip)
    {
        if (skill == SkillId.None) BattleBgm = clip;
        else
        {
            var sound = Skills.Find(x => x.Skill == skill);
            if (sound == null) Skills.Add(new SkillSound { Skill = skill, Clip = clip });
            else sound.Clip = clip;
        }
        if (clip != null && !Library.Contains(clip)) Library.Add(clip);
        Save();
    }

    public float GetVolume(SkillId skill) => skill == SkillId.None ? BattleBgmVolume : Skills.Find(x => x.Skill == skill)?.Volume ?? 1f;
    public float GetClipVolume(AudioClip clip) => ClipVolumes.Find(x => x.Clip == clip)?.Volume ?? 1f;

    public void SetVolume(SkillId skill, float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (skill == SkillId.None) BattleBgmVolume = volume;
        else
        {
            var sound = Skills.Find(x => x.Skill == skill);
            if (sound == null) Skills.Add(new SkillSound { Skill = skill, Volume = volume });
            else sound.Volume = volume;
        }
        Save();
    }

    public void SetClipVolume(AudioClip clip, float volume)
    {
        var entry = ClipVolumes.Find(x => x.Clip == clip);
        if (entry == null) ClipVolumes.Add(new ClipVolume { Clip = clip, Volume = Mathf.Clamp01(volume) });
        else entry.Volume = Mathf.Clamp01(volume);
        Save();
    }

    public void Save()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
    }
}
