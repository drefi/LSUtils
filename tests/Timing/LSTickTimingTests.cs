namespace LSUtils.Tests.Timing;

using LSUtils.Timing;
using NUnit.Framework;

[TestFixture]
public sealed class LSTickTimingTests {
    [Test]
    public void ImmediateSchedule_IncludesActivationTick() {
        var timing = new LSTickTiming(3500f, 1000f);

        Assert.That(timing.ExpectedTickCount, Is.EqualTo(4));
    }

    [Test]
    public void DelayedSchedule_OnlyCountsIntervalsInsideDuration() {
        var timing = new LSTickTiming(3500f, 1000f, LSTickStartMode.AfterInterval);
        var shortTiming = new LSTickTiming(500f, 1000f, LSTickStartMode.AfterInterval);

        Assert.That(timing.ExpectedTickCount, Is.EqualTo(3));
        Assert.That(shortTiming.ExpectedTickCount, Is.Zero);
    }

    [Test]
    public void Timer_AccumulatesPartialDeltasWithoutLosingTime() {
        var timer = new LSTickTimer(new LSTickTiming(3500f, 1000f, LSTickStartMode.AfterInterval));

        Assert.That(timer.Advance(400f), Is.Zero);
        Assert.That(timer.Advance(700f), Is.EqualTo(1));
        Assert.That(timer.Advance(2000f), Is.EqualTo(2));
    }
}
