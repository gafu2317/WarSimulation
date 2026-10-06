using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Combat/Audio Settings")]
public sealed class CombatAudioSettings : ScriptableObject
{
    [Serializable]
    public sealed class SkillSound
    {
        public SkillId Skill;
        public AudioClip Clip;
    }

    public TMP_FontAsset Font;
    public AudioClip BattleBgm;
    public List<SkillSound> Skills = new();
    public List<AudioClip> Library = new();
    [Range(0, 1)] public float BgmVolume = 0.5f;
    [Range(0, 1)] public float SeVolume = 0.8f;

    public AudioClip GetClip(SkillId skill) => Skills.Find(x => x.Skill == skill)?.Clip;

    public void SetClip(SkillId skill, AudioClip clip)
    {
        if (skill == SkillId.None) BattleBgm = clip;
        else
        {
            Skills.RemoveAll(x => x.Skill == skill);
            if (clip != null) Skills.Add(new SkillSound { Skill = skill, Clip = clip });
        }
        if (clip != null && !Library.Contains(clip)) Library.Add(clip);
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
