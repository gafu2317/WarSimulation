using TMPro;
using UnityEngine;

public sealed class CountryTimeProgressEffect : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _month, _day, _turnDay;
    [SerializeField] private RectTransform _turnPage;
    private DateManager _date;
    private float _turn;

    private void OnEnable()
    {
        _date = DateManager.Instance;
        _turnPage.gameObject.SetActive(false);
        if (_date == null) return;
        _month.text = $"{_date.CurrentMonth}月";
        _day.text = _date.CurrentDay.ToString();
        _date.OnDateChanged += TurnPage;
    }

    private void TurnPage(int month, int day)
    {
        _turnDay.text = _day.text;
        _month.text = $"{month}月";
        _day.text = day.ToString();
        _turn = 0;
        _turnPage.localScale = Vector3.one;
        _turnPage.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!_turnPage.gameObject.activeSelf) return;
        _turn = Mathf.MoveTowards(_turn, 1, Time.unscaledDeltaTime / .14f);
        _turnPage.localScale = new Vector3(1, 1 - _turn, 1);
        if (_turn >= 1) _turnPage.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_date != null) _date.OnDateChanged -= TurnPage;
        _date = null;
        _turnPage.gameObject.SetActive(false);
        _turnPage.localScale = Vector3.one;
    }
}
