using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatAudioSettingsView : MonoBehaviour
{
    private CombatAudioSettings _settings;
    private RectTransform _panel;
    private TMP_FontAsset _font;
    private readonly List<SkillDefinition> _definitions = new();

    public static void Open(Transform parent)
    {
        if (parent.GetComponentInChildren<CombatAudioSettingsView>() != null) return;
        var host = new GameObject("AudioSettings", typeof(RectTransform));
        host.transform.SetParent(parent, false);
        host.AddComponent<CombatAudioSettingsView>().Build();
    }

    private void Build()
    {
        _settings = CombatAudioPlayer.Instance.Settings;
        if (_settings == null) { Destroy(gameObject); return; }
        _font = _settings.Font;
        _panel = (RectTransform)transform;
        _panel.anchorMin = Vector2.zero;
        _panel.anchorMax = Vector2.one;
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        gameObject.AddComponent<Image>().color = new Color(0.055f, 0.07f, 0.10f, 0.99f);
        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.spacing = 8;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        var header = Row(transform, 42);
        Label(header, "戦闘の音設定", 26);
        Button(header, "試聴停止", () => CombatAudioPlayer.Instance.StopPreview(), 110);
        Button(header, "閉じる", () => Destroy(gameObject), 80);
        Label(Row(transform, 28), "音素材を各欄へドラッグ＆ドロップ ・ ▶ 試聴 ・ × 削除 ・ 設定は自動保存", 17);
        Slot(Row(transform, 42), SkillId.None, "戦闘中BGM（ループ）");
        var volume = Row(transform, 28);
        Volume(volume, "BGM音量", true);
        Volume(volume, "SE音量", false);
        var catalog = CombatSkillCatalog.CreateDefaultRuntimeCatalog();
        _definitions.AddRange(catalog.Definitions);
        var table = Scroll(transform, true);
        var grid = table.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.spacing = new Vector2(8, 5);
        Canvas.ForceUpdateCanvases();
        grid.cellSize = new Vector2(Mathf.Max(250, (_panel.rect.width - 64) / 3), 43);
        foreach (var definition in _definitions)
            Slot(Row(table, 43), definition.SkillId, definition.DisplayName);
        Destroy(catalog);
        Label(Row(transform, 26), "音素材一覧（ドラッグして配置 / クリックで試聴）", 18);
        var library = Scroll(transform, false);
        var libraryLayout = library.gameObject.AddComponent<VerticalLayoutGroup>();
        libraryLayout.childControlHeight = libraryLayout.childControlWidth = true;
        libraryLayout.childForceExpandHeight = false;
        var clips = new List<AudioClip>(_settings.Library);
#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && !clips.Contains(clip)) clips.Add(clip);
        }
#endif
        clips.Sort((a, b) => string.Compare(a != null ? a.name : "", b != null ? b.name : "", System.StringComparison.Ordinal));
        if (clips.Count == 0) Label(Row(library, 30), "音素材がない場合は Assets に wav / ogg / mp3 を追加して開き直す", 17);
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            var button = Button(Row(library, 30), clip.name, () => CombatAudioPlayer.Instance.Preview(clip, false));
            button.gameObject.AddComponent<CombatAudioDrag>().Clip = clip;
        }
    }

    private void Slot(Transform row, SkillId skill, string title)
    {
        if (skill != SkillId.None) CreateSkillIcon(row, skill);
        Label(row, title, 16, 120);
        AudioClip Current() => skill == SkillId.None ? _settings.BattleBgm : _settings.GetClip(skill);
        var target = Button(row, Current() != null ? Current().name : "ここにドロップ", () => { });
        var drag = target.gameObject.AddComponent<CombatAudioDrag>();
        drag.Assign = clip =>
        {
            _settings.SetClip(skill, clip);
            target.GetComponentInChildren<TMP_Text>().text = clip != null ? clip.name : "ここにドロップ";
        };
        Button(row, "▶", () => CombatAudioPlayer.Instance.Preview(Current(), skill == SkillId.None), 30);
        Button(row, "×", () => { drag.Assign(null); CombatAudioPlayer.Instance.StopPreview(); }, 30);
    }

    private static void CreateSkillIcon(Transform row, SkillId skill)
    {
        var container = new GameObject("SkillIcon", typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(row, false);
        container.GetComponent<LayoutElement>().preferredWidth = 36;
        var imageObject = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(container.transform, false);
        var rect = (RectTransform)imageObject.transform;
        rect.sizeDelta = new Vector2(32, 32);
        var icon = imageObject.GetComponent<RawImage>();
        icon.raycastTarget = false;
        icon.color = Color.Lerp(SkillVfxArt.ColorFor(skill), Color.white, .35f);
        var atlas = SkillVfxAtlas.Shared;
        if (atlas != null && atlas.Material != null)
        {
            icon.texture = atlas.Material.mainTexture;
            icon.uvRect = atlas.Regions[(int)SkillVfxArt.IconFor(skill)];
        }
    }

    private void Volume(Transform row, string title, bool bgm)
    {
        Label(row, title, 16, 95);
        var sliderObject = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(row, false);
        sliderObject.GetComponent<Image>().color = new Color(0.18f, 0.23f, 0.30f);
        sliderObject.GetComponent<LayoutElement>().flexibleWidth = 1;
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliderObject.transform, false);
        var handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(14, 24);
        var slider = sliderObject.GetComponent<Slider>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.value = bgm ? _settings.BgmVolume : _settings.SeVolume;
        slider.onValueChanged.AddListener(value =>
        {
            if (bgm) _settings.BgmVolume = value; else _settings.SeVolume = value;
            _settings.Save();
        });
    }

    private RectTransform Scroll(Transform parent, bool expand)
    {
        var obj = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.15f);
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = expand ? 130 : 100;
        element.preferredHeight = expand ? 500 : 120;
        element.flexibleHeight = expand ? 1 : 0;
        var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        content.transform.SetParent(obj.transform, false);
        var rect = (RectTransform)content.transform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = Vector2.zero;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = obj.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)obj.transform;
        scroll.content = rect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        return rect;
    }

    private Transform Row(Transform parent, float height)
    {
        var obj = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        var layout = obj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        obj.GetComponent<LayoutElement>().preferredHeight = height;
        return obj.transform;
    }

    private TMP_Text Label(Transform parent, string text, float size, float width = 0)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = _font;
        if (_font != null) label.fontSharedMaterial = _font.material;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        var element = obj.GetComponent<LayoutElement>();
        if (width > 0) element.preferredWidth = width; else element.flexibleWidth = 1;
        return label;
    }

    private Button Button(Transform parent, string text, UnityEngine.Events.UnityAction action, float width = 0)
    {
        var obj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.20f, 0.26f, 0.35f);
        var element = obj.GetComponent<LayoutElement>();
        if (width > 0) element.preferredWidth = width; else element.flexibleWidth = 1;
        var label = Label(obj.transform, text, 16);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5, 0);
        rect.offsetMax = new Vector2(-5, 0);
        var button = obj.GetComponent<Button>();
        button.onClick.AddListener(action);
        return button;
    }

    private void OnDestroy()
    {
        if (CombatAudioPlayer.Instance != null) CombatAudioPlayer.Instance.StopPreview();
        foreach (var definition in _definitions) if (definition != null) Destroy(definition);
    }
}
