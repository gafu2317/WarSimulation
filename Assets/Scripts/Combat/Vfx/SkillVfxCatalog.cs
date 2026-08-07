using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillVfxCatalog", menuName = "Combat/VFX/Skill Vfx Catalog")]
public sealed class SkillVfxCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public SkillId SkillId = SkillId.None;
        public GameObject Prefab;
        public SkillVfxSpawnAnchor Anchor = SkillVfxSpawnAnchor.Target;
        [Min(0f)] public float LifetimeSeconds;
        public Vector3 WorldOffset;
    }

    [SerializeField] private List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;

    public bool TryGetEntry(SkillId skillId, out Entry entry)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry candidate = _entries[i];
            if (candidate == null || candidate.SkillId != skillId) continue;
            entry = candidate;
            return true;
        }

        entry = null;
        return false;
    }

#if UNITY_EDITOR
    public void EditorSetEntries(List<Entry> entries)
    {
        _entries = entries ?? new List<Entry>();
    }
#endif
}
