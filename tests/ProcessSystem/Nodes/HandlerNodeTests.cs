using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class HandlerNodeTests {
    [Test]
    public void Handler_ConditionFalse_ShouldSkipExecution() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var executed = false;

        process.WithProcessing(builder => builder
            .Handler("guarded", session => {
                executed = true;
                return LSProcessResultStatus.SUCCESS;
            }, conditions: _ => false)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(executed, Is.False);
        }
    }

    [Test]
    public void Handler_MultipleConditions_ShouldRequireAllTrue() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var executed = false;

        process.WithProcessing(builder => builder
            .Handler("guarded", session => {
                executed = true;
                return LSProcessResultStatus.SUCCESS;
            }, conditions: new LSProcessNodeCondition[] { _ => true, _ => false })
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(executed, Is.False);
        }
    }

    [Test]
    public void Handler_TerminalStatus_ShouldBeCachedAcrossExecuteCalls() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var executionCount = 0;

        process.WithProcessing(builder => builder
            .Handler("counted", session => {
                executionCount++;
                return LSProcessResultStatus.SUCCESS;
            })
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var second = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(second, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(executionCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Handler_WaitingThenResume_ShouldTransitionToSuccess() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var calls = 0;

        process.WithProcessing(builder => builder
            .Handler("wait", session => {
                calls++;
                return calls == 1
                    ? LSProcessResultStatus.WAITING
                    : LSProcessResultStatus.SUCCESS;
            })
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var resumed = process.Resume("wait");

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResultStatus.WAITING));
            Assert.That(resumed, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        }
    }

    [Test]
    public void Handler_WaitingThenFail_ShouldTransitionToFailure() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Handler("wait", session => LSProcessResultStatus.WAITING)
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var failed = process.Fail("wait");

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResultStatus.WAITING));
            Assert.That(failed, Is.EqualTo(LSProcessResultStatus.FAILURE));
        }
    }
}
