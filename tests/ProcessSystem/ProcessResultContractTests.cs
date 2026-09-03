using System;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessResultContractTests {
    [TestCase(true)]
    [TestCase(false)]
    public void DeepNestedContinuation_PreservesPayloadThroughTwoInverters(bool resume) {
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Sequence("outer", outer => outer
            .Selector("choice", choice => choice
                .Inverter("first-inverter", first => first
                    .Inverter("second-inverter", second => second
                        .Handler("wait", _ => LSProcessResult.WaitingFor("pending")))))));

        Assert.That(process.Execute(new LSProcessManager()).GetPayload<string>(), Is.EqualTo("pending"));
        var result = resume ? process.Resume("accepted") : process.Fail("rejected");

        Assert.That(result.IsSuccess, Is.EqualTo(resume));
        Assert.That(result.IsFailure, Is.EqualTo(!resume));
        var first = result.GetPayload<LSProcessInversion>();
        var second = first!.Original.GetPayload<LSProcessInversion>();
        Assert.That(second!.Original.GetPayload<string>(), Is.EqualTo(resume ? "accepted" : "rejected"));
    }

    [Test]
    public void SelectorExhaustion_ExposesLastFailureAndRetainsEveryAttempt() {
        LSProcessSession? session = null;
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Selector("choice", choice => choice
            .Handler("first", current => {
                session = current;
                return LSProcessResult.Failed("first-reason");
            })
            .Handler("last", _ => LSProcessResult.Failed("last-reason"))));

        var result = process.Execute(new LSProcessManager());
        var choice = session!.RootNode.GetChild("choice")!;

        Assert.That(result.GetPayload<string>(), Is.EqualTo("last-reason"));
        Assert.That(choice.GetChild("first")!.Status.GetPayload<string>(), Is.EqualTo("first-reason"));
        Assert.That(choice.GetChild("last")!.Status.GetPayload<string>(), Is.EqualTo("last-reason"));
    }

    [TestCase("success")]
    [TestCase("failure")]
    [TestCase("cancelled")]
    [TestCase("undetermined")]
    public void ResumeAndFail_AfterNonWaitingResult_PreserveResult(string outcome) {
        var process = ProcessReturning(outcome);
        var initial = process.Execute(new LSProcessManager());

        Assert.That(process.Resume(), Is.EqualTo(initial));
        Assert.That(process.Fail(), Is.EqualTo(initial));
    }

    [TestCase("success")]
    [TestCase("failure")]
    [TestCase("undetermined")]
    public void ExplicitCancellation_AfterCompletion_ReplacesOutcome(string outcome) {
        var process = ProcessReturning(outcome);
        process.Execute(new LSProcessManager());

        var result = process.Cancel("late-cancellation");

        Assert.That(result.IsCancelled, Is.True);
        Assert.That(result.GetPayload<string>(), Is.EqualTo("late-cancellation"));
    }

    [Test]
    public void CancellationInsideHandler_WinsOverReturnValueAndPreservesReason() {
        var tailCalls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root
            .Handler("cancel", session => {
                session.Cancel(new CancellationReason("superseded"));
                return LSProcessResult.Failed("ignored");
            })
            .Handler("tail", _ => {
                tailCalls++;
                return LSProcessResult.Success;
            }));

        var result = process.Execute(new LSProcessManager());

        Assert.That(result.GetPayload<CancellationReason>()!.Message, Is.EqualTo("superseded"));
        Assert.That(tailCalls, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WaitingInverter_PreservesResolutionProvenance(bool resume) {
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Inverter("inverse", inverse =>
            inverse.Handler("wait", _ => LSProcessResult.WaitingFor("input"))));
        process.Execute(new LSProcessManager());

        var result = resume ? process.Resume(23) : process.Fail(23);
        var inversion = result.GetPayload<LSProcessInversion>();

        Assert.That(result.IsFailure, Is.EqualTo(resume));
        Assert.That(result.IsSuccess, Is.EqualTo(!resume));
        Assert.That(inversion!.Original.GetPayload<int>(), Is.EqualTo(23));
        Assert.That(inversion.Original.IsSuccess, Is.EqualTo(resume));
    }

    [Test]
    public void ConditionException_IsRetainedAndDoesNotInvokeHandler() {
        var calls = 0;
        var error = new InvalidOperationException("condition failed");
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Handler("guarded", _ => {
            calls++;
            return LSProcessResult.Success;
        }, conditions: _ => throw error));

        Assert.That(Assert.Throws<InvalidOperationException>(
            () => process.Execute(new LSProcessManager())), Is.SameAs(error));
        Assert.That(Assert.Throws<InvalidOperationException>(() => process.Resume()), Is.SameAs(error));
        Assert.That(calls, Is.Zero);
    }

    [TestCase("execute")]
    [TestCase("resume")]
    [TestCase("fail")]
    public void ReentrantControlFromHandler_IsRejectedAndFaultIsRetained(string operation) {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Handler("reentrant", session => {
            calls++;
            return operation switch {
                "execute" => process.Execute(new LSProcessManager()),
                "resume" => session.Resume(),
                _ => session.Fail()
            };
        }));

        var error = Assert.Throws<InvalidOperationException>(
            () => process.Execute(new LSProcessManager()));

        Assert.That(error!.Message, Does.Contain("reentrant"));
        Assert.That(Assert.Throws<InvalidOperationException>(() => process.Resume()), Is.SameAs(error));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void ReentrantExecuteFromCondition_IsRejectedBeforeHandlerRuns() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Handler("guarded", _ => {
            calls++;
            return LSProcessResult.Success;
        }, conditions: _ => process.Execute(new LSProcessManager()).IsSuccess));

        Assert.Throws<InvalidOperationException>(() => process.Execute(new LSProcessManager()));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void EmptyCompositeContracts_AreExplicit() {
        var sequence = new PipelineTestProcess();
        sequence.WithProcessing(root => root.Sequence("empty", empty => empty));
        Assert.That(sequence.Execute(new LSProcessManager()).IsSuccess, Is.True);

        var selector = new PipelineTestProcess();
        selector.WithProcessing(root => root.Selector("empty", empty => empty));
        Assert.That(selector.Execute(new LSProcessManager()).IsFailure, Is.True);

        var inverter = new PipelineTestProcess();
        inverter.WithProcessing(root => root.Inverter("empty", empty => empty));
        Assert.That(inverter.Execute(new LSProcessManager()).IsUndetermined, Is.True);
    }

    private static PipelineTestProcess ProcessReturning(string outcome) {
        var process = new PipelineTestProcess();
        process.WithProcessing(root => root.Handler("result", session => outcome switch {
            "success" => LSProcessResult.Succeeded("done"),
            "failure" => LSProcessResult.Failed("failed"),
            "cancelled" => session.Cancel("cancelled"),
            _ => LSProcessResult.UndeterminedBy("unknown")
        }));
        return process;
    }

    private sealed record CancellationReason(string Message);
}
