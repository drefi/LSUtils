using System;
using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessExecutionTests {
    private static IEnumerable<TestCaseData> ContinuationCases() {
        foreach (var shape in new[] { "sequence", "selector", "inverter", "nested" }) {
            foreach (var status in new[] { LSProcessResult.Success, LSProcessResult.Failure,
                LSProcessResult.Waiting, LSProcessResult.Cancelled }) {
                foreach (var control in status == LSProcessResult.Waiting
                    ? new[] { "resume", "fail", "cancel" } : new[] { "none" }) {
                    yield return new TestCaseData(shape, status, control);
                }
            }
        }
    }

    private static ILSProcessLayerNode Build(string shape, LSProcessResult status, List<string> trace) {
        var root = LSProcessManager.CreateRootNode("root");
        var builder = new LSProcessTreeBuilder(root);
        LSProcessHandler first = _ => { trace.Add("first"); return status; };
        LSProcessHandler next = _ => { trace.Add("next"); return LSProcessResult.Success; };
        switch (shape) {
            case "sequence": builder.Sequence("test", b => b.Handler("first", first).Handler("next", next)); break;
            case "selector": builder.Selector("test", b => b.Handler("first", first).Handler("next", next)); break;
            case "inverter": builder.Inverter("test", b => b.Handler("first", first)); break;
            default: builder.Selector("test", b => b
                .Sequence("path", s => s.Inverter("inverse", i => i.Handler("first", first)))
                .Handler("next", next)); break;
        }
        builder.Handler("tail", _ => { trace.Add("tail"); return LSProcessResult.Success; });
        return root;
    }

    [TestCaseSource(nameof(ContinuationCases))]
    public void ContinuationPreservesValidatedContract(string shape, LSProcessResult status, string control) {
        var trace = new List<string>();
        var newRoot = Build(shape, status, trace);
        var manager = new LSProcessManager();
        var session = new LSProcessSession(manager, new PipelineTestProcess(), newRoot,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        // This matrix was compared against the original executor before its removal.
        var initialExpected = ExpectedOutcome(shape, status);
        Assert.That(session.Execute(), Is.EqualTo(initialExpected));
        var effective = control switch {
            "resume" => LSProcessResult.Success,
            "fail" => LSProcessResult.Failure,
            "cancel" => LSProcessResult.Cancelled,
            _ => status
        };
        var expected = ExpectedOutcome(shape, effective);
        if (control != "none") {
            var actual = control == "resume" ? session.Resume()
                : control == "fail" ? session.Fail() : session.Cancel();
            Assert.That(actual, Is.EqualTo(expected));
        }
        var expectedTrace = new List<string> { "first" };
        if ((shape == "sequence" || shape == "nested") && effective == LSProcessResult.Success ||
            shape == "selector" && effective == LSProcessResult.Failure) expectedTrace.Add("next");
        if (expected == LSProcessResult.Success) expectedTrace.Add("tail");
        Assert.That(session.RootNode.Status, Is.EqualTo(expected));
        Assert.That(session.Execute(), Is.EqualTo(expected));
        Assert.That(trace, Is.EqualTo(expectedTrace));
    }

    private static LSProcessResult ExpectedOutcome(string shape, LSProcessResult status) {
        if (status.IsWaiting || status.IsCancelled) return status;
        return shape switch {
            "sequence" => status,
            "inverter" => status == LSProcessResult.Success
                ? LSProcessResult.Failure : LSProcessResult.Success,
            _ => LSProcessResult.Success
        };
    }

    [Test]
    public void SharedDefinition_HasIndependentStatusesAndCounters() {
        var root = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(root).Handler("wait", _ => LSProcessResult.Waiting);
        var definition = LSProcessDefinition.Compile(root);
        var manager = new LSProcessManager();
        var one = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        var two = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        Assert.That(one.Execute(), Is.EqualTo(LSProcessResult.Waiting));
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResult.NotExecuted));
        Assert.That(two.RootNode.GetChild("wait")!.ExecutionCount, Is.Zero);
        two.Execute();
        one.Resume();
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResult.Waiting));
        two.Fail();
        Assert.That(one.RootNode.Status, Is.EqualTo(LSProcessResult.Success));
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResult.Failure));
        Assert.That(one.RootNode.GetChild("wait")!.ExecutionCount, Is.EqualTo(1));
        Assert.That(two.RootNode.GetChild("wait")!.ExecutionCount, Is.EqualTo(1));
    }

    [Test]
    public void TypedContext_SharesIdentityCurrentNodeAndContinuation() {
        LSProcessSession<PipelineTestProcess>? typed = null;
        LSProcessSession? untyped = null;
        var completions = 0;
        LSProcessHandler<PipelineTestProcess> wait = s => {
            typed = s;
            Assert.That(s.CurrentNode!.NodeID, Is.EqualTo("wait"));
            return LSProcessResult.Waiting;
        };
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("capture", s => {
            untyped = s; return LSProcessResult.Success;
        }).Handler("wait", wait.ToHandler()).Handler("tail", _ => {
            completions++; return LSProcessResult.Success;
        }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResult.Waiting));
        Assert.That(typed!.SessionID, Is.EqualTo(untyped!.SessionID));
        Assert.That(typed.RootNode, Is.SameAs(untyped.RootNode));
        Assert.That(typed.CurrentNode, Is.Null);
        Assert.That(typed.Resume(), Is.EqualTo(LSProcessResult.Success));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void RetainedBuilderAndConditionArray_CannotChangeWaitingExecution() {
        LSProcessTreeBuilder? retained = null;
        var trace = new List<string>();
        var conditions = new LSProcessNodeCondition?[] { _ => true };
        var process = new PipelineTestProcess();
        process.WithProcessing(b => {
            retained = b;
            return b.Handler("wait", _ => LSProcessResult.Waiting)
                .Sequence("future", s => s.Handler("original", _ => {
                    trace.Add("original"); return LSProcessResult.Success;
                }, conditions: conditions));
        });
        process.Execute(new LSProcessManager());
        conditions[0] = _ => false;
        retained!.Sequence("future", s => s.Handler("injected", _ => {
            trace.Add("injected"); return LSProcessResult.Success;
        }));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResult.Success));
        Assert.That(trace, Is.EqualTo(new[] { "original" }));
    }

    [Test]
    public void CallbackException_RestoresContextAndCannotReexecutePartialWork() {
        LSProcessSession? session = null;
        var calls = 0;
        var exception = new InvalidOperationException("failed callback");
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("throw", s => {
            session = s; calls++; throw exception;
        }));
        Assert.That(Assert.Throws<InvalidOperationException>(() => process.Execute(new LSProcessManager())), Is.SameAs(exception));
        Assert.That(session!.CurrentNode, Is.Null);
        Assert.That(Assert.Throws<InvalidOperationException>(() => session.Resume()), Is.SameAs(exception));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void EligibilityIsCapturedWhenEachLayerStarts_NotWhenDefinitionIsBuilt() {
        var allowed = false;
        var calls = 0;
        var conditionCalls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("wait", _ => LSProcessResult.Waiting)
            .Sequence("future", s => s.Handler("conditional", _ => {
                calls++; return LSProcessResult.Success;
            }, conditions: _ => { conditionCalls++; return allowed; })));
        process.Execute(new LSProcessManager());
        Assert.That(conditionCalls, Is.Zero);
        allowed = true;
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResult.Success));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(conditionCalls, Is.EqualTo(1));
    }

    [Test]
    public void CancelFromHandler_PreventsTailAndCannotBeOverwrittenByReturnValue() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("cancel", s => {
            s.Cancel(); return LSProcessResult.Success;
        }).Handler("tail", _ => { calls++; return LSProcessResult.Success; }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResult.Cancelled));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResult.Cancelled));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void ConditionsCanCancelWithoutInvokingPendingHandlers() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("guarded", _ => {
            calls++; return LSProcessResult.Success;
        }, conditions: p => { p.Cancel(); return true; }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResult.Cancelled));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void UnknownResultIsNotRepeatedOrReportedAsSuccess() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("unknown", _ => {
            calls++; return LSProcessResult.Undetermined;
        }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResult.Undetermined));
        Assert.That(process.Execute(), Is.EqualTo(LSProcessResult.Undetermined));
        Assert.That(process.IsCompleted, Is.False);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void SequenceAndSelector_PreserveDecisivePayloads() {
        var sequence = new PipelineTestProcess();
        sequence.WithProcessing(b => b.Handler("first", _ => LSProcessResult.Success)
            .Handler("last", _ => LSProcessResult.Succeeded("sequence-result")));
        var sequenceResult = sequence.Execute(new LSProcessManager());
        Assert.That(sequenceResult.GetPayload<string>(), Is.EqualTo("sequence-result"));

        var selector = new PipelineTestProcess();
        selector.WithProcessing(b => b.Selector("choice", choice => choice
            .Handler("rejected", _ => LSProcessResult.Failed("not-this-one"))
            .Handler("selected", _ => LSProcessResult.Succeeded(42))));
        var selectorResult = selector.Execute(new LSProcessManager());
        Assert.That(selectorResult.GetPayload<int>(), Is.EqualTo(42));
    }

    [Test]
    public void WaitingResolution_PreservesTypedResumeAndFailurePayloads() {
        var resumed = new PipelineTestProcess();
        resumed.WithProcessing(b => b.Handler("wait", _ => LSProcessResult.WaitingFor("approval")));
        Assert.That(resumed.Execute(new LSProcessManager()).GetPayload<string>(), Is.EqualTo("approval"));
        Assert.That(resumed.Resume(17).GetPayload<int>(), Is.EqualTo(17));

        var failed = new PipelineTestProcess();
        failed.WithProcessing(b => b.Handler("wait", _ => LSProcessResult.Waiting));
        failed.Execute(new LSProcessManager());
        Assert.That(failed.Fail(new InvalidOperationException("denied")).GetPayload<Exception>()!.Message,
            Is.EqualTo("denied"));
    }

    [Test]
    public void CancellationAndInversion_PreserveReasonAndProvenance() {
        var cancelled = new PipelineTestProcess();
        cancelled.WithProcessing(b => b.Handler("wait", _ => LSProcessResult.Waiting));
        cancelled.Execute(new LSProcessManager());
        Assert.That(cancelled.Cancel("user-request").GetPayload<string>(), Is.EqualTo("user-request"));

        var inverted = new PipelineTestProcess();
        inverted.WithProcessing(b => b.Inverter("inverse", inverse => inverse
            .Handler("success", _ => LSProcessResult.Succeeded("original"))));
        var inversion = inverted.Execute(new LSProcessManager()).GetPayload<LSProcessInversion>();
        Assert.That(inversion!.Original.IsSuccess, Is.True);
        Assert.That(inversion.Original.GetPayload<string>(), Is.EqualTo("original"));
    }

    [Test]
    public void DeclaredNullPayload_RemainsTyped() {
        var result = LSProcessResult.Succeeded<string?>(null);
        Assert.That(result.HasPayload, Is.True);
        Assert.That(result.PayloadType, Is.EqualTo(typeof(string)));
        Assert.That(result.TryGetPayload<string>(out var payload), Is.True);
        Assert.That(payload, Is.Null);
        Assert.Throws<InvalidOperationException>(() => result.GetPayload<int>());
    }
}
