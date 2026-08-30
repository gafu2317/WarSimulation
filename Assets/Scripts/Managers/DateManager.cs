using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class DateManager : ManagerBase<DateManager>
{
    [Header("Date Settings")]
    [SerializeField, Tooltip("初期の月")]
    private int _currentMonth = 1;
    
    [SerializeField, Tooltip("初期の日")]
    private int _currentDay = 1;

    [Header("Time Settings")]
    [SerializeField, Tooltip("1日進めるのにかかる現実の秒数（定数パラメータ）")]
    private float _secondsPerDay = 1.0f;

    // 外部から月日を取得するためのプロパティ
    public int CurrentMonth => _currentMonth;
    public int CurrentDay => _currentDay;

    // イベント通知
    public Action<int, int> OnDateChanged;  // 日付が変更されたとき（引数: 月, 日）
    public Action<int> OnMonthChanged;      // 月が変更されたとき（引数: 新しい月）

    // 各月の日数（インデックス0はダミー、1〜12月を使用。閏年は考慮しない）
    private readonly int[] _daysInMonth = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    // UniTaskの非同期処理をキャンセル（一時停止）するためのトークン
    private CancellationTokenSource _timeCts;

    protected override void OnAwakeInitialize()
    {
        // 初期化時のバリデーション（範囲外の日付が設定されていたら修正）
        ValidateDate();
    }

    /// <summary>
    /// 早送りで時間を進める（再開）
    /// </summary>
    public async UniTask StartFastForwardAsync()
    {
        // 既に時間送りが実行中であれば何もしない
        if (_timeCts != null) return;

        _timeCts = new CancellationTokenSource();
        CancellationToken token = _timeCts.Token;

        try
        {
            // キャンセル（一時停止）されるまでループし続ける
            while (!token.IsCancellationRequested)
            {
                // 指定した秒数だけ待機
                await UniTask.Delay(TimeSpan.FromSeconds(_secondsPerDay), cancellationToken: token);
                
                // 1日進める
                AddDay();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancel()が呼ばれるとここを通る（正常な一時停止処理）
        }
    }

    /// <summary>
    /// 時間送りを一時停止する
    /// </summary>
    public void PauseFastForward()
    {
        if (_timeCts != null)
        {
            _timeCts.Cancel();
            _timeCts.Dispose();
            _timeCts = null;
        }
    }

    /// <summary>
    /// 日付を1日進める内部処理
    /// </summary>
    private void AddDay()
    {
        _currentDay++;

        // その月の最大日を超えたら翌月へ
        if (_currentDay > _daysInMonth[_currentMonth])
        {
            _currentDay = 1;
            _currentMonth++;

            // 12月を超えたら1月へ（年は管理しないループ）
            if (_currentMonth > 12)
            {
                _currentMonth = 1;
            }
            
            OnMonthChanged?.Invoke(_currentMonth);
        }

        // 日付変更イベントを発火
        OnDateChanged?.Invoke(_currentMonth, _currentDay);
    }

    /// <summary>
    /// インスペクタ等で設定された初期値の妥当性をチェックする
    /// </summary>
    private void ValidateDate()
    {
        _currentMonth = Mathf.Clamp(_currentMonth, 1, 12);
        _currentDay = Mathf.Clamp(_currentDay, 1, _daysInMonth[_currentMonth]);
    }

    protected override void OnDestroy()
    {
        // オブジェクト破棄時にタスクが動き続けないよう安全にキャンセル
        PauseFastForward();
        
        base.OnDestroy();
    }
}