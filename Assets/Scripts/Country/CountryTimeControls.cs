using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class CountryTimeControls : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField, FormerlySerializedAs("_playButton")] private UnityEngine.UI.Button _modeButton;
    [SerializeField] private GameObject _stopIcon, _playIcon, _doubleSpeedIcon;
    private DateManager _date;

    private void Awake() => _modeButton.onClick.AddListener(CycleMode);

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
        RefreshDate(_date.CurrentMonth, _date.CurrentDay);
        RefreshState();
    }

    private void CycleMode()
    {
        if (_date == null) return;
        switch (_date.SpeedMultiplier)
        {
            case 0: _date.Resume(); break;
            case 1: _date.ResumeDoubleSpeed(); break;
            default: _date.Pause(); break;
        }
    }

    private void RefreshDate(int month, int day) => _dateText.text = $"{month}月{day}日";

    private void RefreshState()
    {
        int speed = _date.SpeedMultiplier;
        _stopIcon.SetActive(speed == 0);
        _playIcon.SetActive(speed == 1);
        _doubleSpeedIcon.SetActive(speed == 2);
    }

    private void OnDisable()
    {
        if (_date == null) return;
        _date.OnDateChanged -= RefreshDate;
        _date.OnProgressionChanged -= RefreshState;
        _date.Pause();
        RefreshState();
    }

    private void OnDestroy() => _modeButton.onClick.RemoveListener(CycleMode);
}
