using System;
using System.IO;
using System.Linq;
using UnityEngine;

internal sealed class CountryCheckpointStore
{
    private const string DefaultKey = "WarSimulation.Country";
    internal static string TestKey { get; set; }
    private readonly string _key = TestKey ?? DefaultKey;
    private int _lastSlot = 1;
    private long _revision;

    internal enum LoadResult { New, Loaded, Recovered, Unreadable }

    internal LoadResult Load(Action<CountryCheckpoint> restore)
    {
        var candidates = new CountryCheckpoint[2];
        int existing = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            string key = Key(i);
            if (!PlayerPrefs.HasKey(key)) continue;
            existing++;
            try
            {
                candidates[i] = JsonUtility.FromJson<CountryCheckpoint>(PlayerPrefs.GetString(key));
            }
            catch (ArgumentException ex) { Debug.LogWarning($"Could not read country checkpoint slot {i}: {ex.Message}"); }
        }
        if (existing == 0) return LoadResult.New;
        if (candidates.Any(save => save != null && save.Version > CountryCheckpoint.CurrentVersion))
            return LoadResult.Unreadable;

        bool recovered = existing > candidates.Count(save => save != null);
        foreach (int i in Enumerable.Range(0, candidates.Length).OrderByDescending(i => candidates[i]?.Revision ?? long.MinValue))
        {
            var save = candidates[i];
            if (save == null) continue;
            try
            {
                CountryCheckpoint.Require(save.Version is >= CountryCheckpoint.LegacyCPVersion and <= CountryCheckpoint.CurrentVersion &&
                    save.Revision > 0 && save.Revision < long.MaxValue, "version or revision");
                restore(save);
                _lastSlot = i;
                _revision = save.Revision;
                return recovered ? LoadResult.Recovered : LoadResult.Loaded;
            }
            catch (Exception ex) when (ex is InvalidDataException or OverflowException)
            {
                recovered = true;
                Debug.LogWarning($"Could not restore country checkpoint slot {i}: {ex.Message}");
            }
        }
        return LoadResult.Unreadable;
    }

    internal bool Write(CountryCheckpoint save)
    {
        int slot = 1 - _lastSlot;
        save.Version = CountryCheckpoint.CurrentVersion;
        save.Revision = _revision + 1;
        try
        {
            PlayerPrefs.SetString(Key(slot), JsonUtility.ToJson(save));
            PlayerPrefs.Save();
            _lastSlot = slot;
            _revision = save.Revision;
            return true;
        }
        catch (PlayerPrefsException ex)
        {
            Debug.LogWarning(ex.Message);
            return false;
        }
    }

    private string Key(int slot) => $"{_key}.{slot}";
}
