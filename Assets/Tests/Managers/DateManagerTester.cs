using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

/// <summary>
/// DateManagerの動作確認用テストスクリプト
/// Unity 6 (New Input System) 対応
/// </summary>
public class TestDateManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField, Tooltip("日付を表示するTextMeshPro")]
    private TextMeshProUGUI dateText;

    private void Start()
    {
        // Managerが存在しない場合はエラーを回避
        if (DateManager.Instance == null)
        {
            Debug.LogError("DateManagerのインスタンスが見つかりません。シーンに存在するか確認してください。");
            return;
        }

        // 日付変更イベントにUI更新メソッドを登録
        DateManager.Instance.OnDateChanged += UpdateDateUI;

        // 初回起動時のUI表示を更新
        UpdateDateUI(DateManager.Instance.CurrentMonth, DateManager.Instance.CurrentDay);
        
        Debug.Log("テスト開始: [S]キーで早送り開始 / [P]キーで一時停止");
    }

    private void Update()
    {
        // New Input Systemのキーボードデバイスを取得
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // [S]キーで早送りを開始
        // 非同期メソッドを同期メソッド内から呼ぶため、.Forget()で警告を抑制して投げ放しにします
        if (keyboard.sKey.wasPressedThisFrame)
        {
            Debug.Log("早送り開始");
            DateManager.Instance.StartFastForwardAsync().Forget();
        }

        // [P]キーで一時停止
        if (keyboard.pKey.wasPressedThisFrame)
        {
            Debug.Log("早送り一時停止");
            DateManager.Instance.PauseFastForward();
        }
    }

    /// <summary>
    /// TMPのテキストを更新する
    /// </summary>
    private void UpdateDateUI(int month, int day)
    {
        if (dateText != null)
        {
            // "01月 05日" のように2桁ゼロ埋めで表示
            dateText.text = $"{month:D2}月 {day:D2}日";
        }
    }

    private void OnDestroy()
    {
        // イベントの多重登録やメモリリークを防ぐため、破棄時に登録を解除
        if (DateManager.Instance != null)
        {
            DateManager.Instance.OnDateChanged -= UpdateDateUI;
        }
    }
}