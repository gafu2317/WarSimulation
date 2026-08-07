using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// EffectTest 専用。全 SkillId の Prefab またはプロシージャル VFX を手動再生する。
/// </summary>
public sealed class SkillVfxViewer : MonoBehaviour
{
    private static readonly WeaponKind[] WeaponOrder =
    {
        WeaponKind.Sword,
        WeaponKind.Shield,
        WeaponKind.Wand,
        WeaponKind.Grimoire,
        WeaponKind.Bible,
        WeaponKind.Rosary,
    };

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
    private readonly Dictionary<SkillId, WeaponKind> _weaponBySkill = new();
    private int _index;

    private void OnEnable()
    {
        ApplyUiLayout();
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

    private void ApplyUiLayout()
    {
        ApplyButtonLayout();
        ApplyInfoTextLayout();
    }

    private void ApplyButtonLayout()
    {
        if (_prevButton == null) return;

        RectTransform row = _prevButton.transform.parent as RectTransform;
        Canvas canvas = row != null ? row.GetComponentInParent<Canvas>() : null;
        if (row == null || canvas == null) return;

        row.SetParent(canvas.transform, false);
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.right;
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = Vector2.zero;
        row.offsetMin = new Vector2(24f, 24f);
        row.offsetMax = new Vector2(-24f, 104f);
    }

    private void ApplyInfoTextLayout()
    {
        if (_skillText == null || _statusText == null) return;

        RectTransform skill = _skillText.rectTransform;
        RectTransform status = _statusText.rectTransform;
        RectTransform panel = skill.parent as RectTransform;
        if (panel == null) return;

        // ButtonRow を外したあと、3行スキル + 2行ステータスが収まる高さにする。
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -24f);
        panel.sizeDelta = new Vector2(-48f, 210f);

        skill.anchorMin = new Vector2(0f, 0.4f);
        skill.anchorMax = Vector2.one;
        skill.pivot = new Vector2(0.5f, 1f);
        skill.offsetMin = new Vector2(16f, 4f);
        skill.offsetMax = new Vector2(-16f, -12f);
        _skillText.verticalAlignment = VerticalAlignmentOptions.Top;
        _skillText.enableWordWrapping = true;
        _skillText.overflowMode = TextOverflowModes.Ellipsis;

        status.anchorMin = Vector2.zero;
        status.anchorMax = new Vector2(1f, 0.4f);
        status.pivot = new Vector2(0.5f, 1f);
        status.offsetMin = new Vector2(16f, 8f);
        status.offsetMax = new Vector2(-16f, -4f);
        _statusText.verticalAlignment = VerticalAlignmentOptions.Top;
        _statusText.enableWordWrapping = true;
        _statusText.overflowMode = TextOverflowModes.Ellipsis;
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
        _weaponBySkill.Clear();
        var buckets = new List<SkillId>[WeaponOrder.Length];
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new List<SkillId>();
        }

        var extras = new List<SkillId>();
        foreach (SkillId skillId in Enum.GetValues(typeof(SkillId)))
        {
            if (skillId == SkillId.None) continue;

            WeaponKind kind = ResolveWeaponKindFallback(skillId);
            _weaponBySkill[skillId] = kind;

            int weaponIndex = Array.IndexOf(WeaponOrder, kind);
            if (weaponIndex >= 0)
            {
                buckets[weaponIndex].Add(skillId);
            }
            else
            {
                extras.Add(skillId);
            }
        }

        var ordered = new List<SkillId>(32);
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i].Sort(CompareSkillOrder);
            ordered.AddRange(buckets[i]);
        }

        extras.Sort(CompareSkillOrder);
        ordered.AddRange(extras);

        _skillIds = ordered.ToArray();
        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _skillIds.Length - 1));
    }

    private static int CompareSkillOrder(SkillId left, SkillId right)
    {
        return string.CompareOrdinal(left.ToString(), right.ToString());
    }

    private void Step(int delta)
    {
        if (_skillIds.Length == 0)
        {
            RebuildSkillList();
            if (_skillIds.Length == 0)
            {
                RefreshLabels("再生できる SkillId がありません。");
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
                RefreshLabels("再生できる SkillId がありません。");
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
                _skillText.text = "再生できる SkillId がありません。";
            }
            else
            {
                SkillId id = _skillIds[_index];
                SkillBase skill = CombatSkillFactory.Create(id);
                string skillName = skill != null ? skill.Name : id.ToString();
                string weaponName = WeaponDisplayName(ResolveWeaponKind(id));
                string source = "Procedural";
                if (_catalog != null &&
                    _catalog.TryGetEntry(id, out SkillVfxCatalog.Entry entry) &&
                    entry.Prefab != null &&
                    !entry.Prefab.name.StartsWith("Placeholder"))
                {
                    source = entry.Prefab.name;
                }

                _skillText.text =
                    $"武器: {weaponName}\nスキル: {skillName}  [{_index + 1}/{_skillIds.Length}]\nVFX: {source}";
            }
        }

        if (_statusText != null)
        {
            _statusText.text = status + "\n←/→ or A/D: 選択  Space: 再生  C: 消去  1:×0.25  2:×1";
        }
    }

    private WeaponKind ResolveWeaponKind(SkillId skillId)
    {
        if (_weaponBySkill.TryGetValue(skillId, out WeaponKind kind))
        {
            return kind;
        }

        return ResolveWeaponKindFallback(skillId);
    }

    private static WeaponKind ResolveWeaponKindFallback(SkillId skillId)
    {
        string name = skillId.ToString();
        if (name.StartsWith("Sword_", StringComparison.Ordinal)) return WeaponKind.Sword;
        if (name.StartsWith("Shield_", StringComparison.Ordinal)) return WeaponKind.Shield;
        if (name.StartsWith("Wand_", StringComparison.Ordinal)) return WeaponKind.Wand;
        if (name.StartsWith("Grimoire_", StringComparison.Ordinal) ||
            name.StartsWith("StatDebuff_", StringComparison.Ordinal))
        {
            return WeaponKind.Grimoire;
        }

        if (name.StartsWith("Bible_", StringComparison.Ordinal)) return WeaponKind.Bible;
        if (name.StartsWith("Rosary_", StringComparison.Ordinal)) return WeaponKind.Rosary;
        return WeaponKind.Unarmed;
    }

    private static string WeaponDisplayName(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => "剣",
            WeaponKind.Shield => "盾",
            WeaponKind.Wand => "杖",
            WeaponKind.Grimoire => "魔導書",
            WeaponKind.Bible => "聖書",
            WeaponKind.Rosary => "ロザリオ",
            WeaponKind.Unarmed => "素手",
            _ => kind.ToString(),
        };
    }
}
