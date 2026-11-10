using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CombatBrowserAudioImport : MonoBehaviour
{
    [Serializable]
    private sealed class ImportedAudio
    {
        public int id;
        public string name;
        public int channels;
        public int frequency;
        public int frames;
        public float x;
        public float y;
    }

    [Serializable]
    private sealed class Status { public string message; }

    public Action<AudioClip> Imported;
    public Action<string> StatusChanged;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void CombatAudioFiles_Enable(string receiver);
    [DllImport("__Internal")] private static extern void CombatAudioFiles_Disable();
    [DllImport("__Internal")] private static extern void CombatAudioFiles_Select();
    [DllImport("__Internal")] private static extern int CombatAudioFiles_Copy(int id, [Out] float[] samples, int count);

    private void OnEnable() => CombatAudioFiles_Enable(gameObject.name);
    private void OnDisable() => CombatAudioFiles_Disable();
#endif

    public void SelectFiles()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CombatAudioFiles_Select();
#else
        StatusChanged?.Invoke("ブラウザ版ではPCの音声ファイルを追加できる。EditorではAssetsに音声を追加する");
#endif
    }

    public void OnBrowserAudioStatus(string json) => StatusChanged?.Invoke(JsonUtility.FromJson<Status>(json).message);
    public void OnBrowserAudioError(string json) => OnBrowserAudioStatus(json);

    public void OnBrowserAudio(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var data = JsonUtility.FromJson<ImportedAudio>(json);
        AudioClip clip = null;
        try
        {
            int count = checked(data.frames * data.channels);
            if (data.frames <= 0 || data.channels <= 0 || data.frequency <= 0 || count > 32 * 1024 * 1024)
                throw new InvalidOperationException("読み込めない音声です");
            var samples = new float[count];
            if (CombatAudioFiles_Copy(data.id, samples, count) == 0)
                throw new InvalidOperationException("音声データを受け取れませんでした");
            clip = AudioClip.Create(data.name, data.frames, data.channels, data.frequency, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("音声を作成できませんでした");
            Imported?.Invoke(clip);
            bool assigned = AssignAt(clip, new Vector2(data.x * Screen.width, data.y * Screen.height));
            StatusChanged?.Invoke(data.name + (assigned ? " を配置した" : " を一覧に追加した。各欄へドラッグして配置できる"));
        }
        catch (Exception error)
        {
            if (clip != null) Destroy(clip);
            StatusChanged?.Invoke(error.Message);
        }
#endif
    }

    public bool AssignAt(AudioClip clip, Vector2 position)
    {
        if (clip == null || EventSystem.current == null || position.x < 0 || position.y < 0) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        foreach (var hit in hits)
        {
            var slot = hit.gameObject.GetComponentInParent<CombatAudioDrag>();
            if (slot == null || slot.Assign == null) continue;
            slot.Assign(clip);
            return true;
        }
        return false;
    }
}
