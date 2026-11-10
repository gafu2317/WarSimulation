using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class CountryExpeditionReportView : MonoBehaviour
{
    [SerializeField] private CountryWorldMapData _data;
    [SerializeField] private CountryRosterData _rosterData;
    [SerializeField] private RectTransform _content;
    [SerializeField] private CountryExpeditionRow _rowPrefab;
    [SerializeField] private CountryRosterConfirm _details;
    [SerializeField] private TextMeshProUGUI _empty;
    private readonly List<CountryExpeditionRow> _rows = new();
    public bool DetailsOpen => _details.IsOpen;
    public void CloseDetails() => _details.Close();
    private void OnEnable() => Refresh();
    public void Refresh()
    {
        if (PlayDataManager.Instance == null || DateManager.Instance == null) return;
        var state = PlayDataManager.Instance.GetOrCreateExpeditions(_data, _rosterData, CountryDate.From(DateManager.Instance));
        var trips = state.Returned.Where(trip => trip.ReportMonth == DateManager.Instance.ElapsedMonths).ToArray();
        foreach (var row in _rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } _rows.Clear();
        _empty.gameObject.SetActive(trips.Length == 0);
        foreach (var trip in trips)
        {
            var row = Instantiate(_rowPrefab, _content); _rows.Add(row);
            row.Bind(state, trip, true, () => _details.ShowDetails("遠征の帰還", CountryExpeditionRow.Details(state, trip, _rosterData, true)));
        }
    }
}
