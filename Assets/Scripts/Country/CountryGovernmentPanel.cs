using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Theme = CountryGovernmentTheme;

public sealed class CountryGovernmentPanel : MonoBehaviour
{
    private const float FooterHeight = Theme.PageBarHeight + Theme.PageBarNoticeHeight;
    private static readonly string[] PageNames = { "報告", "配分", "建設", "育成" };
    public enum Page { Income, Budget, Construction, Training }
    [SerializeField] private CountryGovernmentData _data;
    [SerializeField] private GameObject _window;
    [SerializeField] private GameObject[] _pages;
    [SerializeField] private UnityEngine.UI.Button[] _tabs;
    [SerializeField] private TextMeshProUGUI _dateText, _treasuryText, _status;
    [SerializeField] private TextMeshProUGUI _treasuryCaption, _fundingBasis;
    [SerializeField] private UnityEngine.UI.Button _backButton, _confirmButton;
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
    private string _feedback = "";
    private bool _feedbackError;
    private int _building;
    private bool _upgrade = true, _cameraInput, _comparisonInput;
    public bool IsOpen => _window.activeSelf;
    public CountryGovernmentDraft Draft => _draft;
    public CountryCameraController CameraControls => _cameraControls;
    public CountryComparisonView Comparison => _comparison;
    public CountryFacilityPopup FacilityPopup => _facilityPopup;
    private CountryDate PlanningStart => CountryDate.From(_date).PlanningStart;
    private CountryDate FundingStart => _resources.HasAppliedFunding(PlanningStart) ? PlanningStart.AddMonths(1) : PlanningStart;
    private CountryDate ResourceStart => _page == Page.Budget ? FundingStart : PlanningStart;
    private string MonthLabel(CountryDate start) => start.MonthIndex == _date.ElapsedMonths ? "今月" : "来月";

