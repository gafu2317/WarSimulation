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
    private void OnEnable()
    {
        var title = transform.Find("ReturnTitle")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = "今月の報告";
        _empty.text = "今月の報告はない";
        Refresh();
    }
    public void Refresh()
    {
        if (PlayDataManager.Instance == null || DateManager.Instance == null) return;
        var state = PlayDataManager.Instance.GetOrCreateExpeditions(_data, _rosterData, CountryDate.From(DateManager.Instance));
        var trips = state.Returned.Where(trip => trip.ReportMonth == DateManager.Instance.ElapsedMonths).ToArray();
        foreach (var row in _rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } _rows.Clear();
        var reports = PlayDataManager.Instance.GovernmentReport;
        _empty.gameObject.SetActive(trips.Length == 0 && reports.Count == 0);
        foreach (string report in reports)
        {
            var row = Instantiate(_rowPrefab, _content); _rows.Add(row);
            int split = report.IndexOf('　');
            string title = split < 0 ? "施設の報告" : report[..split];
            string summary = split < 0 ? report : report[(split + 1)..];
            row.BindGovernment(title, summary, () => _details.ShowDetails(title, summary));
        }
        foreach (var trip in trips)
        {
            var row = Instantiate(_rowPrefab, _content); _rows.Add(row);
            row.Bind(state, trip, true, () => _details.ShowDetails("遠征の帰還", CountryExpeditionRow.Details(state, trip, PlayDataManager.Instance.Roster, true)));
        }
    }
}
