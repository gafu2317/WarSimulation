using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

public sealed class CombatMapSelectionView : MonoBehaviour
{
    [SerializeField] private List<AuthoredMapDefinition> _mapOptions = new();
    [SerializeField] private Button _openSelectionButton;
    [SerializeField] private TMP_Text _summaryMapNameText;
    [SerializeField] private TMP_Text _summaryAvailabilityText;
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _closeFooterButton;
    [SerializeField] private TMP_Text _overlayMapNameText;
    [SerializeField] private RawImage _previewImage;
    [SerializeField] private RectTransform _magicStoneMarkerLayer;
    [SerializeField] private Image _magicStoneMarkerTemplate;
    [SerializeField] private GameObject _previewUnavailableOverlay;
    [SerializeField] private TMP_Text _previewUnavailableReasonText;
    [SerializeField] private TMP_Text _overlayAvailabilityText;
    [SerializeField] private Color _ownStoneColor = new(0.27f, 0.55f, 1f, 1f);
    [SerializeField] private Color _enemyStoneColor = new(1f, 0.31f, 0.31f, 1f);

    private readonly List<AuthoredMapDefinition> _availableOptions = new();
    private readonly List<Image> _markers = new();
    private int _selectedIndex = -1;
    private bool _stonePositionsReversed;
    private bool _interactionEnabled = true;
    private string _failureMessage;
    private string _preparationMessage;

    public AuthoredMapDefinition SelectedMap =>
        _selectedIndex >= 0 && _selectedIndex < _availableOptions.Count
            ? _availableOptions[_selectedIndex]
            : null;

    public CombatMapAvailability Availability { get; private set; }
    public bool CanStartBattle => _interactionEnabled && Availability.CanStartBattle;
    public bool IsOverlayOpen => _overlayRoot != null && _overlayRoot.activeSelf;

    public event Action SelectionChanged;

    private void Awake()
    {
        _openSelectionButton?.onClick.AddListener(OpenOverlay);
        _previousButton?.onClick.AddListener(SelectPrevious);
        _nextButton?.onClick.AddListener(SelectNext);
        _closeButton?.onClick.AddListener(CloseOverlay);
        _closeFooterButton?.onClick.AddListener(CloseOverlay);
        _magicStoneMarkerTemplate?.gameObject.SetActive(false);
        _overlayRoot?.SetActive(false);
    }

    private void OnDestroy()
    {
        _openSelectionButton?.onClick.RemoveListener(OpenOverlay);
        _previousButton?.onClick.RemoveListener(SelectPrevious);
        _nextButton?.onClick.RemoveListener(SelectNext);
        _closeButton?.onClick.RemoveListener(CloseOverlay);
        _closeFooterButton?.onClick.RemoveListener(CloseOverlay);
    }

    public void Initialize(AuthoredMapDefinition preferredMap, bool stonePositionsReversed)
    {
        _availableOptions.Clear();
        for (int i = 0; i < _mapOptions.Count; i++)
        {
            AuthoredMapDefinition option = _mapOptions[i];
            if (option != null && !_availableOptions.Contains(option))
                _availableOptions.Add(option);
        }

        _selectedIndex = _availableOptions.Count > 0 ? 0 : -1;
        int preferredIndex = _availableOptions.IndexOf(preferredMap);
        if (preferredIndex >= 0) _selectedIndex = preferredIndex;
        _stonePositionsReversed = stonePositionsReversed;
        _interactionEnabled = true;
        _failureMessage = null;
        _preparationMessage = null;
        Refresh();
    }

    public void SetStonePositionsReversed(bool reversed)
    {
        if (_stonePositionsReversed == reversed) return;
        _stonePositionsReversed = reversed;
        _failureMessage = null;
        Refresh();
        SelectionChanged?.Invoke();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (_interactionEnabled == enabled) return;
        _interactionEnabled = enabled;
        RefreshInteraction();
        SelectionChanged?.Invoke();
    }

    public void ShowFailure(string message)
    {
        _failureMessage = message;
        RefreshStatus();
    }

    public void ClearFailure()
    {
        if (string.IsNullOrEmpty(_failureMessage)) return;
        _failureMessage = null;
        RefreshStatus();
    }

    public void ShowPreparation(string message)
    {
        _preparationMessage = message;
        RefreshStatus();
    }

    public void ClearPreparation()
    {
        if (string.IsNullOrEmpty(_preparationMessage)) return;
        _preparationMessage = null;
        RefreshStatus();
    }

    private void OpenOverlay()
    {
        if (!_interactionEnabled || _overlayRoot == null) return;
        _overlayRoot.SetActive(true);
    }

