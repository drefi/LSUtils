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
                    return LSProcessResult.Success;
                })
                .Handler("second", session => {
                    steps.Add("second");
                    return LSProcessResult.Success;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResult.Success));
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
                    return LSProcessResult.Success;
                })
                .Handler("stop", session => {
                    steps.Add("stop");
                    return LSProcessResult.Failure;
                })
                .Handler("never", session => {
                    steps.Add("never");
                    return LSProcessResult.Success;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResult.Failure));
            Assert.That(steps, Is.EqualTo(new[] { "first", "stop" }));
        }
    }

    [Test]
    public void Sequence_WaitingChild_ShouldReturnWaiting() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Sequence("sequence", seq => seq
                .Handler("first", session => LSProcessResult.Success)
                .Handler("wait", session => LSProcessResult.Waiting)
                .Handler("never", session => LSProcessResult.Success))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResult.Waiting));
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
                    return LSProcessResult.Success;
                }),
                conditions: _ => false)
            .Handler("fallback", session => {
                fallbackExecuted = true;
                return LSProcessResult.Success;
            })
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResult.Success));
            Assert.That(gatedExecuted, Is.False);
            Assert.That(fallbackExecuted, Is.True);
        }
    }
}
