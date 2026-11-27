using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Theme = CountryGovernmentTheme;

public sealed class CountryTimeControls : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private Button[] _modeButtons;
    [SerializeField] private GameObject[] _selectionFrames;
    private DateManager _date;

    private void Awake()
    {
        _modeButtons[0].onClick.AddListener(Stop);
        _modeButtons[1].onClick.AddListener(Play);
        _modeButtons[2].onClick.AddListener(DoubleSpeed);
    }

    private void Start()
    {
        _date = DateManager.Instance;
        if (_date == null)
        {
            Debug.LogError("CountryTimeControls requires DateManager.", this);
            enabled = false;
            return;
        }
        Subscribe();
    }

    private void OnEnable() { if (_date != null) Subscribe(); }

    private void Subscribe()
    {
        _date.OnDateChanged += RefreshDate;
        _date.OnProgressionChanged += RefreshState;
        RefreshState();
    }

    private void Stop() => _date?.Pause();
    private void Play() => _date?.Resume();
    private void DoubleSpeed() => _date?.ResumeDoubleSpeed();

    private void RefreshDate(int month, int day) =>
        _dateText.text = $"{month}月{day}日{(_date.IsAdvancingToMonthEnd ? "　早送り中" : "")}";

    private void RefreshState()
    {
        RefreshDate(_date.CurrentMonth, _date.CurrentDay);
        int speed = _date.IsAdvancingToMonthEnd ? -1 : _date.SpeedMultiplier;
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            bool selected = speed == i;
            _selectionFrames[i].SetActive(selected);
            Theme.Colors(_modeButtons[i], selected ? Theme.Paper : Theme.Face);
        }
    }

    private void OnDisable()
    {
        if (_date == null) return;
        _date.OnDateChanged -= RefreshDate;
        _date.OnProgressionChanged -= RefreshState;
        _date.Pause();
        RefreshState();
    }

    private void OnDestroy()
    {
        _modeButtons[0].onClick.RemoveListener(Stop);
        _modeButtons[1].onClick.RemoveListener(Play);
        _modeButtons[2].onClick.RemoveListener(DoubleSpeed);
    }
}
