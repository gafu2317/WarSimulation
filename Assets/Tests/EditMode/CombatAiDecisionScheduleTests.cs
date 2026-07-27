using NUnit.Framework;

public sealed class CombatAiDecisionScheduleTests
{
    [Test]
    public void TryConsume_KeepsTheDecisionTimeOnTheOriginalSchedule()
    {
        var schedule = new CombatAiDecisionSchedule(0.5f);
        schedule.Reset(10d);

        Assert.That(schedule.TryConsume(10d, out int initialSkippedCount), Is.True);
        Assert.That(initialSkippedCount, Is.Zero);
        Assert.That(schedule.TryConsume(10.63d, out int delayedSkippedCount), Is.True);
        Assert.That(delayedSkippedCount, Is.Zero);
        Assert.That(schedule.NextDecisionTime, Is.EqualTo(11d).Within(0.000001d));
    }

    [Test]
    public void TryConsume_ReportsDecisionTimesSkippedByALongFrame()
    {
        var schedule = new CombatAiDecisionSchedule(0.5f);
        schedule.Reset(0d);
        schedule.TryConsume(0d, out _);

        bool consumed = schedule.TryConsume(1.6d, out int skippedCount);

        Assert.That(consumed, Is.True);
        Assert.That(skippedCount, Is.EqualTo(2));
        Assert.That(schedule.NextDecisionTime, Is.EqualTo(2d).Within(0.000001d));
    }

    [Test]
    public void TryConsume_DoesNotRunBeforeTheNextDecisionTime()
    {
        var schedule = new CombatAiDecisionSchedule(0.5f);
        schedule.Reset(3d);
        schedule.TryConsume(3d, out _);

        bool consumed = schedule.TryConsume(3.49d, out int skippedCount);

        Assert.That(consumed, Is.False);
        Assert.That(skippedCount, Is.Zero);
        Assert.That(schedule.NextDecisionTime, Is.EqualTo(3.5d).Within(0.000001d));
    }
}
