using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatAiWorldLabelDebugView : CombatDebugBehaviour
{
    public override string InspectorDescription => "AIキャラクターの頭上に、現在の目的・武器・性格・使用スキルを表示します。";

    [SerializeField, Min(0.1f)] private float _refreshIntervalSeconds = 1f;

    private readonly List<Entry> _entries = new();
    private float _nextRefreshTime;

    private void OnEnable()
    {
        if (!CombatPlaytestDebugSettings.ShowAiLabels)
        {
            enabled = false;
            return;
        }

        RefreshEntries();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshEntries();
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry.Brain == null || entry.Character == null || entry.Label == null) continue;

            bool isActive = entry.Brain.isActiveAndEnabled &&
                entry.Brain.IsAiEnabled &&
                entry.Character.Health != null &&
                entry.Character.Health.IsAlive;
            entry.Label.SetVisible(isActive);

            if (CombatPlaytestDebugSettings.LabelShowObjective)
            {
                entry.Label.SetObjectiveVisible(true);
                entry.Label.SetObjective(entry.Brain.LastPlan.Objective, isActive);
            }
            else
            {
                entry.Label.SetObjectiveVisible(false);
            }

            if (CombatPlaytestDebugSettings.LabelShowWeapon)
            {
                entry.Label.SetWeapon(entry.Character.EquippedWeapon);
            }
            else
            {
                entry.Label.SetWeaponVisible(false);
            }

            if (CombatPlaytestDebugSettings.LabelShowPersonality)
            {
                CombatAiPersonalityProfile personality = entry.Character.PersonalityProfile;
                entry.Label.SetPersonality(personality);
            }
            else
            {
                entry.Label.SetPersonalityVisible(false);
            }
        }
    }

    public void ApplyPlaytestSettings()
    {
        // LabelShow* は Update が毎フレーム反映する。ここでの再構築は不要。
    }

    private void OnDisable()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Label != null)
            {
                _entries[i].Label.SetVisible(false);
            }
        }
    }

    private void RefreshEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Label != null)
            {
                _entries[i].Label.SetVisible(false);
            }
        }

        _entries.Clear();
        CombatAiBrain[] brains = FindObjectsByType<CombatAiBrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < brains.Length; i++)
        {
            CombatAiBrain brain = brains[i];
            if (brain == null) continue;

            Character character = brain.GetComponent<Character>();
            if (character == null) continue;

            CombatAiWorldLabel label = brain.GetComponent<CombatAiWorldLabel>();
            label ??= brain.gameObject.AddComponent<CombatAiWorldLabel>();
            label.SetVisible(true);
            _entries.Add(new Entry(brain, character, label));
        }

        _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
    }

    private readonly struct Entry
    {
        public CombatAiBrain Brain { get; }
        public Character Character { get; }
        public CombatAiWorldLabel Label { get; }

        public Entry(CombatAiBrain brain, Character character, CombatAiWorldLabel label)
        {
            Brain = brain;
            Character = character;
            Label = label;
        }
    }
}
