using System;
using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class MagicStone : MonoBehaviour
{
    [SerializeField] private int _featureIndex = -1;
    [SerializeField] private FeatureType _featureType;
    [SerializeField] private bool _isMainStone;

    private CombatWorldHealthBar _healthBar;
    private HealthSource _healthSource;

    public int FeatureIndex => _featureIndex;
    public FeatureType FeatureType => _featureType;

    public void Setup(int featureIndex, FeatureType featureType, bool isMainStone, float stoneHeight)
    {
        _featureIndex = featureIndex;
        _featureType = featureType;
        _isMainStone = isMainStone;

        float barWidth = isMainStone ? 1.6f : 1.1f;
        float yOffset = stoneHeight + (isMainStone ? 0.8f : 0.5f);
        EnsureHealthBar(new Vector3(0f, yOffset, 0f), barWidth);
    }

    private void Start()
    {
        if (_featureIndex < 0) return;

        CombatMagicStoneSystem system = ResolveSystem();
        system?.RegisterView(_featureIndex, this);

        if (_healthBar == null)
        {
            float barWidth = _isMainStone ? 1.6f : 1.1f;
            float yOffset = _isMainStone ? 4f : 2.3f;
            EnsureHealthBar(new Vector3(0f, yOffset, 0f), barWidth);
        }

        if (_healthSource == null && system != null)
        {
            _healthSource = new HealthSource(system, _featureIndex);
            _healthBar.Configure(_healthSource);
        }
    }

    private void OnDestroy()
    {
        _healthSource?.Dispose();
    }

    public void OnDestroyed()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        _healthBar?.SetVisible(false);
    }

    private void EnsureHealthBar(Vector3 offset, float barWidth)
    {
        _healthBar = GetComponent<CombatWorldHealthBar>();
        if (_healthBar == null)
        {
            _healthBar = gameObject.AddComponent<CombatWorldHealthBar>();
        }

        if (_healthSource != null)
        {
            _healthBar.Configure(_healthSource, offset, barWidth);
        }
    }

    private static CombatMagicStoneSystem ResolveSystem()
    {
        return CombatMagicStoneSystemResolver.Resolve();
    }

    private sealed class HealthSource : ICombatHealthSource
    {
        private readonly CombatMagicStoneSystem _system;
        private readonly int _featureIndex;

        public HealthSource(CombatMagicStoneSystem system, int featureIndex)
        {
            _system = system;
            _featureIndex = featureIndex;
            _system.StateChanged += OnStateChanged;
        }

        public int HP => _system.TryGetHP(_featureIndex, out int hp) ? hp : 0;
        public int MaxHP => _system.TryGetMaxHP(_featureIndex, out int maxHp) ? maxHp : 1;
        public bool IsAlive => HP > 0;

        public event Action HealthChanged;

        public void Dispose()
        {
            _system.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(int featureIndex)
        {
            if (featureIndex != _featureIndex) return;
            HealthChanged?.Invoke();
        }
    }
}
