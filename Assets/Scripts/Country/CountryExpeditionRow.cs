using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CountryExpeditionRow : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _name, _detail, _status;
    private Action _selected;
    private void Awake() => _button.onClick.AddListener(() => _selected?.Invoke());
    public void Bind(CountryExpeditionState state, CountryExpeditionTrip trip, bool returned, Action selected)
    {
        _name.text = state.Data.Regions[trip.Target].Name;
        _detail.text = $"帰還 {trip.Return}　{trip.Members.Count}人";
        _status.text = returned ? $"{Outcome(trip)}　›" : trip.Returning ? "帰路　›" : state.Today.CompareTo(trip.Arrival) >= 0 ? "交戦待ち　›" : "往路　›";
        _status.color = CountryGovernmentTheme.Ink;
        _selected = selected;
    }
    public void BindGovernment(string title, string summary, Action selected)
    {
        _name.text = title;
        _detail.text = summary;
        _status.text = "詳細　›";
        _selected = selected;
    }

    private static string Outcome(CountryExpeditionTrip trip) => trip.Won == true ? "領地獲得" : "敗北";
    public static string Details(CountryExpeditionState state, CountryExpeditionTrip trip, CountryRoster roster, bool returned)
        => $"{state.Data.Regions[trip.Target].Name}　{(returned ? Outcome(trip) + "・帰還" : trip.Returning ? Outcome(trip) + "・帰路" : state.Today.CompareTo(trip.Arrival) >= 0 ? "交戦待ち" : "往路")}\n" +
           $"出発 {trip.Departure}　到着 {trip.Arrival}\n帰還 {trip.Return}　片道 {trip.Days}日\n\n" +
           string.Join("\n", trip.Members.Select((id, i) => $"{roster.Data.Soldiers[id].Name}　{roster.Weapon(trip.Weapons[i]).Name}"));
}