    private void Awake()
    {
        for (int i = 0; i < _tabs.Length; i++) { var page = (Page)i; _tabs[i].onClick.AddListener(() => Open(page)); }
        _backButton.onClick.AddListener(PreviousPage);
        _confirmButton.onClick.AddListener(() =>
        {
            if (_page == Page.Construction) ToggleConstruction();
            else if (_page == Page.Training) _training.ConfirmStart();
        });
        if (_constructionCommand != null) _constructionCommand.onClick.AddListener(ToggleConstruction);
        if (_trainingCommand != null) _trainingCommand.onClick.AddListener(_training.ConfirmStart);
        if (_budgetCommand != null) _budgetCommand.onClick.AddListener(NextPage);
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
        _resources.OnGovernmentChanged += ResourcesChanged;
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
            var row = Instantiate(_allocationPrefab, _budgetContent);
            row.name = facility.Name;
            row.Bind(facility, value => { _draft.SetFunding(id, value); Feedback(""); Refresh(); }, () => FundingDetails(id));
            _budgetRows[i] = row;
            var choice = Instantiate(_choicePrefab, _constructionContent);
            choice.name = facility.Name;
            choice.Bind(facility.Name, () => { _building = id; Feedback(""); Refresh(); });
            _buildingRows[i] = choice;
        }
    }

    private void MonthChanged(int month) => OpenMonthlyReport();
    internal void OpenMonthlyReport() { _monthlyFlow = true; Open(Page.Income); }
    private void ResourcesChanged()
    {
        UpdateForecast(ResourceStart);
        if (IsOpen) Refresh();
    }

    private void UpdateForecast(CountryDate start)
    {
        var receipt = _resources.LastIncomeReceipt;
        int startMonth = start.MonthIndex;
        bool hasReceipt = receipt.Recorded && receipt.MonthIndex == startMonth - 1;
        bool projectIncome = startMonth > _date.ElapsedMonths && !hasReceipt;
        var income = hasReceipt ? (receipt.Tax, receipt.Wood, receipt.Ore) : _resources.EstimateMonthlyIncome();
        _draft.UpdateResources(Forecast(_resources.Treasury, income.Tax), income.Tax,
            Forecast(_resources.Wood, income.Wood), Forecast(_resources.Ore, income.Ore),
            reserveFunding: FundingStart.CompareTo(start) == 0, reserveConstruction: PlanningStart.CompareTo(start) == 0);

        int Forecast(int balance, int gain) => projectIncome ? (int)Math.Min((long)balance + gain, int.MaxValue) : balance;
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
        footer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, FooterHeight);
        footer.GetComponent<UnityEngine.UI.Graphic>().enabled = false;
        if (footer.Find("Edge") is Transform edge) edge.gameObject.SetActive(false);
        var main = (RectTransform)_pages[0].transform.parent;
        main.offsetMin = new Vector2(main.offsetMin.x, FooterHeight + 16);
        main.offsetMax = new Vector2(main.offsetMax.x, -16);
        foreach (string path in new[] { "Budget/Facilities/Workboard", "Construction/Content/Workboard" })
        {
            var board = (RectTransform)main.Find(path);
            if (board == null) continue;
            board.offsetMin = new Vector2(0, board.offsetMin.y);
            board.offsetMax = new Vector2(0, board.offsetMax.y);
        }

        var navigation = (RectTransform)_tabs[0].transform.parent;
        _pageHeading = Instantiate(_tabs[0].GetComponentInChildren<TextMeshProUGUI>(), navigation);
        _pageHeading.name = "PageHeading";
        _pageHeading.gameObject.SetActive(true);
        _pageHeading.fontSize = 32;
        _pageHeading.color = Theme.Text;
        _pageHeading.raycastTarget = false;
        navigation.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = false;
        Place(_pageHeading.transform, 0, 1);
        foreach (var tab in _tabs) tab.gameObject.SetActive(false);
        var header = (RectTransform)navigation.parent;
        header.SetParent(footer, false);
        header.anchorMin = Vector2.zero; header.anchorMax = Vector2.one;
        header.offsetMin = header.offsetMax = Vector2.zero;
        header.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = false;

        const float headingX = Theme.PageBarInset + Theme.PageBarBackWidth + 20;
        Theme.PlacePageItem(navigation, 0, headingX, 220, 64, 34);
        Theme.PlacePageItem(_dateText.rectTransform, 0, headingX, 220, 32, 30);
        _dateText.fontSize = 23;
        SetupPageHeader(main, header);

        _nextPage = Instantiate(_backButton, footer);
        _nextPage.name = "NextPage";
        _nextPage.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        _nextPage.onClick.AddListener(NextPage);
        Theme.PlacePageItem((RectTransform)_backButton.transform, 0, Theme.PageBarInset, Theme.PageBarBackWidth);
        Theme.Colors(_nextPage, new Color32(133, 190, 179, 255));
        foreach (var button in new[] { _confirmButton, _budgetCommand, _constructionCommand, _trainingCommand })
        {
            if (button == null) continue;
            Theme.PlacePageItem((RectTransform)button.transform, 1, -Theme.PageBarInset, Theme.PageBarActionWidth);
            CenterCommandArt(button.transform.Find("Icon"), Theme.PageBarButtonHeight);
        }
        CenterCommandArt(_constructionCommand?.transform.Find("Surface/LeftBrace"), 76);
        CenterCommandArt(_constructionCommand?.transform.Find("Surface/RightBrace"), 76);
        CenterCommandArt(_trainingCommand?.transform.Find("Surface/Staff"), Theme.PageBarButtonHeight);
        CenterCommandArt(_budgetCommand?.transform.Find("Surface/Seal"), 56);
        Theme.PlacePageItem(_status.rectTransform, 1, -Theme.PageBarInset, 620,
            Theme.PageBarHeight + Theme.PageBarNoticeHeight / 2, Theme.PageBarNoticeHeight);
        _status.alignment = TextAlignmentOptions.MidlineRight;
        _status.fontSize = 24;
        _status.textWrappingMode = TextWrappingModes.NoWrap;
        _status.overflowMode = TextOverflowModes.Ellipsis;
        _status.raycastTarget = false;
    }

    private static void CenterCommandArt(Transform part, float maxHeight)
    {
        if (part is not RectTransform rect) return;
        float height = Mathf.Min(rect.rect.height, maxHeight);
        rect.anchorMin = new Vector2(rect.anchorMin.x, .5f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, .5f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, (rect.pivot.y - .5f) * height);
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

        var treasury = (RectTransform)_treasuryText.transform.parent;
        var treasuryIcon = treasury.Find("Icon").GetComponent<UnityEngine.UI.Image>();
        treasuryIcon.gameObject.SetActive(false);
        PlaceStack(_treasuryCaption.transform, 0, 1, true);
        PlaceStack(_treasuryText.transform, 0, 1, false);
        _treasuryCaption.fontSize = 24;
        _treasuryText.fontSize = 34;
        AddMoneyIcon(_treasuryText, treasuryIcon.sprite, 36);

        var budget = (RectTransform)main.Find("Budget/Summary");
        var modes = (RectTransform)main.Find("Construction/Modes");
        var schools = (RectTransform)_training.transform.Find("Schools");
        _headerDetails = new[] { budget.gameObject, modes.gameObject, schools.gameObject };
        foreach (var detail in _headerDetails) detail.transform.SetParent(header, false);

        Theme.PlacePageItem(budget, .5f, 0, 650);
        budget.GetComponent<UnityEngine.UI.Graphic>().enabled = false;
        budget.Find("TaxIcon").gameObject.SetActive(false);
        PlaceStack(_taxBudgetCaption.transform, 0, .26f, true);
        PlaceStack(_taxBudget.transform, 0, .26f, false);
        PlaceStack(budget.Find("TotalCaption"), .29f, .55f, true);
        PlaceStack(_fundingTotal.transform, .29f, .55f, false);
        _cashFlowCaption.gameObject.SetActive(false);
        Place(_useTreasury.transform, .58f, 1);
        var switchLabel = _treasuryToggleLabel;
        switchLabel.alignment = TextAlignmentOptions.MidlineRight;
        switchLabel.margin = new Vector4(0, 0, 10, 0);
        foreach (var label in budget.GetComponentsInChildren<TextMeshProUGUI>(true)) label.fontSize = 24;
        _taxBudget.fontSize = _fundingTotal.fontSize = 32;
        var coin = _tabs[0].transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite;
        AddMoneyIcon(_taxBudget, coin);
        AddMoneyIcon(_fundingTotal, coin);
        Theme.PlacePageItem(_cashFlow.rectTransform, 1, 0, 276, 110, 32);
        _cashFlow.alignment = TextAlignmentOptions.Center;

        main.Find("Construction/Guide").gameObject.SetActive(false);
        modes.GetComponent<UnityEngine.UI.Graphic>().enabled = false;
        modes.Find("Caption").gameObject.SetActive(false);
        _orderCount.gameObject.SetActive(false);
        Theme.PlacePageItem(modes, .5f, 0, 500);
        Place(_upgradeButton.transform, 0, .5f, 4);
        Place(_newButton.transform, .5f, 1, 4);
        Theme.PlacePageItem(schools, .5f, 0, 684);
        schools.Find("SchoolName").gameObject.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            var school = schools.Find($"School{i + 1}");
            Place(school, i / 3f, (i + 1) / 3f, 4);
            if (school.Find("Icon") is RectTransform icon)
                icon.anchoredPosition = new Vector2(icon.anchoredPosition.x, -12);
            school.Find("Label").GetComponent<TextMeshProUGUI>().fontSize = 26;
        }
        var facilities = (RectTransform)main.Find("Budget/Facilities"); facilities.offsetMax = new Vector2(facilities.offsetMax.x, -8);
        LayoutConstruction((RectTransform)main.Find("Construction/Content"));
        var selection = (RectTransform)_training.transform.Find("Selection"); selection.offsetMax = new Vector2(selection.offsetMax.x, 0);
        foreach (var detail in _headerDetails) detail.SetActive(false);
    }

    private static void LayoutConstruction(RectTransform content)
    {
        const float split = .28f;
        content.offsetMax = new Vector2(content.offsetMax.x, -8);
        var left = (RectTransform)content.Find("Left");
        var right = (RectTransform)content.Find("Right");
        var detail = (RectTransform)right.Find("Detail");
        left.anchorMax = new Vector2(split, 1);
        left.offsetMin = detail.offsetMin;
        left.offsetMax = new Vector2(-32, detail.offsetMax.y);
        right.anchorMin = new Vector2(split, 0);
        var divider = (RectTransform)content.Find("ColumnDivider");
        divider.anchorMin = new Vector2(split, 0); divider.anchorMax = new Vector2(split, 1);
        divider.offsetMin = new Vector2(-1, detail.offsetMin.y); divider.offsetMax = new Vector2(1, detail.offsetMax.y);

        var title = detail.Find("Title");
        var plaque = (RectTransform)title.Find("Plaque");
        plaque.anchoredPosition = Vector2.zero;
        float headingHeight = plaque.rect.height;
        ((RectTransform)left.Find("Caption")).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headingHeight);
        ((RectTransform)left.Find("FacilityList")).offsetMax = new Vector2(0, -headingHeight - 16);
        var symbol = (RectTransform)title.Find("Symbol");
        symbol.anchoredPosition = new Vector2(20, (symbol.rect.height - headingHeight) * .5f);
        var name = (RectTransform)title.Find("Name");
        name.anchoredPosition = new Vector2(name.anchoredPosition.x, -4);
        ((RectTransform)title.Find("Level")).anchoredPosition = new Vector2(0, -4);
        var empty = (RectTransform)right.Find("Empty");
        empty.offsetMin = detail.offsetMin; empty.offsetMax = detail.offsetMax;
    }

    private void LateUpdate()
    {
        if (_headerBackground != null && IsOpen) UpdateHeaderBackground();
    }

    private void UpdateHeaderBackground()
    {
        CountrySafeArea.FitBottomBand(_headerBackground, (RectTransform)_shell.transform, FooterHeight);
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
        rect.anchorMin = new Vector2(left, caption ? .41f : .09f);
        rect.anchorMax = new Vector2(right, caption ? .91f : .59f);
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

    private void UpdatePageControls()
    {
        bool report = _page == Page.Income;
        bool budget = _page == Page.Budget;
        bool usePaper = !_monthlyFlow && budget && _budgetCommand != null;
        bool directConfirm = !_monthlyFlow && (budget || _page == Page.Construction);
        _nextPage.gameObject.SetActive((_monthlyFlow || directConfirm) && !usePaper);
        _backButton.gameObject.SetActive(true);
        _backButton.GetComponentInChildren<TextMeshProUGUI>(true).text = !_monthlyFlow || report ? Theme.CountryReturnText : $"‹ {PageNames[(int)_page - 1]}へ";
        _nextPage.GetComponentInChildren<TextMeshProUGUI>().text = directConfirm ? "決定" : _page == Page.Training ? "完了" : $"{PageNames[(int)_page + 1]}へ ›";
        float nextInset = _monthlyFlow || report || budget
            ? Theme.PageBarInset : Theme.PageBarInset + Theme.PageBarActionWidth + 12;
        float nextWidth = !_monthlyFlow && (report || budget) ? Theme.PageBarActionWidth : Theme.PageBarBackWidth;
        Theme.PlacePageItem((RectTransform)_nextPage.transform, 1, -nextInset, nextWidth);
        var command = _page == Page.Construction ? _constructionCommand ?? _confirmButton :
            _page == Page.Training ? _trainingCommand ?? _confirmButton : null;
        if (command != null)
        {
            float inset = Theme.PageBarInset + (_monthlyFlow ? nextWidth + 12 : 0);
            Theme.PlacePageItem((RectTransform)command.transform, 1, -inset, Theme.PageBarActionWidth);
        }

        var treasury = (RectTransform)_treasuryText.transform.parent;
        treasury.gameObject.SetActive(report || budget);
        if (_monthlyFlow || budget)
            Theme.PlacePageItem(treasury, 1, report ? -Theme.PageBarInset - nextWidth - 24 : -Theme.PageBarInset,
                report ? 174 : Theme.PageBarActionWidth, report ? Theme.PageBarHeight / 2 : Theme.PageBarHeight + 18,
                report ? Theme.PageBarButtonHeight : Theme.PageBarNoticeHeight);
        else
            Theme.PlacePageItem(treasury, 1, -Theme.PageBarInset, 174);
        _treasuryCaption.gameObject.SetActive(report);
        _treasuryText.fontSize = budget ? 24 : 34;
        if (budget) Place(_treasuryText.transform, 0, 1);
        else PlaceStack(_treasuryText.transform, 0, 1, false);
        if (_budgetCommand != null)
        {
            _budgetCommand.gameObject.SetActive(usePaper);
            if (usePaper) _budgetCommandLabel.text = _monthlyFlow ? "建設へ ›" : "決定";
        }
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

    private void ToggleConstruction()
    {
        if (_building < 0) return;
        Feedback("");
        if (_draft.IsOrdered(_building)) _draft.RemoveConstruction(_building);
        else _draft.PlanConstruction(_building, _upgrade);
        Refresh();
    }

    private void Complete()
    {
        bool funding = _monthlyFlow || _page == Page.Budget;
        bool construction = _monthlyFlow || _page == Page.Construction;
        if (funding)
        {
            UpdateForecast(FundingStart);
            bool canFund = _draft.CanConfirmFunding;
            UpdateForecast(ResourceStart);
            if (!canFund) { Feedback("配分に必要な資金が不足している", true); return; }
        }
        if ((_monthlyFlow || _page == Page.Training) && !_training.CommitDrafts(out string reason)) { Feedback(reason, true); return; }
        _resources.ConfirmGovernment(_draft, CountryDate.From(_date), FundingStart, funding, construction);
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
        _pageHeading.text = PageNames[(int)_page];
        for (int i = 0; i < _headerDetails.Length; i++) _headerDetails[i].SetActive((int)_page == i + 1);
        UpdatePageControls();
        _dateText.text = _page switch
        {
            Page.Budget => $"{FundingStart.Month}月分",
            Page.Construction => $"{PlanningStart}着工",
            _ => $"{_date.CurrentMonth}月{_date.CurrentDay}日"
        };
        string treasury = CountryResourceDisplay.Format(_page == Page.Income ? _resources.Treasury : _draft.TreasuryForecast);
        _treasuryText.text = _page == Page.Budget ? $"計画後国庫 {treasury}" : treasury;
        _treasuryCaption.text = _page == Page.Income ? "国庫" : "計画後国庫";
        var receipt = _resources.LastIncomeReceipt;
        _incomeTitle.text = "報告書";
        _incomeState.text = receipt.Recorded ? $"{receipt.Month}月分・受取済み" : "まだ受取はない";
        _taxGain.text = $"＋{CountryResourceDisplay.Format(receipt.Tax)}"; _woodGain.text = $"＋{CountryResourceDisplay.Format(receipt.Wood)}"; _oreGain.text = $"＋{CountryResourceDisplay.Format(receipt.Ore)}";
        if (_taxStock != null) _taxStock.text = CountryResourceDisplay.Format(_resources.Treasury);
        _woodStock.text = CountryResourceDisplay.Format(_resources.Wood); _oreStock.text = CountryResourceDisplay.Format(_resources.Ore);
        if (_page == Page.Income) _expeditionReport?.Refresh();
        _budgetTitle.text = $"{MonthLabel(FundingStart)} {FundingStart.Month}月の配分";
        _constructionTitle.text = $"{MonthLabel(PlanningStart)} {PlanningStart.Month}月の建設";
        _taxBudgetCaption.text = _monthlyFlow && _date.CurrentDay == 1 && receipt.Recorded ? "税収" : "税収見込";
        _taxBudget.text = CountryResourceDisplay.Format(_draft.Tax);
        _fundingTotal.text = CountryResourceDisplay.Format(_draft.FundingTotal);
        _cashFlow.text = _draft.TreasuryUsed > 0
            ? $"国庫から {CountryResourceDisplay.Format(_draft.TreasuryUsed)} 補填"
            : $"国庫へ {CountryResourceDisplay.Format(_draft.RemainingTax)}";
        _cashFlow.gameObject.SetActive(_draft.TreasuryUsed > 0 || _draft.RemainingTax != _draft.TreasuryForecast);
        _cashFlow.color = CountryGovernmentTheme.Text;
        _useTreasury.SetIsOnWithoutNotify(_draft.UseTreasury);
        _treasuryToggleLabel.text = "国庫も使う";
        _fundingBasis.text = $"100%の基準：{(_draft.UseTreasury ? "税収＋国庫" : "税収")} {CountryResourceDisplay.Format(_draft.FundingBasis)}　／　金色の区間で調整";
        for (int i = 0; i < _budgetRows.Length; i++)
        {
            bool built = _draft.Level(i) > 0;
            _budgetRows[i].gameObject.SetActive(built);
            if (built) _budgetRows[i].Refresh(_draft.Funding(i), _draft.MaximumFunding(i), _draft.FundingBasis, FundingEffect(i));
        }
        RefreshConstruction();
        _training.Refresh();
        bool ordered = _building >= 0 && _draft.IsOrdered(_building);
        bool canConstruct = _building >= 0 && _draft.CanConstruct(_building, _upgrade);
        string construction = ordered ? "計画を外す" : _upgrade ? "強化を計画" : "新築を計画";
        string training = _date.CurrentDay == 1 ? "訓練を開始" : "訓練を予約";
        if (_constructionCommand != null)
            ShowCommand(_constructionCommand, _constructionCommandLabel, _page == Page.Construction, ordered || canConstruct, construction);
        if (_trainingCommand != null)
            ShowCommand(_trainingCommand, _trainingCommandLabel, _page == Page.Training, _training.CanStart, training);
        bool fallback = (_page == Page.Construction && _constructionCommand == null) || (_page == Page.Training && _trainingCommand == null);
        ShowCommand(_confirmButton, _confirmLabel, fallback,
            _page == Page.Construction ? ordered || canConstruct : _training.CanStart,
            _page == Page.Construction ? construction : training);
        if (fallback)
        {
            _confirmFrame.color = Theme.Border;
            _confirmIcon.sprite = _confirmIcons[(int)_page];
        }

        string notice = _feedback;
        bool error = _feedbackError;
        if (string.IsNullOrEmpty(notice))
        {
            if (_page == Page.Budget && !_draft.CanConfirmFunding)
            {
                notice = "配分に必要な資金が不足している";
                error = true;
            }
            else if (_page == Page.Construction)
            {
                if (_building < 0) notice = "施設を選ぶ";
                else if (!ordered && !canConstruct)
                {
                    var cost = _draft.Cost(_building, _upgrade);
                    string missing = _draft.TreasuryForecast < cost.Money ? $" 国庫 {CountryResourceDisplay.Format(cost.Money - _draft.TreasuryForecast)}" : "";
                    if (_draft.WoodForecast < cost.Wood) missing += $" 木材 {CountryResourceDisplay.Format(cost.Wood - _draft.WoodForecast)}";
                    if (_draft.OreForecast < cost.Ore) missing += $" 鉱石 {CountryResourceDisplay.Format(cost.Ore - _draft.OreForecast)}";
                    notice = "不足：" + missing;
                    error = true;
                }
                else if (_draft.OrderCount > 0) notice = _orderCount.text;
            }
            else if (_page == Page.Training)
                notice = _training.CanStart ? $"{_training.SelectedCount}人・{_training.StartDate}開始" : "訓練対象を選ぶ";
        }
        SetNotice(notice, error);
    }

    private static void ShowCommand(UnityEngine.UI.Button button, TextMeshProUGUI label, bool visible, bool available, string text)
    {
        button.interactable = available;
        label.text = text;
        button.gameObject.SetActive(visible);
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
            _buildingRows[i].Refresh(_draft.IsOrdered(i) ? "計画済み" : _upgrade ? $"Lv.{_draft.Level(i)}" : "未建設", i == _building);
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
        _level.text = _upgrade ? $"Lv.{_draft.Level(_building)} → Lv.{_draft.Level(_building) + 1}" : "新築 Lv.1";
        _moneyCost.text = CountryResourceDisplay.Format(cost.Money); _woodCost.text = CountryResourceDisplay.Format(cost.Wood); _oreCost.text = CountryResourceDisplay.Format(cost.Ore);
        _months.text = $"{cost.Months}か月";
        _constructionEffect.text = ConstructionEffect(facility);
    }

    private string ConstructionEffect(CountryGovernmentFacility facility)
    {
        if (_upgrade && facility.Name == CountryGovernmentData.SpiritSchool)
        {
            int spiritLevel = _draft.Level(_building);
            int before = CountryGovernmentData.SpiritCPBonus(spiritLevel);
            int after = CountryGovernmentData.SpiritCPBonus(spiritLevel + 1);
            return $"全員の最大CP ＋{before} → ＋{after}";
        }
        if (!_upgrade || !facility.TrainingSchool) return facility.Role;
        int level = _draft.Level(_building);
        return $"訓練定員 {Mathf.Min(7, level + 2)}人 → {Mathf.Min(7, level + 3)}人 ／ 習得できる学校Lv {level} → {Mathf.Min(5, level + 1)}";
    }

    private string FundingEffect(int id)
    {
        var facility = _data.Facilities[id];
        if (facility.Name != CountryGovernmentData.Blacksmith || _resources.Roster == null) return facility.Role;
        if (_draft.Funding(id) < facility.Minimum) return "配分不足・製作なし";
        float chance = _resources.Roster.HighestTierChance(_draft.Level(id), _draft.Funding(id), facility.Minimum);
        return $"最高段階の出現率　{chance:P0}";
    }

    private void FundingDetails(int id)
    {
        var facility = _data.Facilities[id];
        string effect = facility.Name == CountryGovernmentData.Blacksmith ? "\n" + FundingEffect(id) : "";
        string role = facility.Role.Length == 0 ? "" : $"\n\n施設の役割\n{facility.Role}";
        Details(facility.Name + "の配分", $"{MonthLabel(FundingStart)} {FundingStart}から適用\n最低必要額　{CountryResourceDisplay.Format(facility.Minimum)}\n今回の配分　{CountryResourceDisplay.Format(_draft.Funding(id))}\n追加配分　{CountryResourceDisplay.Format(Math.Max(0, _draft.Funding(id) - facility.Minimum))}{role}{effect}");
    }
    private void ConstructionDetails()
    {
        if (_building < 0) return;
        var facility = _data.Facilities[_building]; var cost = _draft.Cost(_building, _upgrade);
        string effect = ConstructionEffect(facility);
        string role = effect.Length == 0 ? "" : $"\n\n完成後の役割\n{effect}";
        Details(facility.Name + "の建設内容", $"{_level.text}\n{MonthLabel(PlanningStart)} {PlanningStart}着工 → {PlanningStart.AddMonths(cost.Months)}完成予定\n費用　{CountryResourceDisplay.Format(cost.Money)} ／ 木材 {CountryResourceDisplay.Format(cost.Wood)} ／ 鉱石 {CountryResourceDisplay.Format(cost.Ore)}\n工期　{cost.Months}か月{role}");
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
        _feedback = message;
        _feedbackError = error;
        SetNotice(message, error);
    }

    private void SetNotice(string message, bool error)
    {
        _status.text = message;
        _status.color = error ? Theme.Error : Theme.Text;
        _status.gameObject.SetActive(!string.IsNullOrEmpty(message));
        if (_page == Page.Budget)
            _treasuryText.transform.parent.gameObject.SetActive(string.IsNullOrEmpty(message));
    }
    private static void SetSelected(UnityEngine.UI.Button button, bool selected)
    {
        CountryGovernmentTheme.SelectTab(button, selected);
    }

    private void OnDisable()
    {
        if (_date != null) _date.OnMonthChanged -= MonthChanged;
        if (_resources != null)
        {
            _resources.OnResourcesChanged -= ResourcesChanged;
            _resources.OnGovernmentChanged -= ResourcesChanged;
        }
        Close();
    }
    private void OnDestroy() => _cancel?.Dispose();
}
