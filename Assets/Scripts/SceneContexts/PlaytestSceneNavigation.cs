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

    private static void Open(string path)
    {
        if (IsLoading || SceneManager.GetActiveScene().path == path) return;
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("Playtest scene is missing from the build: " + path);
            return;
        }
        IsLoading = true;
        DateManager.Instance?.Pause();
        CombatAudioPlayer.Existing?.StopBattle();
        Time.timeScale = 1;
        try
        {
            var loading = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            loading.completed += _ => IsLoading = false;
        }
        catch (Exception)
        {
            IsLoading = false;
            throw;
        }
    }
}
