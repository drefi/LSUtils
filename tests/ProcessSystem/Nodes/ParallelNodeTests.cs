using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ParallelNodeTests {
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.NONE)]
    public void Parallel_AllSuccess_ShouldReturnSuccess(LSProcessNodeParallel.ParallelThresholdMode mode) {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("a", _ => LSProcessResultStatus.SUCCESS)
                .Handler("b", _ => LSProcessResultStatus.SUCCESS)
                .Handler("c", _ => LSProcessResultStatus.SUCCESS),
                successThreshold: 2,
                failureThreshold: 2,
                thresholdMode: mode)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.NONE)]
    public void Parallel_AllFailure_ShouldReturnFailure(LSProcessNodeParallel.ParallelThresholdMode mode) {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("a", _ => LSProcessResultStatus.FAILURE)
                .Handler("b", _ => LSProcessResultStatus.FAILURE)
                .Handler("c", _ => LSProcessResultStatus.FAILURE),
                successThreshold: 2,
                failureThreshold: 2,
                thresholdMode: mode)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY, LSProcessResultStatus.SUCCESS)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY, LSProcessResultStatus.FAILURE)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.NONE, LSProcessResultStatus.WAITING)]
    public void Parallel_MixedSuccessFailureWaiting_WithThresholdOne_ShouldDependOnMode(
        LSProcessNodeParallel.ParallelThresholdMode mode,
        LSProcessResultStatus expected) {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("ok", _ => LSProcessResultStatus.SUCCESS)
                .Handler("ko", _ => LSProcessResultStatus.FAILURE)
                .Handler("wait", _ => LSProcessResultStatus.WAITING),
                successThreshold: 1,
                failureThreshold: 1,
                thresholdMode: mode)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Parallel_SuccessPriority_WithZeroSuccessThreshold_ShouldAlwaysSucceed() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("fail", _ => LSProcessResultStatus.FAILURE)
                .Handler("wait", _ => LSProcessResultStatus.WAITING),
                successThreshold: 0,
                failureThreshold: 2,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void Parallel_FailurePriority_WithZeroFailureThreshold_ShouldAlwaysFail() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("ok", _ => LSProcessResultStatus.SUCCESS)
                .Handler("wait", _ => LSProcessResultStatus.WAITING),
                successThreshold: 2,
                failureThreshold: 0,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY, LSProcessResultStatus.SUCCESS)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY, LSProcessResultStatus.FAILURE)]
    [TestCase(LSProcessNodeParallel.ParallelThresholdMode.NONE, LSProcessResultStatus.SUCCESS)]
    public void Parallel_BothThresholdsMet_ShouldFollowModePriority(
        LSProcessNodeParallel.ParallelThresholdMode mode,
        LSProcessResultStatus expected) {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("s1", _ => LSProcessResultStatus.SUCCESS)
                .Handler("s2", _ => LSProcessResultStatus.SUCCESS)
                .Handler("f1", _ => LSProcessResultStatus.FAILURE)
                .Handler("f2", _ => LSProcessResultStatus.FAILURE),
                successThreshold: 2,
                failureThreshold: 2,
                thresholdMode: mode)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Parallel_NoneMode_ShouldWaitUntilNoWaitingChildren() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var waitExecutions = 0;

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("ok", _ => LSProcessResultStatus.SUCCESS)
                .Handler("wait", _ => {
                    waitExecutions++;
                    return waitExecutions == 1
                        ? LSProcessResultStatus.WAITING
                        : LSProcessResultStatus.SUCCESS;
                }),
                successThreshold: 2,
                failureThreshold: 2,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.NONE)
        );

        var initial = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var resumed = process.Resume("wait");

        using (Assert.EnterMultipleScope()) {
            Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
            Assert.That(resumed, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        }
    }

    [Test]
    public void Parallel_FailWaitingChild_ShouldReachFailureThreshold() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("wait", _ => LSProcessResultStatus.WAITING)
                .Handler("ok", _ => LSProcessResultStatus.SUCCESS),
                successThreshold: 2,
                failureThreshold: 1,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.FAILURE_PRIORITY)
        );

        var initial = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var failed = process.Fail("wait");

        using (Assert.EnterMultipleScope()) {
            Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
            Assert.That(failed, Is.EqualTo(LSProcessResultStatus.FAILURE));
        }
    }

    [Test]
    public void Parallel_Cancel_ShouldSetProcessCancelled() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("wait1", _ => LSProcessResultStatus.WAITING)
                .Handler("wait2", _ => LSProcessResultStatus.WAITING),
                successThreshold: 2,
                failureThreshold: 2,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.NONE)
        );

        var initial = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        process.Cancel();

        using (Assert.EnterMultipleScope()) {
            Assert.That(initial, Is.EqualTo(LSProcessResultStatus.WAITING));
            Assert.That(process.IsCancelled, Is.True);
        }
    }

    [Test]
    public void Parallel_NegativeSuccessThreshold_WithMixedResults_ShouldNotFailWhenFailureThresholdNotMet() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Parallel("parallel", par => par
                .Handler("ok", _ => LSProcessResultStatus.SUCCESS)
                .Handler("fail", _ => LSProcessResultStatus.FAILURE),
                successThreshold: -1,
                failureThreshold: 2,
                thresholdMode: LSProcessNodeParallel.ParallelThresholdMode.SUCCESS_PRIORITY)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }
}