    private void CloseOverlay()
    {
        _overlayRoot?.SetActive(false);
    }

    private void SelectPrevious()
    {
        SelectRelative(-1);
    }

    private void SelectNext()
    {
        SelectRelative(1);
    }

    private void SelectRelative(int offset)
    {
        if (!_interactionEnabled || _availableOptions.Count == 0) return;
        _selectedIndex = (_selectedIndex + offset + _availableOptions.Count) % _availableOptions.Count;
        _failureMessage = null;
        _preparationMessage = null;
        Refresh();
        SelectionChanged?.Invoke();
    }

    private void Refresh()
    {
        Availability = CombatMapAvailability.Evaluate(SelectedMap, _stonePositionsReversed);
        string mapName = SelectedMap != null ? SelectedMap.name : "マップ未登録";
        SetText(_summaryMapNameText, $"マップ選択：{mapName}");
        SetText(_overlayMapNameText, mapName);
        RefreshPreview();
        RefreshStatus();
        RefreshInteraction();
    }

    private void RefreshPreview()
    {
        bool hasPreview = SelectedMap != null && SelectedMap.HasValidBakedPreview;
        if (_previewImage != null)
        {
            _previewImage.texture = hasPreview ? SelectedMap.BakedPreview : null;
            _previewImage.enabled = hasPreview;
        }

        if (_previewUnavailableOverlay != null)
            _previewUnavailableOverlay.SetActive(!hasPreview);
        if (_previewUnavailableReasonText != null)
        {
            _previewUnavailableReasonText.text = SelectedMap == null
                ? "マップが登録されていません"
                : SelectedMap.BakedPreview == null
                    ? "プレビューが未生成です"
                    : "マップ変更後にプレビューが再生成されていません";
        }

        RefreshMagicStoneMarkers(hasPreview);
    }

    private void RefreshMagicStoneMarkers(bool visible)
    {
        for (int i = 0; i < _markers.Count; i++)
            _markers[i].gameObject.SetActive(false);
        if (!visible || SelectedMap == null || SelectedMap.SharedConfig == null ||
            _magicStoneMarkerLayer == null || _magicStoneMarkerTemplate == null)
            return;

        var ownCenters = new List<Vector2>();
        var enemyCenters = new List<Vector2>();
        List<AuthoredMagicStonePlacement> stones = SelectedMap.MagicStones;
        for (int i = 0; i < stones.Count; i++)
        {
            AuthoredMagicStonePlacement stone = stones[i];
            if (stone == null) continue;
            if (stone.Type == FeatureType.OwnMainStone) ownCenters.Add(stone.Center);
            if (stone.Type == FeatureType.EnemyMainStone) enemyCenters.Add(stone.Center);
        }

        bool swap = _stonePositionsReversed && ownCenters.Count == enemyCenters.Count;
        int markerIndex = 0;
        for (int i = 0; i < ownCenters.Count; i++)
            ShowMarker(markerIndex++, swap ? enemyCenters[i] : ownCenters[i], _ownStoneColor);
        for (int i = 0; i < enemyCenters.Count; i++)
            ShowMarker(markerIndex++, swap ? ownCenters[i] : enemyCenters[i], _enemyStoneColor);
    }

    private void ShowMarker(int index, Vector2 center, Color color)
    {
        while (_markers.Count <= index)
        {
            Image marker = Instantiate(_magicStoneMarkerTemplate, _magicStoneMarkerLayer);
            marker.name = "MagicStoneMarker";
            _markers.Add(marker);
        }

        float worldSize = SelectedMap.SharedConfig.WorldSize;
        Vector2 normalized = worldSize > 0f ? center / worldSize : Vector2.zero;
        Image image = _markers[index];
        RectTransform rect = image.rectTransform;
        rect.anchorMin = normalized;
        rect.anchorMax = normalized;
        rect.anchoredPosition = Vector2.zero;
        image.color = color;
        image.gameObject.SetActive(true);
    }

    private void RefreshStatus()
    {
        string status = !string.IsNullOrEmpty(_failureMessage)
            ? _failureMessage
            : !string.IsNullOrEmpty(_preparationMessage)
                ? _preparationMessage
                : Availability.Message;
        SetText(_summaryAvailabilityText, status);
        SetText(_overlayAvailabilityText, status);
    }

    private void RefreshInteraction()
    {
        if (_openSelectionButton != null)
            _openSelectionButton.interactable = _interactionEnabled && _availableOptions.Count > 0;
        bool canNavigate = _interactionEnabled && _availableOptions.Count > 1;
        if (_previousButton != null) _previousButton.interactable = canNavigate;
        if (_nextButton != null) _nextButton.interactable = canNavigate;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }
}
