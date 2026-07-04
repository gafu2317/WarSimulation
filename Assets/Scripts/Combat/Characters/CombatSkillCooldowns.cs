using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
public sealed class CombatSkillCooldowns : MonoBehaviour
{
    private readonly Dictionary<string, float> _nextReadyTimeByKey = new();

    public bool IsReady(SkillBase skill)
    {
        if (skill == null) return false;
        if (skill.CooldownSeconds <= 0f) return true;

        string key = skill.CooldownKey;
        if (string.IsNullOrEmpty(key)) return true;
        if (!_nextReadyTimeByKey.TryGetValue(key, out float nextReadyTime)) return true;

        return Time.time >= nextReadyTime;
    }

    public float GetRemainingSeconds(SkillBase skill)
    {
        if (skill == null || skill.CooldownSeconds <= 0f) return 0f;

        string key = skill.CooldownKey;
        if (string.IsNullOrEmpty(key)) return 0f;
        if (!_nextReadyTimeByKey.TryGetValue(key, out float nextReadyTime)) return 0f;

        return Mathf.Max(0f, nextReadyTime - Time.time);
    }

    public void StartCooldown(SkillBase skill)
    {
        if (skill == null || skill.CooldownSeconds <= 0f) return;

        string key = skill.CooldownKey;
        if (string.IsNullOrEmpty(key)) return;

        _nextReadyTimeByKey[key] = Time.time + skill.CooldownSeconds;
    }

    public void ResetCooldown(SkillBase skill)
    {
        if (skill == null) return;

        string key = skill.CooldownKey;
        if (string.IsNullOrEmpty(key)) return;

        _nextReadyTimeByKey.Remove(key);
    }

    public void ClearAll()
    {
        _nextReadyTimeByKey.Clear();
    }
}
