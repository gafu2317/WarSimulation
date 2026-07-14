using UnityEngine;

public class CountryCycleSystem : MonoBehaviour
{
    private void Start()
    {
        if (DateManager.Instance != null)
        {
            DateManager.Instance.OnMonthChanged += HandleMonthChanged;
        }
    }

    private void OnDestroy()
    {
        if (DateManager.Instance != null)
        {
            DateManager.Instance.OnMonthChanged -= HandleMonthChanged;
        }
    }

    private void HandleMonthChanged(int newMonth)
    {
        // 毎月の初めに時間の進行を一時停止させる
        DateManager.Instance.PauseFastForward();

        // ひとまず仮に一定量を加算
        if (PlayDataManager.Instance != null)
        {
            PlayDataManager.Instance.SaveMoneyChange(100);
            PlayDataManager.Instance.SaveWoodChange(50);
            PlayDataManager.Instance.SaveOreChange(30);

            // 現在の日付をセーブ
            PlayDataManager.Instance.SaveCurrentDate(DateManager.Instance.CurrentMonth, DateManager.Instance.CurrentDay);
        }

        // ここで本来はUIなどを表示し、ユーザーのアクションを待つ。
        // ユーザーが決定アクション（ConfirmCountryOperation）を実行すると時間が再開する。
    }
}
