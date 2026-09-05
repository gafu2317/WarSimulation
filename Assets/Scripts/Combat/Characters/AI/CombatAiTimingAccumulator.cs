using System;
using System.Collections.Generic;

internal sealed class CombatAiTimingAccumulator
{
    private readonly List<double> _durationsMilliseconds = new();
    private double _totalMilliseconds;
    private double _minimumMilliseconds = double.PositiveInfinity;
    private double _maximumMilliseconds;

    public int Count => _durationsMilliseconds.Count;
    public double AverageMilliseconds => Count > 0 ? _totalMilliseconds / Count : 0d;
    public double MinimumMilliseconds => Count > 0 ? _minimumMilliseconds : 0d;
    public double MaximumMilliseconds => Count > 0 ? _maximumMilliseconds : 0d;

    public double MedianMilliseconds
    {
        get
        {
            if (Count == 0) return 0d;

            double[] sorted = _durationsMilliseconds.ToArray();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) / 2d;
        }
    }

    public double StandardDeviationMilliseconds
    {
        get
        {
            if (Count == 0) return 0d;

            double average = AverageMilliseconds;
            double squaredDifferenceTotal = 0d;
            for (int i = 0; i < Count; i++)
            {
                double difference = _durationsMilliseconds[i] - average;
                squaredDifferenceTotal += difference * difference;
            }

            return Math.Sqrt(squaredDifferenceTotal / Count);
        }
    }

    public void Reset()
    {
        _durationsMilliseconds.Clear();
        _totalMilliseconds = 0d;
        _minimumMilliseconds = double.PositiveInfinity;
        _maximumMilliseconds = 0d;
    }

    public void Record(long startTimestamp)
    {
        double durationMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000d /
            System.Diagnostics.Stopwatch.Frequency;
        RecordMilliseconds(durationMilliseconds);
    }

    public void RecordMilliseconds(double durationMilliseconds)
    {
        _durationsMilliseconds.Add(durationMilliseconds);
        _totalMilliseconds += durationMilliseconds;
        _minimumMilliseconds = Math.Min(_minimumMilliseconds, durationMilliseconds);
        _maximumMilliseconds = Math.Max(_maximumMilliseconds, durationMilliseconds);
    }
}

internal sealed class CombatAiBatchPhaseMeasurements
{
    public double ContextMilliseconds { get; private set; }
    public double PlanningMilliseconds { get; private set; }

    public void Reset()
    {
        ContextMilliseconds = 0d;
        PlanningMilliseconds = 0d;
    }

    public void AddContext(long startTimestamp)
    {
        ContextMilliseconds += ElapsedMilliseconds(startTimestamp);
    }

    public void AddPlanning(long startTimestamp)
    {
        PlanningMilliseconds += ElapsedMilliseconds(startTimestamp);
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000d /
            System.Diagnostics.Stopwatch.Frequency;
    }
}
