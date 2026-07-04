using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatMagicStoneStatusView : MonoBehaviour
{
    [SerializeField] private FeatureType _featureType = FeatureType.OwnMainStone;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image _hpFillImage;
    [SerializeField] private string _label = "HP";

    private CombatMagicStoneSystem _magicStoneSystem;

    private void Awake()
    {
        ResolveReferences();
        TryResolveSystem();
    }

    private void OnEnable()
    {
        TryResolveSystem();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Refresh()
    {
        ResolveReferences();
        TryResolveSystem();

        int hp = 0;
        int maxHp = 1;
        if (_magicStoneSystem != null &&
            _magicStoneSystem.TryGetState(_featureType, out MagicStoneRuntimeState state) &&
            state != null)
        {
            hp = state.HP;
            maxHp = Mathf.Max(1, state.MaxHP);
        }

        if (_hpText != null)
        {
            _hpText.text = $"{_label} {hp}/{maxHp}";
        }

        if (_hpFillImage != null)
        {
            _hpFillImage.fillAmount = Mathf.Clamp01(hp / (float)maxHp);
        }
    }

    private void ResolveReferences()
    {
        _hpText ??= GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        _hpFillImage ??= GetComponentInChildren<Image>(includeInactive: true);
    }

    private void TryResolveSystem()
    {
        if (_magicStoneSystem != null) return;
        _magicStoneSystem = CombatMagicStoneSystemResolver.Resolve();
    }

    private void Subscribe()
    {
        if (_magicStoneSystem == null) return;
        _magicStoneSystem.StateChanged -= OnStoneStateChanged;
        _magicStoneSystem.StateChanged += OnStoneStateChanged;
    }

    private void Unsubscribe()
    {
        if (_magicStoneSystem == null) return;
        _magicStoneSystem.StateChanged -= OnStoneStateChanged;
    }

    private void OnStoneStateChanged(int featureIndex)
    {
        if (_magicStoneSystem == null ||
            !_magicStoneSystem.TryGetState(featureIndex, out MagicStoneRuntimeState state) ||
            state == null ||
            state.Type != _featureType)
        {
            return;
        }

        Refresh();
    }
}
