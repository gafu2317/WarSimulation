using System;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatMagicStoneSystem : MonoBehaviour
{
    [SerializeField, Min(1)] private int _mainStoneMaxHP = 500;
    [SerializeField, Min(1)] private int _subStoneMaxHP = 200;

    private readonly Dictionary<int, MagicStoneRuntimeState> _states = new Dictionary<int, MagicStoneRuntimeState>();
    private readonly Dictionary<int, MagicStone> _views = new Dictionary<int, MagicStone>();

    public event Action<int> StateChanged;
    public event Action<FeatureType> MainStoneDestroyed;

    public void Initialize(MapData map)
    {
        _states.Clear();
        _views.Clear();

        if (map == null) return;

        List<PlacedFeature> features = map.Features;
        for (int i = 0; i < features.Count; i++)
        {
            FeatureType type = features[i].Type;
            if (!IsMagicStone(type)) continue;

            _states[i] = new MagicStoneRuntimeState(
                i,
                type,
                GetMaxHPForType(type),
                GetMaxHPForType(type));
        }

        RebindViews();
    }

    public bool TryGetState(int featureIndex, out MagicStoneRuntimeState state)
    {
        return _states.TryGetValue(featureIndex, out state);
    }

    public bool TryGetHP(int featureIndex, out int hp)
    {
        if (_states.TryGetValue(featureIndex, out MagicStoneRuntimeState state))
        {
            hp = state.HP;
            return true;
        }

        hp = 0;
        return false;
    }

    public bool TryGetMaxHP(int featureIndex, out int maxHp)
    {
        if (_states.TryGetValue(featureIndex, out MagicStoneRuntimeState state))
        {
            maxHp = state.MaxHP;
            return true;
        }

        maxHp = 1;
        return false;
    }

    public int TakeDamage(int featureIndex, int amount)
    {
        if (amount <= 0 || !_states.TryGetValue(featureIndex, out MagicStoneRuntimeState state)) return 0;
        if (state.HP <= 0) return 0;

        int previousHP = state.HP;
        state.HP = Mathf.Max(0, state.HP - amount);
        int applied = previousHP - state.HP;

        StateChanged?.Invoke(featureIndex);

        if (state.HP == 0)
        {
            if (_views.TryGetValue(featureIndex, out MagicStone view))
            {
                view.OnDestroyed();
            }

            if (IsMainStone(state.Type))
            {
                MainStoneDestroyed?.Invoke(state.Type);
            }
        }

        return applied;
    }

    public bool IsDestroyed(FeatureType type)
    {
        foreach (KeyValuePair<int, MagicStoneRuntimeState> pair in _states)
        {
            if (pair.Value.Type != type) continue;
            return pair.Value.HP <= 0;
        }

        return false;
    }

    public void RegisterView(int featureIndex, MagicStone view)
    {
        if (view == null) return;
        _views[featureIndex] = view;
    }

    private void RebindViews()
    {
        MagicStone[] views = FindObjectsByType<MagicStone>();
        for (int i = 0; i < views.Length; i++)
        {
            MagicStone view = views[i];
            if (view == null) continue;

            RegisterView(view.FeatureIndex, view);
            view.OnRestored();
        }
    }

    private int GetMaxHPForType(FeatureType type)
    {
        return IsMainStone(type) ? _mainStoneMaxHP : _subStoneMaxHP;
    }

    private static bool IsMagicStone(FeatureType type)
    {
        return type == FeatureType.OwnMainStone ||
               type == FeatureType.OwnSubStone ||
               type == FeatureType.EnemyMainStone ||
               type == FeatureType.EnemySubStone;
    }

    private static bool IsMainStone(FeatureType type)
    {
        return type == FeatureType.OwnMainStone || type == FeatureType.EnemyMainStone;
    }
}

public sealed class MagicStoneRuntimeState
{
    public int FeatureIndex { get; }
    public FeatureType Type { get; }
    public int MaxHP { get; }
    public int HP { get; set; }

    public MagicStoneRuntimeState(int featureIndex, FeatureType type, int maxHp, int hp)
    {
        FeatureIndex = featureIndex;
        Type = type;
        MaxHP = maxHp;
        HP = hp;
    }
}
