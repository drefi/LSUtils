using System;
using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Audit of current behavior. Tests prefixed Currently characterize observations,
/// including defects; they are not specifications endorsing that behavior.
/// </summary>
[TestFixture]
public class ProcessFlowAuditTests {
    private sealed class AuditProcess : LSProcess { }

    private static LSProcessResultStatus Start(LSProcess process) => process.Execute(new LSProcessManager());

    [Test]
    public void NestedSequence_ResumesLocallyAcrossTwoWaits_WithoutRepeatingHandlers() {
        var trace = new List<string>();
        var process = new AuditProcess();
        process.WithProcessing(b => b.Sequence("nested", s => s
            .Handler("first", _ => { trace.Add("first"); return LSProcessResultStatus.WAITING; })
            .Handler("second", _ => { trace.Add("second"); return LSProcessResultStatus.WAITING; }))
            .Handler("tail", _ => { trace.Add("tail"); return LSProcessResultStatus.SUCCESS; }));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.IsCompleted, Is.False);
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(trace, Is.EqualTo(new[] { "first", "second", "tail" }));
    }

    [Test]
    public void Sequence_FailInvertedWait_ContinuesToTail() {
        var tailCalls = 0;
        var process = new AuditProcess();
        process.WithProcessing(b => b.Inverter("inverse", i => i
            .Handler("wait", _ => LSProcessResultStatus.WAITING))
            .Handler("tail", _ => { tailCalls++; return LSProcessResultStatus.SUCCESS; }));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Fail(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(tailCalls, Is.EqualTo(1));
    }

    [Test]
    public void Selector_FailWait_ExecutesFallbackAndParentTailOnce() {
        var trace = new List<string>();
        var process = new AuditProcess();
        process.WithProcessing(b => b.Selector("choice", s => s
            .Handler("wait", _ => { trace.Add("wait"); return LSProcessResultStatus.WAITING; })
            .Handler("fallback", _ => { trace.Add("fallback"); return LSProcessResultStatus.SUCCESS; }))
            .Handler("tail", _ => { trace.Add("tail"); return LSProcessResultStatus.SUCCESS; }));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Fail(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(trace, Is.EqualTo(new[] { "wait", "fallback", "tail" }));
    }

    [Test]
    public void CancelWaitingSequence_WithUnstartedBranch_DoesNotThrowOrExecuteBranch() {
        var pendingCalls = 0;
        var process = new AuditProcess();
        process.WithProcessing(b => b.Handler("wait", _ => LSProcessResultStatus.WAITING)
            .Sequence("pending", s => s.Handler("work", _ => {
                pendingCalls++; return LSProcessResultStatus.SUCCESS;
            })));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.DoesNotThrow(process.Cancel);
        Assert.That(process.IsCancelled, Is.True);
        Assert.That(pendingCalls, Is.Zero);
    }

    [Test]
    public void SelectorResumingLastInvertedWait_ReturnsFailureWithoutExecutingSequenceTail() {
        var tailCalls = 0;
        var process = new AuditProcess();
        process.WithProcessing(b => b.Selector("choice", s => s
            .Inverter("inverse", i => i.Handler("wait", _ => LSProcessResultStatus.WAITING)))
            .Handler("tail", _ => { tailCalls++; return LSProcessResultStatus.SUCCESS; }));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(process.Execute(), Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(tailCalls, Is.Zero);
    }

    [Test]
    public void SelectorExhaustionOnResume_AllowsParentSelectorFallback() {
        var trace = new List<string>();
        var process = new AuditProcess();
        process.WithProcessing(b => b.Selector("outer", outer => outer
            .Selector("inner", inner => inner
                .Inverter("inverse", i => i.Handler("wait", _ => {
                    trace.Add("wait"); return LSProcessResultStatus.WAITING;
                })))
            .Handler("fallback", _ => { trace.Add("fallback"); return LSProcessResultStatus.SUCCESS; }))
            .Handler("tail", _ => { trace.Add("tail"); return LSProcessResultStatus.SUCCESS; }));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(trace, Is.EqualTo(new[] { "wait", "fallback", "tail" }));
    }

    [Test]
    public void WithProcessingIsBlocked_AndRuntimeDoesNotExposeEditableNodes() {
        LSProcessSession? session = null;
        var invoked = false;
        var process = new AuditProcess();
        process.WithProcessing(b => b.Inverter("inverse", i => i.Handler("wait", s => {
            session = s; return LSProcessResultStatus.WAITING;
        })));

        Assert.That(Start(process), Is.EqualTo(LSProcessResultStatus.WAITING));
        process.WithProcessing(b => { invoked = true; return b; });
        Assert.That(invoked, Is.False);
        var inverter = session!.RootNode.GetChild("inverse")!;
        Assert.That(inverter, Is.Not.InstanceOf<ILSProcessLayerNode>());
        Assert.That(inverter.GetChild("wait")!.Status, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Resume(), Is.EqualTo(LSProcessResultStatus.FAILURE));
    }
}
