using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlaytestSceneNavigation
{
    public const string CountryScene = "Assets/Scenes/Country.unity";
    public const string CombatScene = "Assets/Scenes/GafuTest.unity";
    public static bool IsLoading { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => IsLoading = false;

    public static void OpenCombat() => Open(CombatScene);
    public static void OpenCountry() => Open(CountryScene);
    internal static bool TryOpenCountryBattle() => Open(CombatScene);
    internal static bool TryReturnFromCountryBattle() => Open(CountryScene);

    private static bool Open(string path)
    {
        if (IsLoading || SceneManager.GetActiveScene().path == path) return false;
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("Scene is missing from the build: " + path);
            return false;
        }

        AsyncOperation loading;
        try
        {
            loading = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }
        catch (ArgumentException ex)
        {
            Debug.LogError($"Could not load scene '{path}': {ex.Message}");
            return false;
        }
        if (loading == null)
        {
            Debug.LogError("Scene load did not start: " + path);
            return false;
        }

        IsLoading = true;
        loading.completed += _ => IsLoading = false;
        DateManager.Instance?.Pause();
        CombatAudioPlayer.Existing?.StopBattle();
        Time.timeScale = 1;
        return true;
    }
}
