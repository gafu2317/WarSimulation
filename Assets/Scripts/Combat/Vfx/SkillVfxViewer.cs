using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// EffectTest 専用。カタログの Prefab を手動再生して見た目を確認する。
/// </summary>
public sealed class SkillVfxViewer : MonoBehaviour
{
    [SerializeField] private SkillVfxPlayer _player;
    [SerializeField] private SkillVfxCatalog _catalog;
    [SerializeField] private Transform _caster;
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _point;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _skillText;
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _clearButton;
    [SerializeField] private Button _slowButton;
    [SerializeField] private Button _normalButton;

    private SkillId[] _skillIds = Array.Empty<SkillId>();
    private int _index;

    private void OnEnable()
    {
        BindButtons(true);
        RebuildSkillList();
        RefreshLabels("Ready");
    }

    private void OnDisable()
    {
        BindButtons(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
        {
            Step(-1);
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
        {
            Step(1);
        }
        else if (keyboard.spaceKey.wasPressedThisFrame)
        {
            PlayCurrent();
        }
        else if (keyboard.cKey.wasPressedThisFrame)
        {
            ClearEffects();
        }
        else if (keyboard.digit1Key.wasPressedThisFrame)
        {
            SetTimeScale(0.25f);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            SetTimeScale(1f);
        }
    }

    private void BindButtons(bool bind)
    {
        Bind(_prevButton, OnPrev, bind);
        Bind(_nextButton, OnNext, bind);
        Bind(_playButton, OnPlay, bind);
        Bind(_clearButton, OnClear, bind);
        Bind(_slowButton, OnSlow, bind);
        Bind(_normalButton, OnNormal, bind);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action, bool bind)
    {
        if (button == null) return;
        if (bind) button.onClick.AddListener(action);
        else button.onClick.RemoveListener(action);
    }

    private void OnPrev() => Step(-1);
    private void OnNext() => Step(1);
    private void OnPlay() => PlayCurrent();
    private void OnClear() => ClearEffects();
    private void OnSlow() => SetTimeScale(0.25f);
    private void OnNormal() => SetTimeScale(1f);

    private void RebuildSkillList()
    {
        if (_catalog == null || _catalog.Entries.Count == 0)
        {
            _skillIds = Array.Empty<SkillId>();
            _index = 0;
            return;
        }

        var ids = new SkillId[_catalog.Entries.Count];
        int count = 0;
        for (int i = 0; i < _catalog.Entries.Count; i++)
        {
            SkillVfxCatalog.Entry entry = _catalog.Entries[i];
            if (entry == null || entry.SkillId == SkillId.None) continue;
            ids[count++] = entry.SkillId;
        }

        if (count == 0)
        {
            _skillIds = Array.Empty<SkillId>();
            _index = 0;
            return;
        }

        _skillIds = new SkillId[count];
        Array.Copy(ids, _skillIds, count);
        _index = Mathf.Clamp(_index, 0, _skillIds.Length - 1);
    }

    private void Step(int delta)
    {
        if (_skillIds.Length == 0)
        {
            RebuildSkillList();
            if (_skillIds.Length == 0)
            {
                RefreshLabels("カタログに SkillId がありません。");
                return;
            }
        }

        _index = (_index + delta + _skillIds.Length) % _skillIds.Length;
        RefreshLabels("Selected");
    }

    private void PlayCurrent()
    {
        if (_player == null)
        {
            RefreshLabels("SkillVfxPlayer が未設定です。");
            return;
        }

        if (_skillIds.Length == 0)
        {
            RebuildSkillList();
            if (_skillIds.Length == 0)
            {
                RefreshLabels("カタログに再生対象がありません。");
                return;
            }
        }

        SkillId skillId = _skillIds[_index];
        Vector3 selfPos = _caster != null ? _caster.position : Vector3.zero;
        Vector3? targetPos = _target != null ? _target.position : null;
        Vector3? pointPos = _point != null ? _point.position : null;
        bool ok = _player.TryPlay(skillId, selfPos, targetPos, pointPos, out string message);
        RefreshLabels(ok ? message : "失敗: " + message);
    }

    private void ClearEffects()
    {
        _player?.ClearAll();
        RefreshLabels("Cleared");
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        RefreshLabels($"timeScale={scale:0.##}");
    }

    private void RefreshLabels(string status)
    {
        if (_skillText != null)
        {
            if (_skillIds.Length == 0)
            {
                _skillText.text = "Skill: (none)";
            }
            else
            {
                SkillId id = _skillIds[_index];
                string prefabName = "-";
                if (_catalog != null && _catalog.TryGetEntry(id, out SkillVfxCatalog.Entry entry) && entry.Prefab != null)
                {
                    prefabName = entry.Prefab.name;
                }

                _skillText.text = $"Skill: {id}  [{_index + 1}/{_skillIds.Length}]\nPrefab: {prefabName}";
            }
        }

        if (_statusText != null)
        {
            _statusText.text = status + "\n←/→ or A/D: 選択  Space: 再生  C: 消去  1:×0.25  2:×1";
        }
    }
}
