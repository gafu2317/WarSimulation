using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatAudioSettingsView : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private CombatAudioPlayer _player;
    private Button _previewButton;
    private GameObject _sePage;
    private GameObject _bgmPage;
    private Transform _bgmRows;
    private Button _seTab;
    private Button _bgmTab;
    private bool _showBgm;
    private GridLayoutGroup _skillGrid;
    private GridLayoutGroup _libraryGrid;
    private CombatAudioSettings _settings;
    private RectTransform _panel;
    private TMP_FontAsset _font;
    private Transform _library;
    private GameObject _emptyLibraryHint;
    private CombatBrowserAudioImport _browserImport;
    private readonly List<SkillDefinition> _definitions = new();

    public static void Open(Transform parent)
    {
        if (parent.GetComponentInChildren<CombatAudioSettingsView>() != null) return;
        var host = new GameObject("AudioSettings", typeof(RectTransform));
        host.transform.SetParent(parent, false);
        host.AddComponent<CombatAudioSettingsView>().Build();
    }

    private void OnEnable() => IsOpen = true;
    private void OnDisable() => IsOpen = false;

    private void Build()
    {
        _player = CombatAudioPlayer.Instance;
        _settings = _player.Settings;
        if (_settings == null) { Destroy(gameObject); return; }
        _font = _settings.Font;
        _panel = (RectTransform)transform;
        _panel.anchorMin = Vector2.zero;
        _panel.anchorMax = Vector2.one;
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        gameObject.AddComponent<Image>().color = new Color(0.055f, 0.07f, 0.10f, 1f);
        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.spacing = 8;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        var header = Row(transform, 42);
        Label(header, "音設定", 26);
        Button(header, "閉じる", () => Destroy(gameObject), 80);
        var importRow = Row(transform, 36);
        _browserImport = gameObject.AddComponent<CombatBrowserAudioImport>();
        var status = Label(importRow, "PCの音声を各欄へドロップ / 一覧からドラッグ ・ ▶ 試聴 / ■ 停止 ・ × 削除", 17);
        _browserImport.StatusChanged = message => status.text = message;
        _browserImport.Imported = clip =>
        {
            _settings.Library.Add(clip);
            AddLibraryClip(clip);
        };
        Button(importRow, "音声ファイル追加", _browserImport.SelectFiles, 150);
        var tabs = Row(transform, 38);
        _seTab = Button(tabs, "SE", () => SelectTab(false), 140);
        _bgmTab = Button(tabs, "BGM", () => SelectTab(true), 140);
        _seTab.gameObject.name = "SETab";
        _bgmTab.gameObject.name = "BGMTab";
        _sePage = Page("SE");
        Volume(Row(_sePage.transform, 28), "SE音量", false);
        _bgmPage = Page("BGM");
        Volume(Row(_bgmPage.transform, 28), "BGM音量", true);
        var bgmHeader = Row(_bgmPage.transform, 36);
        Label(bgmHeader, "場面ごとのBGM（ループ試聴）", 18);
        _bgmRows = Scroll(_bgmPage.transform, false);
        _bgmRows.parent.GetComponent<LayoutElement>().preferredHeight = 260;
        var bgmLayout = _bgmRows.gameObject.AddComponent<VerticalLayoutGroup>();
        bgmLayout.spacing = 6;
        bgmLayout.childControlWidth = bgmLayout.childControlHeight = true;
        bgmLayout.childForceExpandHeight = false;
        BgmSlot(CombatAudioSettings.BgmScene.Formation, "編成");
        BgmSlot(CombatAudioSettings.BgmScene.Battle, "戦闘中");
        BgmSlot(CombatAudioSettings.BgmScene.Victory, "勝利");
        BgmSlot(CombatAudioSettings.BgmScene.Defeat, "敗北");
        Label(Row(_bgmPage.transform, 28), "戦闘中以外は設定・試聴用。場面での自動再生は今後追加", 16);
        _settings.InitializePlayerMagicSounds();
        var catalog = CombatSkillCatalog.CreateDefaultRuntimeCatalog();
        _definitions.AddRange(catalog.Definitions);
        var table = Scroll(_sePage.transform, false);
        var grid = table.gameObject.AddComponent<GridLayoutGroup>();
        _skillGrid = grid;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.spacing = new Vector2(8, 5);
        Canvas.ForceUpdateCanvases();
        grid.cellSize = new Vector2(Mathf.Max(250, (_panel.rect.width - 64) / 3), 43);
        foreach (var definition in _definitions)
            Slot(Row(table, 43), definition.SkillId, definition.DisplayName);
        Slot(Row(table, 43), SkillId.Player_AreaMagic, "広範囲魔法");
        Slot(Row(table, 43), SkillId.Player_AllInvulnerable, "全員無敵");
        Destroy(catalog);
        Label(Row(transform, 26), "音素材一覧（ドラッグして配置 / クリックで試聴）", 18);
        table.parent.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil((_definitions.Count + 2) / 3f) * 48 - 5;
        var library = Scroll(transform, true);
        _library = library;
        _libraryGrid = library.gameObject.AddComponent<GridLayoutGroup>();
        _libraryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _libraryGrid.constraintCount = 4;
        _libraryGrid.spacing = new Vector2(8, 6);
        var clips = new List<AudioClip>(_settings.Library);
#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && !clips.Contains(clip)) clips.Add(clip);
        }
