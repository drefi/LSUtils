using System;
using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessExecutionTests {
    private static IEnumerable<TestCaseData> ContinuationCases() {
        foreach (var shape in new[] { "sequence", "selector", "inverter", "nested" }) {
            foreach (var status in new[] { LSProcessResultStatus.SUCCESS, LSProcessResultStatus.FAILURE,
                LSProcessResultStatus.WAITING, LSProcessResultStatus.CANCELLED }) {
                foreach (var control in status == LSProcessResultStatus.WAITING
                    ? new[] { "resume", "fail", "cancel" } : new[] { "none" }) {
                    yield return new TestCaseData(shape, status, control);
                }
            }
        }
    }

    private static ILSProcessLayerNode Build(string shape, LSProcessResultStatus status, List<string> trace) {
        var root = LSProcessManager.CreateRootNode("root");
        var builder = new LSProcessTreeBuilder(root);
        LSProcessHandler first = _ => { trace.Add("first"); return status; };
        LSProcessHandler next = _ => { trace.Add("next"); return LSProcessResultStatus.SUCCESS; };
        switch (shape) {
            case "sequence": builder.Sequence("test", b => b.Handler("first", first).Handler("next", next)); break;
            case "selector": builder.Selector("test", b => b.Handler("first", first).Handler("next", next)); break;
            case "inverter": builder.Inverter("test", b => b.Handler("first", first)); break;
            default: builder.Selector("test", b => b
                .Sequence("path", s => s.Inverter("inverse", i => i.Handler("first", first)))
                .Handler("next", next)); break;
        }
        builder.Handler("tail", _ => { trace.Add("tail"); return LSProcessResultStatus.SUCCESS; });
        return root;
    }

    [TestCaseSource(nameof(ContinuationCases))]
    public void ContinuationPreservesValidatedContract(string shape, LSProcessResultStatus status, string control) {
        var trace = new List<string>();
        var newRoot = Build(shape, status, trace);
        var manager = new LSProcessManager();
        var session = new LSProcessSession(manager, new PipelineTestProcess(), newRoot,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        // This matrix was compared against the original executor before its removal.
        var initialExpected = ExpectedOutcome(shape, status);
        Assert.That(session.Execute(), Is.EqualTo(initialExpected));
        var effective = control switch {
            "resume" => LSProcessResultStatus.SUCCESS,
            "fail" => LSProcessResultStatus.FAILURE,
            "cancel" => LSProcessResultStatus.CANCELLED,
            _ => status
        };
        var expected = ExpectedOutcome(shape, effective);
        if (control != "none") {
            var actual = control == "resume" ? session.Resume()
                : control == "fail" ? session.Fail() : session.Cancel();
            Assert.That(actual, Is.EqualTo(expected));
        }
        var expectedTrace = new List<string> { "first" };
        if ((shape == "sequence" || shape == "nested") && effective == LSProcessResultStatus.SUCCESS ||
            shape == "selector" && effective == LSProcessResultStatus.FAILURE) expectedTrace.Add("next");
        if (expected == LSProcessResultStatus.SUCCESS) expectedTrace.Add("tail");
        Assert.That(session.RootNode.Status, Is.EqualTo(expected));
        Assert.That(session.Execute(), Is.EqualTo(expected));
        Assert.That(trace, Is.EqualTo(expectedTrace));
    }

    private static LSProcessResultStatus ExpectedOutcome(string shape, LSProcessResultStatus status) {
        if (status is LSProcessResultStatus.WAITING or LSProcessResultStatus.CANCELLED) return status;
        return shape switch {
            "sequence" => status,
            "inverter" => status == LSProcessResultStatus.SUCCESS
                ? LSProcessResultStatus.FAILURE : LSProcessResultStatus.SUCCESS,
            _ => LSProcessResultStatus.SUCCESS
        };
    }

    [Test]
    public void SharedDefinition_HasIndependentStatusesAndCounters() {
        var root = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(root).Handler("wait", _ => LSProcessResultStatus.WAITING);
        var definition = LSProcessDefinition.Compile(root);
        var manager = new LSProcessManager();
        var one = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        var two = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
        Assert.That(one.Execute(), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResultStatus.UNKNOWN));
        Assert.That(two.RootNode.GetChild("wait")!.ExecutionCount, Is.Zero);
        two.Execute();
        one.Resume();
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResultStatus.WAITING));
        two.Fail();
        Assert.That(one.RootNode.Status, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(two.RootNode.Status, Is.EqualTo(LSProcessResultStatus.FAILURE));
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
            return LSProcessResultStatus.WAITING;
        };
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("capture", s => {
            untyped = s; return LSProcessResultStatus.SUCCESS;
        }).Handler("wait", wait.ToHandler()).Handler("tail", _ => {
            completions++; return LSProcessResultStatus.SUCCESS;
        }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(typed!.SessionID, Is.EqualTo(untyped!.SessionID));
        Assert.That(typed.RootNode, Is.SameAs(untyped.RootNode));
        Assert.That(typed.CurrentNode, Is.Null);
        Assert.That(typed.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
            return b.Handler("wait", _ => LSProcessResultStatus.WAITING)
                .Sequence("future", s => s.Handler("original", _ => {
                    trace.Add("original"); return LSProcessResultStatus.SUCCESS;
                }, conditions: conditions));
        });
        process.Execute(new LSProcessManager());
        conditions[0] = _ => false;
        retained!.Sequence("future", s => s.Handler("injected", _ => {
            trace.Add("injected"); return LSProcessResultStatus.SUCCESS;
        }));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
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
        process.WithProcessing(b => b.Handler("wait", _ => LSProcessResultStatus.WAITING)
            .Sequence("future", s => s.Handler("conditional", _ => {
                calls++; return LSProcessResultStatus.SUCCESS;
            }, conditions: _ => { conditionCalls++; return allowed; })));
        process.Execute(new LSProcessManager());
        Assert.That(conditionCalls, Is.Zero);
        allowed = true;
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(conditionCalls, Is.EqualTo(1));
    }

    [Test]
    public void CancelFromHandler_PreventsTailAndCannotBeOverwrittenByReturnValue() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("cancel", s => {
            s.Cancel(); return LSProcessResultStatus.SUCCESS;
        }).Handler("tail", _ => { calls++; return LSProcessResultStatus.SUCCESS; }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void ConditionsCanCancelWithoutInvokingPendingHandlers() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("guarded", _ => {
            calls++; return LSProcessResultStatus.SUCCESS;
        }, conditions: p => { p.Cancel(); return true; }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResultStatus.CANCELLED));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void UnknownResultIsNotRepeatedOrReportedAsSuccess() {
        var calls = 0;
        var process = new PipelineTestProcess();
        process.WithProcessing(b => b.Handler("unknown", _ => {
            calls++; return LSProcessResultStatus.UNKNOWN;
        }));
        Assert.That(process.Execute(new LSProcessManager()), Is.EqualTo(LSProcessResultStatus.UNKNOWN));
        Assert.That(process.Execute(), Is.EqualTo(LSProcessResultStatus.UNKNOWN));
        Assert.That(process.IsCompleted, Is.False);
        Assert.That(calls, Is.EqualTo(1));
    }
}
