using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessInterventionTests {
    private sealed class RequestProcess : LSProcess { }

    [Test]
    public void RegisteredParticipants_CanModifyAndObserveSharedResultInOrder() {
        var manager = new LSProcessManager();
        var observed = new List<int>();
        manager.Register<RequestProcess>(b => b.Handler("resolve", s => {
            s.Process.SetData("result", 10);
            return LSProcessResultStatus.SUCCESS;
        }));
        manager.Register<RequestProcess>(b => b.Handler("adjust", s => {
            s.Process.SetData("result", s.Process.GetData<int>("result") + 2);
            return LSProcessResultStatus.SUCCESS;
        }));
        manager.Register<RequestProcess>(b => b.Handler("observe", s => {
            observed.Add(s.Process.GetData<int>("result"));
            return LSProcessResultStatus.SUCCESS;
        }));
        manager.Register<RequestProcess>(b => b.Handler("another-observer", s => {
            observed.Add(s.Process.GetData<int>("result"));
            return LSProcessResultStatus.SUCCESS;
        }));

        var process = new RequestProcess();
        Assert.That(process.Execute(manager), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.Execute(manager), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(observed, Is.EqualTo(new[] { 12, 12 }));
    }

    [TestCase(true, LSProcessResultStatus.SUCCESS)]
    [TestCase(false, LSProcessResultStatus.FAILURE)]
    public void RegisteredGate_WaitsForExternalAnswer_ThenValidatesBeforeCompletion(
        bool approved, LSProcessResultStatus expected) {
        var manager = new LSProcessManager();
        var waits = 0;
        var completions = 0;
        manager.Register<RequestProcess>(b => b
            .Handler("approval", _ => { waits++; return LSProcessResultStatus.WAITING; })
            .Handler("validate-answer", s => s.Process.GetData<bool>("approved")
                ? LSProcessResultStatus.SUCCESS : LSProcessResultStatus.FAILURE)
            .Handler("completed", _ => { completions++; return LSProcessResultStatus.SUCCESS; }));

        var process = new RequestProcess();
        Assert.That(process.Execute(manager), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(process.Execute(manager), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(completions, Is.Zero);
        process.SetData("approved", approved);
        Assert.That(process.Resume(), Is.EqualTo(expected));
        Assert.That(process.IsCompleted, Is.True);
        Assert.That(waits, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(approved ? 1 : 0));
    }

    [Test]
    public void RegisteredVeto_PreventsActionAndSuccessObservers() {
        var manager = new LSProcessManager();
        var trace = new List<string>();
        manager.Register<RequestProcess>(b => b.Handler("validate", s => {
            s.Process.SetData("reason", "not-allowed");
            return LSProcessResultStatus.FAILURE;
        }));
        manager.Register<RequestProcess>(b => b
            .Handler("apply", _ => { trace.Add("apply"); return LSProcessResultStatus.SUCCESS; })
            .Handler("completed", _ => { trace.Add("completed"); return LSProcessResultStatus.SUCCESS; }));

        var process = new RequestProcess();
        Assert.That(process.Execute(manager), Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(process.GetData<string>("reason"), Is.EqualTo("not-allowed"));
        Assert.That(trace, Is.Empty);
    }
}
