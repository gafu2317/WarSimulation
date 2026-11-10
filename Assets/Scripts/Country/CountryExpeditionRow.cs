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
        _status.text = returned ? "領地獲得　›" : trip.Returning ? "帰路　›" : "往路　›";
        _status.color = CountryGovernmentTheme.Ink;
        _selected = selected;
    }
    public static string Details(CountryExpeditionState state, CountryExpeditionTrip trip, CountryRosterData roster, bool returned)
        => $"{state.Data.Regions[trip.Target].Name}　{(returned ? "領地獲得・帰還" : trip.Returning ? "領地獲得・帰路" : "往路")}\n" +
           $"出発 {trip.Departure}　到着 {trip.Arrival}\n帰還 {trip.Return}　片道 {trip.Days}日\n\n" +
           string.Join("\n", trip.Members.Select((id, i) => $"{roster.Soldiers[id].Name}　{roster.Weapons[trip.Weapons[i]].Name}"));
}