#endif
        clips.Sort((a, b) => string.Compare(a != null ? a.name : "", b != null ? b.name : "", System.StringComparison.Ordinal));
        if (clips.Count == 0)
        {
            var hint = Row(library, 36);
            _emptyLibraryHint = hint.gameObject;
            Label(hint, "音声ファイル追加、またはPCからドロップ（EditorではAssetsに追加）", 17);
        }
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            AddLibraryClip(clip);
        }
        Canvas.ForceUpdateCanvases();
        SelectTab(false);
    }

    private GameObject Page(string title)
    {
        var page = new GameObject(title + "Page", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        page.transform.SetParent(transform, false);
        var layout = page.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        return page;
    }

    private void SelectTab(bool bgm)
    {
        _player.StopPreview();
        ClearPreviewButton();
        _showBgm = bgm;
        _sePage.SetActive(!bgm);
        _bgmPage.SetActive(bgm);
        _seTab.interactable = bgm;
        _bgmTab.interactable = !bgm;
        Canvas.ForceUpdateCanvases();
        RefreshGridWidths();
    }

    private void BgmSlot(CombatAudioSettings.BgmScene scene, string title)
    {
        var row = Row(_bgmRows, 43);
        Label(row, title, 16, 120);
        AudioClip Current() => _settings.GetBgm(scene);
        var target = Button(row, Current() != null ? Current().name : "ここにドロップ", () => { });
        var drag = target.gameObject.AddComponent<CombatAudioDrag>();
        drag.Assign = clip =>
        {
            _settings.SetBgm(scene, clip);
            target.GetComponentInChildren<TMP_Text>().text = clip != null ? clip.name : "ここにドロップ";
        };
        VolumeSlider(row, "個別音量", _settings.GetBgmVolume(scene), value => _settings.SetBgmVolume(scene, value), 100);
        Button preview = null;
        preview = Button(row, "▶", () => TogglePreview(preview, Current(), true, scene: scene), 32);
        Button(row, "×", () => { drag.Assign(null); _player.StopPreview(); ClearPreviewButton(); }, 32);
    }

    private void LateUpdate()
    {
        if (_previewButton != null && (_player == null || !_player.IsPreviewPlaying)) ClearPreviewButton();
    }

    private void OnRectTransformDimensionsChange() => RefreshGridWidths();

    private void RefreshGridWidths()
    {
        if (_skillGrid != null)
            _skillGrid.cellSize = new Vector2((((RectTransform)_skillGrid.transform.parent).rect.width - 16) / 3, 43);
        if (_libraryGrid != null)
            _libraryGrid.cellSize = new Vector2((((RectTransform)_libraryGrid.transform.parent).rect.width - 24) / 4, 36);
    }

    private void ClearPreviewButton()
    {
        if (_previewButton != null) _previewButton.GetComponentInChildren<TMP_Text>().text = "▶";
        _previewButton = null;
    }

    private void TogglePreview(Button button, AudioClip clip, bool bgm, SkillId? skill = null, CombatAudioSettings.BgmScene? scene = null)
    {
        bool stop = _previewButton == button && _player.IsPreviewPlaying;
        _player.StopPreview();
        ClearPreviewButton();
        if (stop || clip == null) return;
        _player.Preview(clip, bgm, skill, scene);
        _previewButton = button;
        button.GetComponentInChildren<TMP_Text>().text = "■";
    }

    private void AddLibraryClip(AudioClip clip)
    {
        if (_emptyLibraryHint != null)
        {
            _emptyLibraryHint.SetActive(false);
            Destroy(_emptyLibraryHint);
            _emptyLibraryHint = null;
        }
        var row = Row(_library, 36);
        Button preview = null;
        var button = Button(row, clip.name, () => TogglePreview(preview, clip, _showBgm));
        button.gameObject.AddComponent<CombatAudioDrag>().Clip = clip;
        VolumeSlider(row, "素材音量", _settings.GetClipVolume(clip), value => _settings.SetClipVolume(clip, value), 65);
        preview = Button(row, "▶", () => TogglePreview(preview, clip, _showBgm), 32);
    }

    private void Slot(Transform row, SkillId skill, string title)
    {
        if (skill != SkillId.None) CreateSkillIcon(row, skill);
        Label(row, title, 16, skill == SkillId.None ? 190 : 120);
        AudioClip Current() => skill == SkillId.None ? _settings.BattleBgm : _settings.GetClip(skill);
        var target = Button(row, Current() != null ? Current().name : "ここにドロップ", () => { });
        var drag = target.gameObject.AddComponent<CombatAudioDrag>();
        drag.Assign = clip =>
        {
            _settings.SetClip(skill, clip);
            target.GetComponentInChildren<TMP_Text>().text = clip != null ? clip.name : "ここにドロップ";
        };
        VolumeSlider(row, "個別音量", _settings.GetVolume(skill), value => _settings.SetVolume(skill, value), 65);
        Button preview = null;
        preview = Button(row, "▶", () => TogglePreview(preview, Current(), skill == SkillId.None, skill), 30);
        Button(row, "×", () => { drag.Assign(null); _player.StopPreview(); ClearPreviewButton(); }, 30);
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
        VolumeSlider(row, title, bgm ? _settings.BgmVolume : _settings.SeVolume, value =>
        {
            if (bgm) _settings.BgmVolume = value; else _settings.SeVolume = value;
            _settings.Save();
        });
    }

    private void VolumeSlider(Transform row, string title, float value, System.Action<float> changed, float width = 0)
    {
        var sliderObject = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(row, false);
        sliderObject.GetComponent<Image>().color = new Color(0.18f, 0.23f, 0.30f);
        var element = sliderObject.GetComponent<LayoutElement>();
        if (width > 0) element.minWidth = element.preferredWidth = width; else element.flexibleWidth = 1;
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliderObject.transform, false);
        var handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(10, 22);
        var slider = sliderObject.GetComponent<Slider>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.value = value;
        var percentage = Label(row, $"{Mathf.RoundToInt(value * 100)}%", 13, 37);
        slider.onValueChanged.AddListener(volume =>
        {
            changed(volume);
            percentage.text = $"{Mathf.RoundToInt(volume * 100)}%";
            _player.RefreshVolumes();
        });
    }

    private RectTransform Scroll(Transform parent, bool expand)
    {
        var obj = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.15f);
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = expand ? 100 : 130;
        element.preferredHeight = expand ? 120 : 500;
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
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        element.flexibleHeight = 0;
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
        label.textWrappingMode = TextWrappingModes.NoWrap;
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
        if (_player != null) _player.StopPreview();
        foreach (var definition in _definitions) if (definition != null) Destroy(definition);
    }
}
