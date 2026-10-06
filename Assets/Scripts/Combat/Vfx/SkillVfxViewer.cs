using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// EffectTest 専用。全 SkillId のVFXを手動・自動再生する。
/// </summary>
[ExecuteAlways]
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
    [SerializeField] private Transform _caster;
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _point;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _skillText;
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _autoButton;
    [SerializeField] private Button _clearButton;
    [SerializeField] private Button _slowButton;
    [SerializeField] private Button _normalButton;

    private SkillId[] _skillIds = Array.Empty<SkillId>();
    private readonly Dictionary<SkillId, WeaponKind> _weaponBySkill = new();
    private readonly List<UnityEngine.UI.Outline> _skillButtonOutlines = new();
    private ScrollRect _skillScroll;
    private int _index;
    private bool _autoPlaying;

    private void OnEnable()
    {
        ApplyUiLayout();
        BindButtons(Application.isPlaying);
        RebuildSkillList();
        BuildSkillButtons();
        BindSkillButtons(Application.isPlaying);
        RefreshLabels("Ready");
    }

    private void OnDisable()
    {
        BindButtons(false);
        BindSkillButtons(false);
        StopAutoPlay();
        if (Application.isPlaying) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        AdvanceAutoPlay();
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
            StopAutoPlay();
            PlayCurrent();
        }
        else if (keyboard.pKey.wasPressedThisFrame)
        {
            ToggleAutoPlay();
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
        Bind(_autoButton, ToggleAutoPlay, bind);
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

        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.right;
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 120f);
        panel.sizeDelta = new Vector2(-48f, 150f);

        skill.anchorMin = new Vector2(0f, 0.4f);
        skill.anchorMax = Vector2.one;
        skill.pivot = new Vector2(0.5f, 1f);
        skill.offsetMin = new Vector2(16f, 4f);
        skill.offsetMax = new Vector2(-16f, -12f);
        _skillText.verticalAlignment = VerticalAlignmentOptions.Top;
        _skillText.fontSize = 24f;
        _skillText.textWrappingMode = TextWrappingModes.Normal;
        _skillText.overflowMode = TextOverflowModes.Ellipsis;

        status.anchorMin = Vector2.zero;
        status.anchorMax = new Vector2(1f, 0.4f);
        status.pivot = new Vector2(0.5f, 1f);
        status.offsetMin = new Vector2(16f, 8f);
        status.offsetMax = new Vector2(-16f, -4f);
        _statusText.verticalAlignment = VerticalAlignmentOptions.Top;
        _statusText.fontSize = 20f;
        _statusText.textWrappingMode = TextWrappingModes.Normal;
        _statusText.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void BuildSkillButtons()
    {
        if (_skillScroll != null || _skillText == null) return;
        Canvas canvas = _skillText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform existing = canvas.transform.Find("SkillPicker");
        if (existing != null)
        {
            _skillScroll = existing.GetComponent<ScrollRect>();
            _skillButtonOutlines.Clear();
            foreach (Transform tile in _skillScroll.content)
                _skillButtonOutlines.Add(tile.GetComponent<UnityEngine.UI.Outline>());
            return;
        }

        RectTransform panel = UiRect("SkillPicker", canvas.transform);
        panel.anchorMin = Vector2.up;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(.5f, 1);
        panel.offsetMin = new Vector2(0, -240);
        panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = new Color(.12f, .12f, .12f, .96f);
        _skillScroll = panel.gameObject.AddComponent<ScrollRect>();
        _skillScroll.horizontal = true;
        _skillScroll.vertical = false;
        _skillScroll.movementType = ScrollRect.MovementType.Clamped;
        _skillScroll.scrollSensitivity = 32;

        RectTransform viewport = UiRect("Viewport", panel);
        Stretch(viewport, new Vector2(16, 24), new Vector2(-16, -12));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = UiRect("Skills", viewport);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.up;
        content.pivot = new Vector2(0, 1);
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(92, 92);
        grid.spacing = new Vector2(8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 2;
        grid.padding = new RectOffset(4, 4, 4, 4);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        _skillScroll.viewport = viewport;
        _skillScroll.content = content;

        RectTransform track = UiRect("Scrollbar", panel);
        track.anchorMin = Vector2.zero;
        track.anchorMax = Vector2.right;
        track.offsetMin = new Vector2(16, 8);
        track.offsetMax = new Vector2(-16, 16);
        track.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .08f);
        RectTransform handle = UiRect("Handle", track);
        Stretch(handle, Vector2.zero, Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(.6f, .7f, .9f, .7f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        _skillScroll.horizontalScrollbar = scrollbar;

        SkillVfxAtlas atlas = SkillVfxAtlas.Shared;
        for (int i = 0; i < _skillIds.Length; i++)
        {
            SkillId id = _skillIds[i];
            Color color = SkillVfxArt.ColorFor(id);
            RectTransform tile = UiRect(id.ToString(), content);
            Image background = tile.gameObject.AddComponent<Image>();
            background.color = new Color(.25f, .25f, .25f);
            var selection = tile.gameObject.AddComponent<UnityEngine.UI.Outline>();
            selection.effectColor = Color.white;
            selection.effectDistance = new Vector2(2, -2);
            _skillButtonOutlines.Add(selection);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            RectTransform art = UiRect("Effect", tile);
            Stretch(art, new Vector2(12, 30), new Vector2(-12, -8));
            var icon = art.gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            icon.color = Color.Lerp(color, Color.white, .35f);
            if (atlas != null && atlas.Material != null)
            {
                icon.texture = atlas.Material.mainTexture;
                icon.uvRect = atlas.Regions[(int)SkillVfxArt.IconFor(id)];
            }

            RectTransform labelRect = UiRect("Name", tile);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.right;
            labelRect.offsetMin = new Vector2(3, 3);
            labelRect.offsetMax = new Vector2(-3, 29);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _skillText.font;
            label.fontSharedMaterial = _skillText.fontSharedMaterial;
            label.text = CombatSkillFactory.Create(id)?.Name ?? id.ToString();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10;
            label.fontSizeMax = 16;
            label.raycastTarget = false;
        }
    }

    private void BindSkillButtons(bool bind)
    {
        for (int i = 0; i < _skillButtonOutlines.Count; i++)
        {
            int index = i;
            Button button = _skillButtonOutlines[i].GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            if (bind) button.onClick.AddListener(() => SelectSkill(index));
        }
    }

    private void SelectSkill(int index)
    {
        StopAutoPlay();
        _index = index;
        _player?.ClearAll();
        PlayCurrent();
    }

    private static RectTransform UiRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }



    private static void Bind(Button button, UnityEngine.Events.UnityAction action, bool bind)
    {
        if (button == null) return;
        if (bind) button.onClick.AddListener(action);
        else button.onClick.RemoveListener(action);
    }

    private void OnPrev() => Step(-1);
    private void OnNext() => Step(1);
    private void OnPlay() { StopAutoPlay(); PlayCurrent(); }
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
        StopAutoPlay();
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

    private bool PlayCurrent()
    {
        if (_player == null)
        {
            RefreshLabels("SkillVfxPlayer が未設定です。");
            return false;
        }

        if (_skillIds.Length == 0)
        {
            RebuildSkillList();
            if (_skillIds.Length == 0)
            {
                RefreshLabels("再生できる SkillId がありません。");
                return false;
            }
        }

        SkillId skillId = _skillIds[_index];
        Vector3 selfPos = Feet(_caster);
        Vector3? targetPos = _target != null ? Feet(_target) : null;
        Vector3? pointPos = _point != null ? _point.position : null;
        bool ok = _player.TryPlay(skillId, selfPos, targetPos, pointPos, out string message);
        RefreshLabels(ok ? message : "失敗: " + message);
        return ok;
    }

    public void ToggleAutoPlay()
    {
        if (_autoPlaying)
        {
            StopAutoPlay();
            RefreshLabels("自動再生停止");
            return;
        }
        _player?.ClearAll();
        _index = 0;
        _autoPlaying = true;
        if (!PlayCurrent()) StopAutoPlay();
    }

    private void AdvanceAutoPlay()
    {
        if (!_autoPlaying) return;
        if (_player == null) { StopAutoPlay(); return; }
        if (_player.ActiveCount > 0) return;
        _index = (_index + 1) % _skillIds.Length;
        if (!PlayCurrent()) StopAutoPlay();
    }

    private void StopAutoPlay()
    {
        _autoPlaying = false;
        UpdateAutoButton();
    }

    private void UpdateAutoButton()
    {
        if (_autoButton == null) return;
        var label = _autoButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = _autoPlaying ? "自動停止" : "全自動";
    }

    private static Vector3 Feet(Transform anchor)
    {
        if (anchor == null) return Vector3.zero;
        Vector3 p = anchor.position;
        Renderer renderer = anchor.GetComponent<Renderer>();
        if (renderer != null) p.y = renderer.bounds.min.y;
        return p;
    }

    private void ClearEffects()
    {
        StopAutoPlay();
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
        UpdateAutoButton();
        for (int i = 0; i < _skillButtonOutlines.Count; i++)
        {
            _skillButtonOutlines[i].enabled = i == _index;
        }
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
                _skillText.text =
                    $"武器: {weaponName}\nスキル: {skillName}  [{_index + 1}/{_skillIds.Length}]";
            }
        }

        if (_statusText != null)
        {
            _statusText.text = (_autoPlaying ? "全スキル自動再生中（繰り返し） / " : "") + status;
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
