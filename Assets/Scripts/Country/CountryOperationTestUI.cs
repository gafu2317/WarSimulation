using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class CountryOperationTestUI : MonoBehaviour
{
    private Dictionary<FacilityType, int> _plannedUpgrades = new Dictionary<FacilityType, int>();
    private int _allocatedFunding = 0; // 運営費の仮割り振り
    private float _uiScale = 2.0f;
    private Vector2 _scrollPosition;

    private void Start()
    {
        // 初期化
        foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
        {
            _plannedUpgrades[type] = 0;
        }
    }

    private void OnGUI()
    {
        // スケール調整用スライダー（スケール適用前）
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 40));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"UI Scale: {_uiScale:F1}", GUILayout.Width(100));
        _uiScale = GUILayout.HorizontalSlider(_uiScale, 1.0f, 5.0f, GUILayout.Width(200));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        // 画面全体のUIスケールを適用
        GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1.0f));

        // スケール後の領域計算
        float scaledWidth = (Screen.width - 20) / _uiScale;
        float scaledHeight = (Screen.height - 60) / _uiScale;

        GUILayout.BeginArea(new Rect(10 / _uiScale, 50 / _uiScale, scaledWidth, scaledHeight));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        GUILayout.Label("=== 国家状態 ===");
        if (DateManager.Instance != null)
        {
            GUILayout.Label($"日付: {DateManager.Instance.CurrentMonth}月 {DateManager.Instance.CurrentDay}日");
        }
        
        if (PlayDataManager.Instance != null)
        {
            GUILayout.Label($"資金: {PlayDataManager.Instance.Money}");
            GUILayout.Label($"木材: {PlayDataManager.Instance.Wood}");
            GUILayout.Label($"鉱石: {PlayDataManager.Instance.Ore}");
            GUILayout.Label($"領土: {PlayDataManager.Instance.Territory}");
            
            GUILayout.Space(10);
            GUILayout.Label("--- 施設レベル ---");
            foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
            {
                int currentLevel = 0;
                if (PlayDataManager.Instance.FacilityLevels.TryGetValue(type, out int level))
                {
                    currentLevel = level;
                }
                
                int queueCount = 0;
                if (CountrySceneContext.Instance != null && CountrySceneContext.Instance.CountryOperationSystem != null)
                {
                    queueCount = CountrySceneContext.Instance.CountryOperationSystem.GetQueueCount(type);
                }

                GUILayout.Label($"{type}: Lv {currentLevel} (強化待ち: {queueCount})");
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("=== 国家運営アクション (計画) ===");

        GUILayout.BeginHorizontal();
        GUILayout.Label($"運営費割り振り (資金): {_allocatedFunding}");
        if (GUILayout.Button("+10", GUILayout.Width(50))) _allocatedFunding += 10;
        if (GUILayout.Button("-10", GUILayout.Width(50))) _allocatedFunding = Mathf.Max(0, _allocatedFunding - 10);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("--- 施設強化計画 ---");

        int totalMoneyCost = _allocatedFunding;
        int totalWoodCost = 0;
        int totalOreCost = 0;

        foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{type} 追加強化数: {_plannedUpgrades[type]}");
            if (GUILayout.Button("+", GUILayout.Width(30))) _plannedUpgrades[type]++;
            if (GUILayout.Button("-", GUILayout.Width(30))) _plannedUpgrades[type] = Mathf.Max(0, _plannedUpgrades[type] - 1);
            GUILayout.EndHorizontal();

            // コスト計算
            int currentLevel = 0;
            if (PlayDataManager.Instance != null && PlayDataManager.Instance.FacilityLevels.TryGetValue(type, out int level))
            {
                currentLevel = level;
            }
            if (CountrySceneContext.Instance != null && CountrySceneContext.Instance.CountryOperationSystem != null)
            {
                currentLevel += CountrySceneContext.Instance.CountryOperationSystem.GetQueueCount(type);
            }

            for (int i = 0; i < _plannedUpgrades[type]; i++)
            {
                int targetLevel = currentLevel + i + 1;
                var cost = FacilityUpgradeData.GetCost(type, targetLevel);
                totalMoneyCost += cost.MoneyCost;
                totalWoodCost += cost.WoodCost;
                totalOreCost += cost.OreCost;
            }
        }

        GUILayout.Space(10);
        GUILayout.Label($"予想合計コスト -> 資金: {totalMoneyCost}, 木材: {totalWoodCost}, 鉱石: {totalOreCost}");

        bool hasEnough = false;
        if (PlayDataManager.Instance != null)
        {
            hasEnough = PlayDataManager.Instance.HasEnoughResources(totalMoneyCost, totalWoodCost, totalOreCost);
            if (!hasEnough)
            {
                GUI.color = Color.red;
                GUILayout.Label("※リソースが不足しています");
                GUI.color = Color.white;
            }
        }

        // 月初め（一時停止中）のみ決定ボタンを押せる想定。
        // DateManagerの進行状態を取るプロパティが無いので、リソース不足かどうかだけでボタンの有効化を制御します。
        GUI.enabled = hasEnough;

        if (GUILayout.Button("アクションを決定して時間を再開する", GUILayout.Height(40)))
        {
            if (CountrySceneContext.Instance != null && CountrySceneContext.Instance.CountryOperationSystem != null)
            {
                List<CountryOperationSystem.UpgradeTask> tasks = new List<CountryOperationSystem.UpgradeTask>();

                foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
                {
                    int currentLevel = 0;
                    if (PlayDataManager.Instance.FacilityLevels.TryGetValue(type, out int level))
                    {
                        currentLevel = level;
                    }
                    currentLevel += CountrySceneContext.Instance.CountryOperationSystem.GetQueueCount(type);

                    for (int i = 0; i < _plannedUpgrades[type]; i++)
                    {
                        int targetLevel = currentLevel + i + 1;
                        var cost = FacilityUpgradeData.GetCost(type, targetLevel);
                        
                        tasks.Add(new CountryOperationSystem.UpgradeTask
                        {
                            Type = type,
                            TargetLevel = targetLevel,
                            RemainingDays = cost.RequiredDays
                        });
                    }
                }

                // Confirm実行
                CountrySceneContext.Instance.CountryOperationSystem.ConfirmCountryOperation(totalMoneyCost, totalWoodCost, totalOreCost, tasks);

                // 計画リセット
                _allocatedFunding = 0;
                foreach (FacilityType type in System.Enum.GetValues(typeof(FacilityType)))
                {
                    _plannedUpgrades[type] = 0;
                }
            }
        }
        GUI.enabled = true; // 元に戻す

        GUILayout.Space(20);
        if (GUILayout.Button("時間を早送りする (手動用)", GUILayout.Height(30)))
        {
             DateManager.Instance?.StartFastForwardAsync().Forget();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
