using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class SelectorNodeTests {
    [Test]
    public void Selector_ShouldStopAfterFirstSuccess() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var steps = new List<string>();

        process.WithProcessing(builder => builder
            .Selector("selector", sel => sel
                .Handler("fail", session => {
                    steps.Add("fail");
                    return LSProcessResultStatus.FAILURE;
                })
                .Handler("win", session => {
                    steps.Add("win");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("skip", session => {
                    steps.Add("skip");
                    return LSProcessResultStatus.SUCCESS;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(steps, Is.EqualTo(new[] { "fail", "win" }));
        }
    }

    [Test]
    public void Selector_AllFail_ShouldReturnFailure() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Selector("selector", sel => sel
                .Handler("a", session => LSProcessResultStatus.FAILURE)
                .Handler("b", session => LSProcessResultStatus.FAILURE))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
    }

    [Test]
    public void Selector_ConditionFalseOnFirstChild_ShouldEvaluateNextChild() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();
        var firstExecuted = false;
        var secondExecuted = false;

        process.WithProcessing(builder => builder
            .Selector("selector", sel => sel
                .Handler("gated", session => {
                    firstExecuted = true;
                    return LSProcessResultStatus.SUCCESS;
                }, conditions: _ => false)
                .Handler("next", session => {
                    secondExecuted = true;
                    return LSProcessResultStatus.SUCCESS;
                }))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(firstExecuted, Is.False);
            Assert.That(secondExecuted, Is.True);
        }
    }

    [Test]
    public void Selector_WaitingFirstChild_ShouldReturnWaiting() {
        var manager = new LSProcessManager();
        var process = new PipelineTestProcess();

        process.WithProcessing(builder => builder
            .Selector("selector", sel => sel
                .Handler("wait", session => LSProcessResultStatus.WAITING)
                .Handler("next", session => LSProcessResultStatus.SUCCESS))
        );

        var result = process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL);

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
    }
}
