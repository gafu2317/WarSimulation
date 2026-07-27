using System;

public sealed class CombatAiDecisionSchedule
{
    private const double TimeTolerance = 0.000001d;

    private readonly double _intervalSeconds;
    private double _nextDecisionTime;
    private bool _isInitialized;

    public double NextDecisionTime => _nextDecisionTime;

    public CombatAiDecisionSchedule(float intervalSeconds)
    {
        if (intervalSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalSeconds),
                intervalSeconds,
                "AI decision interval must be greater than zero.");
        }

        _intervalSeconds = intervalSeconds;
    }

    public void Reset(double startTime)
    {
        _nextDecisionTime = startTime;
        _isInitialized = true;
    }

    public bool TryConsume(double currentTime, out int skippedDecisionCount)
    {
        skippedDecisionCount = 0;
        if (!_isInitialized)
        {
            Reset(currentTime);
        }

        if (currentTime + TimeTolerance < _nextDecisionTime)
        {
            return false;
        }

        int dueDecisionCount = Math.Max(
            1,
            (int)Math.Floor((currentTime - _nextDecisionTime) / _intervalSeconds) + 1);
        skippedDecisionCount = dueDecisionCount - 1;
        _nextDecisionTime += dueDecisionCount * _intervalSeconds;
        return true;
    }
}
