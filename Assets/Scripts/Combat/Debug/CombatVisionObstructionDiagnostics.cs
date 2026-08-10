using System;
using System.Collections.Generic;
using UnityEngine;

public static class CombatVisionObstructionDiagnostics
{
    private sealed class Entry
    {
        public string Key;
        public string OwnerName;
        public string TargetName;
        public string ColliderName;
        public string ColliderType;
        public string LayerName;
        public int Count;
        public float FirstHitDistance;
        public Vector3 FirstOrigin;
        public Vector3 FirstHitPoint;
        public Bounds ColliderBounds;
    }

    private static readonly Dictionary<string, Entry> Entries = new();

    public static void BeginBattle()
    {
        Entries.Clear();
    }

    public static void Clear()
    {
        Entries.Clear();
    }

    public static void Record(Character owner, Transform target, RaycastHit hit, Vector3 origin)
    {
        if (!CombatPlaytestDebugSettings.UseThickSightCast ||
            !CombatPlaytestDebugSettings.LogVisionObstructions ||
            owner == null ||
            hit.collider == null)
        {
            return;
        }

        Collider collider = hit.collider;
        string key = owner.GetInstanceID() + ":" +
            (target != null ? target.GetInstanceID() : 0) + ":" +
            collider.GetInstanceID();
        if (!Entries.TryGetValue(key, out Entry entry))
        {
            entry = new Entry
            {
                Key = key,
                OwnerName = owner.name,
                TargetName = target != null ? target.name : "<none>",
                ColliderName = collider.name,
                ColliderType = collider.GetType().Name,
                LayerName = LayerMask.LayerToName(collider.gameObject.layer),
                FirstHitDistance = hit.distance,
                FirstOrigin = origin,
                FirstHitPoint = hit.point,
                ColliderBounds = collider.bounds,
            };
            Entries.Add(key, entry);
        }

        entry.Count++;
    }

    public static void WriteTo(Action<string> writeLine)
    {
        if (writeLine == null ||
            !CombatPlaytestDebugSettings.UseThickSightCast ||
            !CombatPlaytestDebugSettings.LogVisionObstructions)
        {
            return;
        }

        if (Entries.Count == 0)
        {
            writeLine("[VisionDiag] no obstruction recorded");
            return;
        }

        var entries = new List<Entry>(Entries.Values);
        entries.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            writeLine(
                "[VisionDiag] owner=" + entry.OwnerName +
                " target=" + entry.TargetName +
                " blocker=" + entry.ColliderName +
                " collider=" + entry.ColliderType +
                " layer=" + entry.LayerName +
                " hits=" + entry.Count +
                " firstDistance=" + entry.FirstHitDistance.ToString("F3") +
                " origin=" + entry.FirstOrigin +
                " boundsMin=" + entry.ColliderBounds.min +
                " boundsMax=" + entry.ColliderBounds.max +
                " firstPoint=" + entry.FirstHitPoint);
        }

        Entries.Clear();
    }
}
