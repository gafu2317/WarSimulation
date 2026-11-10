using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CountryGovernmentPanel : MonoBehaviour
{
    public enum Page { Income, Budget, Construction, Training }
    [SerializeField] private CountryGovernmentData _data;
    [SerializeField] private GameObject _window;
    [SerializeField] private GameObject[] _pages;
    [SerializeField] private UnityEngine.UI.Button[] _tabs;
    [SerializeField] private TextMeshProUGUI _dateText, _treasuryText, _status;
    [SerializeField] private TextMeshProUGUI _treasuryCaption, _fundingBasis;
    [SerializeField] private UnityEngine.UI.Button _backButton, _confirmButton;
    [SerializeField] private Sprite _roundedPanel;
    [SerializeField] private TextMeshProUGUI _confirmLabel;
    [SerializeField] private UnityEngine.UI.Button _constructionCommand;
    [SerializeField] private TextMeshProUGUI _constructionCommandLabel;
    [SerializeField] private UnityEngine.UI.Button _budgetCommand, _trainingCommand;
    [SerializeField] private TextMeshProUGUI _budgetCommandLabel, _trainingCommandLabel;
    [SerializeField] private UnityEngine.UI.Graphic _confirmFrame;
    [SerializeField] private TextMeshProUGUI _incomeTitle, _incomeState, _taxGain, _woodGain, _oreGain, _taxStock, _woodStock, _oreStock;
    [SerializeField] private TextMeshProUGUI _taxBudget, _fundingTotal, _cashFlow, _treasuryToggleLabel;
    [SerializeField] private TextMeshProUGUI _taxBudgetCaption, _cashFlowCaption;
    [SerializeField] private UnityEngine.UI.Image _confirmIcon;
    [SerializeField] private Sprite[] _confirmIcons;
    [SerializeField] private TextMeshProUGUI _budgetTitle, _constructionTitle;
    [SerializeField] private UnityEngine.UI.Toggle _useTreasury;
    [SerializeField] private RectTransform _budgetContent, _constructionContent;
    [SerializeField] private CountryGovernmentAllocationRow _allocationPrefab;
    [SerializeField] private CountryGovernmentChoice _choicePrefab;
    [SerializeField] private UnityEngine.UI.ScrollRect _constructionScroll;
    [SerializeField] private UnityEngine.UI.Button _upgradeButton, _newButton;
    [SerializeField] private TextMeshProUGUI _constructionName, _level, _moneyCost, _woodCost, _oreCost, _months, _constructionEffect, _orderCount;
    [SerializeField] private CountryFacilitySymbol _constructionSymbol;
    [SerializeField] private GameObject _constructionDetail;
    [SerializeField] private TextMeshProUGUI _constructionEmpty;
    [SerializeField] private UnityEngine.UI.Button _constructionDetailsButton;
    [SerializeField] private CountryTrainingView _training;
    [SerializeField] private GameObject _detailModal;
    [SerializeField] private TextMeshProUGUI _detailTitle, _detailBody;
    [SerializeField] private UnityEngine.UI.Button _detailClose;
    [SerializeField] private CanvasGroup _shell;
    [SerializeField] private CountryCameraController _cameraControls;
    [SerializeField] private CountryComparisonView _comparison;
    [SerializeField] private CountryFacilityPopup _facilityPopup;
    [SerializeField] private CountryExpeditionPanel _expeditions;
    [SerializeField] private CountryDefensePanel _defense;
    [SerializeField] private CountryExpeditionReportView _expeditionReport;
    private CountryGovernmentDraft _draft;
    private CountryGovernmentAllocationRow[] _budgetRows;
    private CountryGovernmentChoice[] _buildingRows;
    private DateManager _date;
    private PlayDataManager _resources;
    private InputAction _cancel;
    private GameObject _returnFocus, _detailReturnFocus;
    private Page _page;
    private bool _monthlyFlow;
    private UnityEngine.UI.Button _nextPage;
    private TextMeshProUGUI _pageHeading;
    private RectTransform _headerBackground;
    private GameObject[] _headerDetails;
    private RectTransform[] _commandRects;
    private Vector2[] _commandMin, _commandMax, _commandPivot, _commandPosition, _commandSize;
    private Vector2 _statusMin, _statusMax, _statusOffsetMin, _statusOffsetMax;
    private int _building;
    private bool _upgrade = true, _cameraInput, _comparisonInput;
    public bool IsOpen => _window.activeSelf;
    public CountryGovernmentDraft Draft => _draft;
    public CountryCameraController CameraControls => _cameraControls;
    public CountryComparisonView Comparison => _comparison;
    public CountryFacilityPopup FacilityPopup => _facilityPopup;
    private CountryDate PlanningStart => !_monthlyFlow && _date.CurrentDay == 1
        ? CountryDate.From(_date).AddMonths(1) : CountryDate.From(_date).PlanningStart;
    private string PlanningMonth => _monthlyFlow && _date.CurrentDay == 1 ? "今月" : "来月";

    private void Awake()
    {
        for (int i = 0; i < _tabs.Length; i++) { var page = (Page)i; _tabs[i].onClick.AddListener(() => Open(page)); }
        _backButton.onClick.AddListener(PreviousPage);
        _useTreasury.onValueChanged.AddListener(SetTreasury);
        _upgradeButton.onClick.AddListener(() => SetConstructionMode(true));
        _newButton.onClick.AddListener(() => SetConstructionMode(false));
        _constructionDetailsButton.onClick.AddListener(ConstructionDetails);
        _detailClose.onClick.AddListener(CloseDetails);
        _cancel = new InputAction("Close government", InputActionType.Button, "<Keyboard>/escape");
        _cancel.performed += _ => { if (_expeditionReport != null && _expeditionReport.DetailsOpen) _expeditionReport.CloseDetails(); else if (_training.ConfirmationOpen) _training.CloseConfirmation(); else if (_detailModal.activeSelf) CloseDetails(); else Close(); };
        _training.Changed += Refresh;
    }

    private void Start()
    {
        _date = DateManager.Instance;
        _resources = PlayDataManager.Instance;
        if (_date == null || _resources == null)
        {
            Debug.LogError("CountryGovernmentPanel requires DateManager and PlayDataManager.", this);
            enabled = false;
            return;
        }
        _draft = _resources.GetOrCreateGovernmentDraft(_data);
        SetupPages();
        CreateRows();
        _training.Initialize(_resources.GetOrCreateRoster(_training.Data, CountryDate.From(_date)));
        Subscribe();
    }
    private void OnEnable()
    {
        if (_date == null || _resources == null) return;
        _draft = _resources.GetOrCreateGovernmentDraft(_data);
        Subscribe();
    }
    private void Subscribe()
    {
        _date.OnMonthChanged += MonthChanged;
        _resources.OnResourcesChanged += ResourcesChanged;
        ResourcesChanged();
    }

    private void CreateRows()
    {
        int count = _data.Facilities.Length;
        _budgetRows = new CountryGovernmentAllocationRow[count];
        _buildingRows = new CountryGovernmentChoice[count];
        for (int i = 0; i < count; i++)
        {
            int id = i;
            var facility = _data.Facilities[i];
            if (facility.InitialLevel > 0)
            {
                var row = Instantiate(_allocationPrefab, _budgetContent);
                row.name = facility.Name;
                row.Bind(facility, value => { _draft.SetFunding(id, value); Feedback(""); Refresh(); }, () => FundingDetails(id));
                _budgetRows[i] = row;
            }
            var choice = Instantiate(_choicePrefab, _constructionContent);
            choice.name = facility.Name;
            choice.Bind(facility.Name, () => { _building = id; ToggleConstruction(id); });
            _buildingRows[i] = choice;
        }
    }

    private void MonthChanged(int month) { _monthlyFlow = true; Open(Page.Income); }
    private void ResourcesChanged()
    {
        var receipt = _resources.LastIncomeReceipt;
        bool projectIncome = !_monthlyFlow || (_date.CurrentDay != 1 && (!receipt.Recorded || receipt.MonthIndex < _date.ElapsedMonths));
        int tax = _monthlyFlow && _date.CurrentDay == 1 && receipt.Recorded ? receipt.Tax : (int)_resources.MonthlyTaxIncome;
        _draft.UpdateResources(_resources.Treasury + (projectIncome ? (int)_resources.MonthlyTaxIncome : 0), tax,
            _resources.Wood + (projectIncome ? (int)_resources.MonthlyWoodIncome : 0),
            _resources.Ore + (projectIncome ? (int)_resources.MonthlyOreIncome : 0), taxIncluded: receipt.Recorded || projectIncome);
        if (IsOpen) Refresh();
    }

    public void OpenIncome() => OpenDirect(Page.Income);
    public void OpenBudget() => OpenDirect(Page.Budget);
    public void OpenConstruction() => OpenDirect(Page.Construction);
    public void OpenTraining() => OpenDirect(Page.Training);
    private void OpenDirect(Page page) { _monthlyFlow = false; Open(page); }
    public void Open(Page page)
    {
        if (_date == null || _resources == null) return;
        _expeditions?.Close();
        _defense?.Close();
        _date.Pause();
        if (!IsOpen)
        {
            _draft = _resources.GetOrCreateGovernmentDraft(_data).Copy();
            _facilityPopup?.Close();
            _returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _cameraInput = _cameraControls != null && _cameraControls.InputEnabled;
            _comparisonInput = _comparison != null && _comparison.InputEnabled;
            if (_cameraControls != null) _cameraControls.InputEnabled = false;
            if (_comparison != null) _comparison.InputEnabled = false;
            _window.SetActive(true);
            _cancel.Enable();
        }
        CloseDetails();
        _expeditionReport?.CloseDetails();
        _page = page;
        Feedback("");
        ResourcesChanged();
        if (_backButton.gameObject.activeInHierarchy) _backButton.Select();
        else if (_nextPage.gameObject.activeInHierarchy) _nextPage.Select();
        else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void Close()
    {
        if (!IsOpen) return;
        CloseDetails();
        _expeditionReport?.CloseDetails();
        _training.CloseConfirmation();
        if (!_monthlyFlow) _resources.SetGovernmentDraft(_draft.Copy());
        _monthlyFlow = false;
        _window.SetActive(false);
        _cancel?.Disable();
        if (_cameraControls != null) _cameraControls.InputEnabled = _cameraInput;
        if (_comparison != null) _comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }

    private void SetupPages()
    {
        var footer = (RectTransform)_backButton.transform.parent;
        var footerGraphic = footer.GetComponent<UnityEngine.UI.Graphic>();
        if (footerGraphic != null) footerGraphic.enabled = false;
        var edge = footer.Find("Edge"); if (edge != null) edge.gameObject.SetActive(false);
        var main = (RectTransform)_pages[0].transform.parent;
        main.offsetMin = new Vector2(main.offsetMin.x, 0);
        foreach (string path in new[] { "Budget/Facilities/Workboard", "Construction/Content/Workboard" })
        {
            var board = (RectTransform)main.Find(path);
            if (board == null) continue;
            board.offsetMin = new Vector2(0, board.offsetMin.y);
            board.offsetMax = new Vector2(0, board.offsetMax.y);
        }
        var navigation = _tabs[0].transform.parent;
        var source = _tabs[0].GetComponentInChildren<TextMeshProUGUI>();
        _pageHeading = Instantiate(source, navigation); _pageHeading.name = "PageHeading";
        _pageHeading.gameObject.SetActive(true); _pageHeading.fontSize = 36;
        _pageHeading.color = CountryGovernmentTheme.Text; _pageHeading.raycastTarget = false;
        var headingLayout = _pageHeading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        headingLayout.flexibleWidth = 1;
        foreach (var tab in _tabs) tab.gameObject.SetActive(false);
        SetupPageHeader(main, (RectTransform)navigation.parent);
        _nextPage = Instantiate(_backButton, _backButton.transform.parent);
        _nextPage.name = "NextPage"; _nextPage.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        _nextPage.onClick.AddListener(NextPage);
        var backRect = (RectTransform)_backButton.transform;
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = Vector2.zero;
        backRect.anchoredPosition = new Vector2(72, 40); backRect.sizeDelta = new Vector2(224, 112);
        var nextRect = (RectTransform)_nextPage.transform;
        nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(1, 0);
        nextRect.anchoredPosition = new Vector2(-72, 40); nextRect.sizeDelta = new Vector2(224, 112);
        StylePageButton(_backButton, _roundedPanel, CountryGovernmentTheme.Face);
        StylePageButton(_nextPage, _roundedPanel, new Color32(133, 190, 179, 255));
        var buttons = new[] { _confirmButton, _budgetCommand, _constructionCommand, _trainingCommand };
        _commandRects = new RectTransform[buttons.Length];
        _commandMin = new Vector2[buttons.Length]; _commandMax = new Vector2[buttons.Length];
        _commandPivot = new Vector2[buttons.Length]; _commandPosition = new Vector2[buttons.Length]; _commandSize = new Vector2[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var rect = (RectTransform)buttons[i].transform; _commandRects[i] = rect;
            _commandMin[i] = rect.anchorMin; _commandMax[i] = rect.anchorMax; _commandPivot[i] = rect.pivot;
            _commandPosition[i] = rect.anchoredPosition; _commandSize[i] = rect.sizeDelta;
        }
        var status = _status.rectTransform;
        _statusMin = status.anchorMin; _statusMax = status.anchorMax;
        _statusOffsetMin = status.offsetMin; _statusOffsetMax = status.offsetMax;
    }

    private void SetupPageHeader(RectTransform main, RectTransform header)
    {
        var headerImage = header.GetComponent<UnityEngine.UI.Image>();
        var background = new GameObject("HeaderBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        _headerBackground = (RectTransform)background.transform;
        _headerBackground.SetParent(_window.transform, false); _headerBackground.SetAsFirstSibling();
        var backgroundImage = background.GetComponent<UnityEngine.UI.Image>();
        backgroundImage.color = headerImage.color; backgroundImage.raycastTarget = false;
        headerImage.enabled = false;
        UpdateHeaderBackground();
        var navigation = header.Find("Navigation").GetComponent<UnityEngine.UI.LayoutElement>();
        navigation.minWidth = 120; navigation.preferredWidth = 120; navigation.flexibleWidth = 0;
        var treasury = header.Find("Treasury");
        var treasuryLayout = treasury.GetComponent<UnityEngine.UI.LayoutElement>();
        treasuryLayout.minWidth = 300; treasuryLayout.preferredWidth = 300;
        var treasuryIcon = treasury.Find("Icon").GetComponent<UnityEngine.UI.Image>();
        var treasurySprite = treasuryIcon.sprite;
        treasuryIcon.gameObject.SetActive(false);
        PlaceStack(treasury.Find("Caption"), 0, 1, true);
        PlaceStack(treasury.Find("Value"), 0, 1, false);
        treasury.Find("Caption").GetComponent<TextMeshProUGUI>().fontSize = 24;
        treasury.Find("Value").GetComponent<TextMeshProUGUI>().fontSize = 30;
        var budget = (RectTransform)main.Find("Budget/Summary");
        var constructionGuide = (RectTransform)main.Find("Construction/Guide");
        var constructionModes = (RectTransform)main.Find("Construction/Modes");
        var schools = (RectTransform)_training.transform.Find("Schools");
        var construction = new GameObject("ConstructionHeader", typeof(RectTransform)).GetComponent<RectTransform>();
        constructionGuide.SetParent(construction, false); constructionModes.SetParent(construction, false);
        _headerDetails = new[] { budget.gameObject, construction.gameObject, schools.gameObject };
        foreach (var detail in _headerDetails)
        {
            detail.transform.SetParent(header, false);
            detail.transform.SetSiblingIndex(header.Find("Treasury").GetSiblingIndex());
            var layout = detail.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = 0; layout.flexibleWidth = 1;
        }
        budget.GetComponent<UnityEngine.UI.Graphic>().enabled = false;
        budget.Find("TaxIcon").gameObject.SetActive(false);
        PlaceStack(budget.Find("TaxCaption"), 0, .24f, true); PlaceStack(budget.Find("Tax"), 0, .24f, false);
        PlaceStack(budget.Find("TotalCaption"), .25f, .49f, true); PlaceStack(budget.Find("Total"), .25f, .49f, false);
        PlaceStack(budget.Find("FlowCaption"), .50f, .74f, true); PlaceStack(budget.Find("Remainder"), .50f, .74f, false);
        Place(budget.Find("UseTreasury"), .75f, 1);
        foreach (var label in budget.GetComponentsInChildren<TextMeshProUGUI>(true)) label.fontSize = 22;
        foreach (string name in new[] { "Tax", "Total", "Remainder" }) budget.Find(name).GetComponent<TextMeshProUGUI>().fontSize = 30;
        var switchLabel = budget.Find("UseTreasury/Label").GetComponent<TextMeshProUGUI>();
        switchLabel.alignment = TextAlignmentOptions.MidlineRight;
        switchLabel.margin = new Vector4(0, 0, 10, 0);
        var coin = _tabs[0].transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite;
        foreach (var value in new[] { _taxBudget, _fundingTotal, _cashFlow }) AddMoneyIcon(value, coin);
        AddMoneyIcon(_treasuryText, treasurySprite, 36);
        constructionGuide.gameObject.SetActive(false);
        constructionModes.Find("Caption").gameObject.SetActive(false);
        constructionModes.Find("Count").gameObject.SetActive(false);
        Place(constructionModes, 0, 1);
        Place(constructionModes.Find("Upgrade"), 0, .18f); Place(constructionModes.Find("New"), .19f, .37f);
        foreach (var label in constructionModes.GetComponentsInChildren<TextMeshProUGUI>(true)) label.fontSize = 26;
        Place(schools.Find("School1"), 0, .18f); Place(schools.Find("School2"), .18f, .36f);
        Place(schools.Find("School3"), .36f, .54f); Place(schools.Find("SchoolName"), .55f, 1);
        foreach (var label in schools.GetComponentsInChildren<TextMeshProUGUI>(true)) label.fontSize = 24;
        var facilities = (RectTransform)main.Find("Budget/Facilities"); facilities.offsetMax = new Vector2(facilities.offsetMax.x, -72);
        var content = (RectTransform)main.Find("Construction/Content"); content.offsetMax = new Vector2(content.offsetMax.x, -72);
        var title = content.Find("Right/Detail/Title");
        ((RectTransform)title.Find("Plaque")).anchoredPosition = new Vector2(-16, 0);
        var titleName = (RectTransform)title.Find("Name");
        titleName.offsetMin = new Vector2(116, -4); titleName.offsetMax = new Vector2(-300, -4);
        ((RectTransform)title.Find("Symbol")).anchoredPosition = new Vector2(0, -8);
        ((RectTransform)title.Find("Level")).anchoredPosition = new Vector2(0, -4);
        var selection = (RectTransform)_training.transform.Find("Selection"); selection.offsetMax = new Vector2(selection.offsetMax.x, 0);
        foreach (var detail in _headerDetails) detail.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_headerBackground != null && IsOpen) UpdateHeaderBackground();
    }

    private void UpdateHeaderBackground()
    {
        var shell = (RectTransform)_shell.transform;
        float top = shell.anchorMax.y;
        _headerBackground.anchorMin = new Vector2(0, top);
        _headerBackground.anchorMax = new Vector2(1, top);
        _headerBackground.pivot = new Vector2(0, 1);
        _headerBackground.anchoredPosition = new Vector2(0, shell.offsetMax.y);
        _headerBackground.sizeDelta = new Vector2(0, ((RectTransform)shell.Find("Header")).rect.height);
    }

    private static void Place(Transform child, float left, float right, float inset = 0)
    {
        var rect = (RectTransform)child;
        rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(right, 1);
        rect.offsetMin = new Vector2(inset, 0); rect.offsetMax = new Vector2(-inset, 0);
    }

    private static void PlaceStack(Transform child, float left, float right, bool caption)
    {
        var rect = (RectTransform)child;
        rect.anchorMin = new Vector2(left, caption ? .5f : .18f);
        rect.anchorMax = new Vector2(right, caption ? .82f : .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void AddMoneyIcon(TextMeshProUGUI value, Sprite coin, float size = 28)
    {
        var iconObject = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var icon = iconObject.GetComponent<UnityEngine.UI.Image>();
        icon.transform.SetParent(value.transform, false);
        icon.sprite = coin; icon.preserveAspect = true; icon.raycastTarget = false;
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(size / 2, 0); rect.sizeDelta = new Vector2(size, size);
        value.margin = new Vector4(size + 8, 0, 0, 0);
    }

    internal static void StylePageButton(UnityEngine.UI.Button button, Sprite roundedPanel, Color color)
    {
        button.GetComponentInChildren<TextMeshProUGUI>(true).color = CountryGovernmentTheme.Ink;
        var outline = button.transform.Find("Outline").GetComponent<UnityEngine.UI.Graphic>();
        outline.enabled = false;
        var border = (UnityEngine.UI.Image)button.targetGraphic;
        border.sprite = roundedPanel; border.type = UnityEngine.UI.Image.Type.Sliced;
        border.color = Color.black; border.raycastTarget = false;
        border.rectTransform.anchorMin = Vector2.zero; border.rectTransform.anchorMax = Vector2.one;
        border.rectTransform.offsetMin = new Vector2(0, 8); border.rectTransform.offsetMax = new Vector2(0, -8);
        var faceObject = new GameObject("RoundedFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var face = faceObject.GetComponent<UnityEngine.UI.Image>();
        face.transform.SetParent(border.transform, false);
        face.sprite = roundedPanel; face.type = UnityEngine.UI.Image.Type.Sliced;
        face.color = Color.white; face.raycastTarget = false;
        face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one;
        face.rectTransform.offsetMin = Vector2.one * 5; face.rectTransform.offsetMax = Vector2.one * -5;
        button.targetGraphic = face;
        CountryGovernmentTheme.Colors(button, color);
        button.GetComponent<CountryButtonFeedback>().SetFocusGraphic(null);
    }
    private void UpdatePageControls()
    {
        _nextPage.gameObject.SetActive(_monthlyFlow);
        _backButton.gameObject.SetActive(!_monthlyFlow || _page != Page.Income);
        _backButton.GetComponentInChildren<TextMeshProUGUI>(true).text = "‹ 戻る";
        _nextPage.GetComponentInChildren<TextMeshProUGUI>().text = !_monthlyFlow || _page == Page.Training ? "完了" : "次へ ›";
        for (int i = 0; i < _commandRects.Length; i++)
        {
            var rect = _commandRects[i]; if (rect == null) continue;
            rect.anchorMin = _monthlyFlow ? new Vector2(.5f, .5f) : _commandMin[i];
            rect.anchorMax = _monthlyFlow ? new Vector2(.5f, .5f) : _commandMax[i];
            rect.pivot = _monthlyFlow ? new Vector2(.5f, .5f) : _commandPivot[i];
            rect.anchoredPosition = _monthlyFlow ? Vector2.zero : _commandPosition[i]; rect.sizeDelta = _commandSize[i];
        }
        var status = _status.rectTransform;
        status.anchorMin = _monthlyFlow ? Vector2.zero : _statusMin;
        status.anchorMax = _monthlyFlow ? new Vector2(.5f, 1) : _statusMax;
        status.offsetMin = _monthlyFlow ? new Vector2(292, 8) : _statusOffsetMin;
        status.offsetMax = _monthlyFlow ? new Vector2(-240, -8) : _statusOffsetMax;
    }
    private void PreviousPage()
    {
        if (!_monthlyFlow || _page == Page.Income) { Close(); return; }
        Open((Page)((int)_page - 1));
    }
    private void NextPage()
    {
        if (!_monthlyFlow || _page == Page.Training) { Complete(); return; }
        Open((Page)((int)_page + 1));
    }

    private void ToggleConstruction(int id)
    {
        if (_draft.IsOrdered(id)) _draft.RemoveConstruction(id);
        else if (!_draft.PlanConstruction(id, _upgrade)) Feedback("建設に必要な資源を確認する", true);
        Refresh();
    }

    private void Complete()
    {
        if ((_monthlyFlow || _page == Page.Budget || _page == Page.Construction) && !_draft.CanConfirmFunding) { Feedback("配分に必要な資金が不足している", true); return; }
        if (!_training.ValidateDrafts(out string reason)) { Feedback(reason, true); return; }
        if (!_training.CommitDrafts(out reason)) { Feedback(reason, true); return; }
        _resources.SetGovernmentDraft(_draft);
        Close();
    }

    private void SetTreasury(bool value)
    {
        if (!_draft.SetUseTreasury(value)) Feedback($"国庫を{CountryResourceDisplay.Format(_draft.TreasuryUsed)}使用中。税収内に戻すとOFFにできる", true);
        else Feedback("");
        Refresh();
    }

    private void SetConstructionMode(bool upgrade)
    {
        _upgrade = upgrade;
        _building = -1;
        Feedback("");
        Refresh();
        Canvas.ForceUpdateCanvases();
        _constructionScroll.verticalNormalizedPosition = 1;
    }

    private void Refresh()
    {
        if (_resources == null) return;
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].SetActive(i == (int)_page);
        }
        _pageHeading.text = new[] { "報告", "配分", "建設", "育成" }[(int)_page];
        for (int i = 0; i < _headerDetails.Length; i++) _headerDetails[i].SetActive((int)_page == i + 1);
        UpdatePageControls();
        _dateText.text = $"{_date.CurrentMonth}月{_date.CurrentDay}日";
        _treasuryText.text = CountryResourceDisplay.Format(_page == Page.Income ? _resources.Treasury : _draft.TreasuryForecast);
        _treasuryCaption.text = _page == Page.Income ? "国庫" : "計画後国庫";
        var receipt = _resources.LastIncomeReceipt;
        _incomeTitle.text = "報告書";
        _incomeState.text = receipt.Recorded ? $"{receipt.Month}月分・受取済み" : "まだ受取はない";
        _taxGain.text = $"＋{CountryResourceDisplay.Format(receipt.Tax)}"; _woodGain.text = $"＋{CountryResourceDisplay.Format(receipt.Wood)}"; _oreGain.text = $"＋{CountryResourceDisplay.Format(receipt.Ore)}";
        if (_taxStock != null) _taxStock.text = CountryResourceDisplay.Format(_resources.Treasury);
        _woodStock.text = CountryResourceDisplay.Format(_resources.Wood); _oreStock.text = CountryResourceDisplay.Format(_resources.Ore);
        if (_page == Page.Income) _expeditionReport?.Refresh();
        _budgetTitle.text = $"{PlanningMonth} {PlanningStart.Month}月の配分";
        _constructionTitle.text = $"{PlanningMonth} {PlanningStart.Month}月の建設";
        _taxBudgetCaption.text = $"{PlanningStart.Month}月の税収{(_monthlyFlow && _date.CurrentDay == 1 && receipt.Recorded ? "" : "見込")}";
        _taxBudget.text = CountryResourceDisplay.Format(_draft.Tax);
        _fundingTotal.text = CountryResourceDisplay.Format(_draft.FundingTotal);
        _cashFlowCaption.text = _draft.TreasuryUsed > 0 ? "国庫から補填" : "残りは国庫へ";
        _cashFlow.text = CountryResourceDisplay.Format(_draft.TreasuryUsed > 0 ? _draft.TreasuryUsed : _draft.RemainingTax);
        _cashFlow.color = CountryGovernmentTheme.Text;
        _useTreasury.SetIsOnWithoutNotify(_draft.UseTreasury);
        _treasuryToggleLabel.text = "国庫も使う";
        _fundingBasis.text = $"100%の基準：{(_draft.UseTreasury ? "税収＋国庫" : "税収")} {CountryResourceDisplay.Format(_draft.FundingBasis)}　／　金色の区間で調整";
        for (int i = 0; i < _budgetRows.Length; i++)
            if (_budgetRows[i] != null) _budgetRows[i].Refresh(_draft.Funding(i), _draft.MaximumFunding(i), _draft.FundingBasis);
        RefreshConstruction();
        _training.Refresh();
        var command = _page switch {
            Page.Budget => _budgetCommand, Page.Construction => _constructionCommand,
            Page.Training => _trainingCommand, _ => null
        };
        _confirmButton.gameObject.SetActive(false);
        _confirmLabel.text = _page switch {
            Page.Budget => $"{PlanningMonth}の配分を確定", Page.Training => _date.CurrentDay == 1 ? "今月の訓練を開始" : "来月の訓練を予約",
            _ => _building >= 0 && _draft.IsOrdered(_building) ? "計画済み" : _date.CurrentDay != 1 ? $"来月から{(_upgrade ? "強化" : "建設")}" : _upgrade ? "強化する" : "建設する"
        };
        _confirmButton.interactable = _page switch {
            Page.Budget => _draft.CanConfirmFunding, Page.Training => _training.CanStart,
            Page.Construction => _building >= 0 && _draft.CanConstruct(_building, _upgrade), _ => true
        };
        _confirmFrame.color = CountryGovernmentTheme.Border;
        _confirmLabel.color = _confirmButton.interactable ? CountryGovernmentTheme.Ink : CountryGovernmentTheme.DisabledText;
        _confirmIcon.sprite = _confirmIcons[(int)_page];
        _confirmIcon.color = _confirmButton.interactable ? Color.white : new Color(1, 1, 1, .4f);
        if (_constructionCommand != null) _constructionCommand.gameObject.SetActive(false);
        if (_budgetCommand != null) _budgetCommand.gameObject.SetActive(false);
        if (_trainingCommand != null) _trainingCommand.gameObject.SetActive(false);
        if (_page == Page.Budget && !_draft.CanConfirmFunding) Feedback("配分に必要な資金が不足している", true);
        if (_page == Page.Construction && _building >= 0 && !_draft.IsOrdered(_building) && !_draft.CanConstruct(_building, _upgrade))
        {
            var cost = _draft.Cost(_building, _upgrade);
            string missing = _draft.TreasuryForecast < cost.Money ? $" 国庫 {CountryResourceDisplay.Format(cost.Money - _draft.TreasuryForecast)}" : "";
            if (_draft.WoodForecast < cost.Wood) missing += $" 木材 {CountryResourceDisplay.Format(cost.Wood - _draft.WoodForecast)}";
            if (_draft.OreForecast < cost.Ore) missing += $" 鉱石 {CountryResourceDisplay.Format(cost.Ore - _draft.OreForecast)}";
            Feedback("不足：" + missing, true);
        }
    }

    private void RefreshConstruction()
    {
        if (_building < 0 || !_draft.IsCandidate(_building, _upgrade))
        {
            _building = -1;
            for (int i = 0; i < _buildingRows.Length; i++) if (_draft.IsCandidate(i, _upgrade)) { _building = i; break; }
        }
        for (int i = 0; i < _buildingRows.Length; i++)
        {
            _buildingRows[i].gameObject.SetActive(_draft.IsCandidate(i, _upgrade));
            _buildingRows[i].Refresh(_draft.IsOrdered(i) ? "計画済み" : _upgrade ? $"Lv.{_data.Facilities[i].InitialLevel}" : "未建設", i == _building);
        }
        SetSelected(_upgradeButton, _upgrade); SetSelected(_newButton, !_upgrade);
        _orderCount.text = $"建設計画 {_draft.OrderCount}件";
        _constructionDetail.SetActive(_building >= 0);
        _constructionEmpty.gameObject.SetActive(_building < 0);
        _constructionEmpty.text = _upgrade ? "強化できる施設はない" : "新築できる施設はない";
        if (_building < 0) return;
        var facility = _data.Facilities[_building];
        var cost = _draft.Cost(_building, _upgrade);
        _constructionName.text = facility.Name;
        _constructionSymbol.SetFacility(facility.Name);
        _level.text = _upgrade ? $"Lv.{facility.InitialLevel} → Lv.{facility.InitialLevel + 1}" : "新築 Lv.1";
        _moneyCost.text = CountryResourceDisplay.Format(cost.Money); _woodCost.text = CountryResourceDisplay.Format(cost.Wood); _oreCost.text = CountryResourceDisplay.Format(cost.Ore);
        _months.text = $"{cost.Months}か月";
        _constructionEffect.text = ConstructionEffect(facility);
    }

    private string ConstructionEffect(CountryGovernmentFacility facility)
    {
        if (!_upgrade || !facility.TrainingSchool) return facility.Role;
        int level = facility.InitialLevel;
        return $"訓練定員 {Mathf.Min(7, level + 2)}人 → {Mathf.Min(7, level + 3)}人 ／ 習得できる学校Lv {level} → {Mathf.Min(5, level + 1)}";
    }

    private void FundingDetails(int id)
    {
        var facility = _data.Facilities[id];
        Details(facility.Name + "の配分", $"{PlanningMonth} {PlanningStart}から適用\n最低必要額　{CountryResourceDisplay.Format(facility.Minimum)}\n今回の配分　{CountryResourceDisplay.Format(_draft.Funding(id))}\n追加配分　{CountryResourceDisplay.Format(_draft.Funding(id) - facility.Minimum)}\n\n施設の役割\n{facility.Role}");
    }
    private void ConstructionDetails()
    {
        if (_building < 0) return;
        var facility = _data.Facilities[_building]; var cost = _draft.Cost(_building, _upgrade);
        Details(facility.Name + "の建設内容", $"{_level.text}\n{PlanningMonth} {PlanningStart}着工 → {PlanningStart.AddMonths(cost.Months)}完成予定\n費用　{CountryResourceDisplay.Format(cost.Money)} ／ 木材 {CountryResourceDisplay.Format(cost.Wood)} ／ 鉱石 {CountryResourceDisplay.Format(cost.Ore)}\n工期　{cost.Months}か月\n\n完成後の役割\n{ConstructionEffect(facility)}");
    }
    private void Details(string title, string body)
    {
        _detailReturnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _detailTitle.text = title; _detailBody.text = body;
        _shell.interactable = _shell.blocksRaycasts = false;
        _detailModal.SetActive(true); _detailClose.Select();
    }
    public void CloseDetails()
    {
        if (!_detailModal.activeSelf) return;
        _detailModal.SetActive(false);
        _shell.interactable = _shell.blocksRaycasts = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_detailReturnFocus);
    }
    private void Feedback(string message, bool error = false)
    {
        string period = _page == Page.Income ? "" : $"{PlanningMonth} {PlanningStart}から適用";
        if (error) message = $"<color=#{ColorUtility.ToHtmlStringRGB(CountryGovernmentTheme.Error)}>{message}</color>";
        _status.text = string.IsNullOrEmpty(message) ? "" : period + "\n" + message;
        _status.gameObject.SetActive(!string.IsNullOrEmpty(message));
        _status.color = CountryGovernmentTheme.Text;
    }
    private static void SetSelected(UnityEngine.UI.Button button, bool selected)
    {
        CountryGovernmentTheme.SelectTab(button, selected);
    }

    private void OnDisable()
    {
        if (_date != null) _date.OnMonthChanged -= MonthChanged;
        if (_resources != null) _resources.OnResourcesChanged -= ResourcesChanged;
        Close();
    }
    private void OnDestroy() => _cancel?.Dispose();
}
