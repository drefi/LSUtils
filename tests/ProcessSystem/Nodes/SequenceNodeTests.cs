using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class SequenceNodeTests {
    [Test]
    public void Sequence_ShouldRunChildrenInOrder() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var steps = new List<string>();

        process.WithProcessing(builder => builder
            .Sequence("sequence", seq => seq
                .Handler("first", session => {
                    steps.Add("first");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("second", session => {
                    steps.Add("second");
                    return LSProcessResultStatus.SUCCESS;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(steps, Is.EqualTo(new[] { "first", "second" }));
        }
    }

    [Test]
    public void Sequence_ShouldShortCircuitOnFirstFailure() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var steps = new List<string>();

        process.WithProcessing(builder => builder
            .Sequence("sequence", seq => seq
                .Handler("first", session => {
                    steps.Add("first");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("stop", session => {
                    steps.Add("stop");
                    return LSProcessResultStatus.FAILURE;
                })
                .Handler("never", session => {
                    steps.Add("never");
                    return LSProcessResultStatus.SUCCESS;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
            Assert.That(steps, Is.EqualTo(new[] { "first", "stop" }));
        }
    }

    [Test]
    public void Sequence_WaitingChild_ShouldReturnWaiting() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Sequence("sequence", seq => seq
                .Handler("first", session => LSProcessResultStatus.SUCCESS)
                .Handler("wait", session => LSProcessResultStatus.WAITING)
                .Handler("never", session => LSProcessResultStatus.SUCCESS))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
    }

    [Test]
    public void Sequence_ConditionFalse_ShouldSkipEntireSequence() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var gatedExecuted = false;
        var fallbackExecuted = false;

        process.WithProcessing(builder => builder
            .Sequence("gated", seq => seq
                .Handler("inside", session => {
                    gatedExecuted = true;
                    return LSProcessResultStatus.SUCCESS;
                }),
                conditions: _ => false)
            .Handler("fallback", session => {
                fallbackExecuted = true;
                return LSProcessResultStatus.SUCCESS;
            })
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(gatedExecuted, Is.False);
            Assert.That(fallbackExecuted, Is.True);
        }
    }
}
