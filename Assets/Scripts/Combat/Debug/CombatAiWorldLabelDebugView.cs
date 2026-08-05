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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RefreshEntries();
#else
        enabled = false;
#endif
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
            CombatAiPersonalityProfile personality = entry.Character.PersonalityProfile;
            entry.Label.SetObjective(entry.Brain.LastPlan.Objective, isActive);
            entry.Label.SetWeapon(entry.Character.EquippedWeapon);
            entry.Label.SetPersonality(personality, CombatAiPersonalityHighlight.Matches(personality));
        }
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
        CombatAiBrain[] brains = FindObjectsByType<CombatAiBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
