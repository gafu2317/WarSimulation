using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class CountryOperationSystem : MonoBehaviour
{
    // 強化キュー（施設ごとのタスク一覧）
    public class UpgradeTask
    {
        public FacilityType Type;
        public int TargetLevel;
        public int RemainingDays;
    }

    private Dictionary<FacilityType, Queue<UpgradeTask>> _upgradeQueues = new Dictionary<FacilityType, Queue<UpgradeTask>>();

    private void Start()
    {
        if (DateManager.Instance != null)
        {
            DateManager.Instance.OnDateChanged += HandleDateChanged;
        }
    }

    private void OnDestroy()
    {
        if (DateManager.Instance != null)
        {
            DateManager.Instance.OnDateChanged -= HandleDateChanged;
        }
    }

    /// <summary>
    /// 日付が変わった時の処理。強化日数を進める。
    /// </summary>
    private void HandleDateChanged(int month, int day)
    {
        foreach (var kvp in _upgradeQueues)
        {
            FacilityType type = kvp.Key;
            Queue<UpgradeTask> queue = kvp.Value;

            if (queue.Count > 0)
            {
                var currentTask = queue.Peek();
                currentTask.RemainingDays--;

                if (currentTask.RemainingDays <= 0)
                {
                    // 完了
                    queue.Dequeue();
                    PlayDataManager.Instance.SaveFacilityLevelChange(type, 1);
                    PlayDataManager.Instance.SaveCurrentDate(DateManager.Instance.CurrentMonth, DateManager.Instance.CurrentDay);
                    Debug.Log($"{type} の強化が完了しました。レベル: {currentTask.TargetLevel}");
                }
            }
        }
    }

    /// <summary>
    /// 国家運営アクションを決定する
    /// </summary>
    public void ConfirmCountryOperation(int totalMoneyCost, int totalWoodCost, int totalOreCost, List<UpgradeTask> newUpgrades)
    {
        if (!PlayDataManager.Instance.HasEnoughResources(totalMoneyCost, totalWoodCost, totalOreCost))
        {
            Debug.LogWarning("リソースが足りません！");
            return;
        }

        // リソース消費
        PlayDataManager.Instance.SaveMoneyChange(-totalMoneyCost);
        PlayDataManager.Instance.SaveWoodChange(-totalWoodCost);
        PlayDataManager.Instance.SaveOreChange(-totalOreCost);

        // キューに追加
        foreach (var task in newUpgrades)
        {
            if (!_upgradeQueues.ContainsKey(task.Type))
            {
                _upgradeQueues[task.Type] = new Queue<UpgradeTask>();
            }
            _upgradeQueues[task.Type].Enqueue(task);
        }

        // セーブ
        PlayDataManager.Instance.SaveCountryOperation();

        // 時間再開
        DateManager.Instance.StartFastForwardAsync().Forget();
    }

    public int GetQueueCount(FacilityType type)
    {
        if (_upgradeQueues.TryGetValue(type, out var queue))
        {
            return queue.Count;
        }
        return 0;
    }
}
