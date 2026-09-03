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
                return LSProcessResult.Success;
            }, conditions: _ => false)
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResult.Success));
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
                return LSProcessResult.Success;
            }, conditions: new LSProcessNodeCondition[] { _ => true, _ => false })
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResult.Success));
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
                return LSProcessResult.Success;
            })
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var second = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResult.Success));
            Assert.That(second, Is.EqualTo(LSProcessResult.Success));
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
                    ? LSProcessResult.Waiting
                    : LSProcessResult.Success;
            })
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var resumed = process.Resume();

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResult.Waiting));
            Assert.That(resumed, Is.EqualTo(LSProcessResult.Success));
        }
    }

    [Test]
    public void Handler_WaitingThenFail_ShouldTransitionToFailure() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Handler("wait", session => LSProcessResult.Waiting)
        );

        var first = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);
        var failed = process.Fail();

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(LSProcessResult.Waiting));
            Assert.That(failed, Is.EqualTo(LSProcessResult.Failure));
        }
    }
}
