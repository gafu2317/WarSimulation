using TMPro;
using UnityEngine;

public sealed class CountryFacilityBanner : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private UnityEngine.UI.Image _accent;
    [SerializeField] private RectTransform _connector;
    [SerializeField] private CountryFacilitySymbol _symbol;
    private CountryFacilityPopup _popup;
    public CountryFacilityTarget Target { get; private set; }
    public RectTransform Rect { get; private set; }

    private void Awake()
    {
        Rect = (RectTransform)transform;
        _button.onClick.AddListener(Open);
    }

    public void Initialize(CountryFacilityTarget target, CountryFacilityPopup popup)
    {
        Target = target;
        _popup = popup;
        _label.text = target.name;
        _symbol.SetFacility(target.name);
        _accent.color = target.accent;
        Rect.sizeDelta = new Vector2(Mathf.Max(150, _label.GetPreferredValues(target.name).x + 128), 112);
    }

    public void Position(Vector2 point, float lift)
    {
        Rect.anchoredPosition = point;
        _connector.sizeDelta = new Vector2(2, lift);
        _connector.gameObject.SetActive(lift > 1);
    }

    private void Open() => _popup.Open(Target);
}
