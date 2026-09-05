using System;
using System.Collections.Generic;
using System.Text.Json;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public sealed class ProcessExecutionMementoTests {
    [Test]
    public void WaitingExecutionRestoresWithoutRepeatingCompletedHandlers() {
        var trace = new List<string>();
        var root = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(root)
            .Handler("before", _ => { trace.Add("before"); return LSProcessResult.Success; })
            .Handler("wait", _ => { trace.Add("wait"); return LSProcessResult.WaitingFor("approval"); })
            .Handler("after", _ => { trace.Add("after"); return LSProcessResult.Success; });
        var definition = LSProcessDefinition.Compile(root);
        var manager = new LSProcessManager();
        var codecs = StringCodecs();
        var original = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);

        Assert.That(original.Execute(), Is.EqualTo(LSProcessResult.Waiting));
        var memento = original.CaptureExecution(codecs);
        var restored = new LSProcessSession(manager, new PipelineTestProcess(), definition,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null, memento, codecs);
        Assert.That(restored.Resume("accepted"), Is.EqualTo(LSProcessResult.Success));

        Assert.Multiple(() => {
            Assert.That(trace, Is.EqualTo(new[] { "before", "wait", "after" }));
            Assert.That(restored.RootNode.GetChild("before")!.ExecutionCount, Is.EqualTo(1));
            Assert.That(restored.RootNode.GetChild("wait")!.Status.GetPayload<string>(), Is.EqualTo("accepted"));
        });
    }

    [Test]
    public void CaptureRejectsPayloadWithoutRegisteredCodec() {
        var session = WaitingSession("root", "wait", LSProcessResult.WaitingFor(42));
        session.Execute();

        Assert.That(
            () => session.CaptureExecution(new LSProcessPayloadCodecRegistry()),
            Throws.InvalidOperationException);
    }

    [Test]
    public void RestoreRejectsDifferentDefinitionAndFormat() {
        var codecs = StringCodecs();
        var original = WaitingSession("root", "wait", LSProcessResult.WaitingFor("approval"));
        original.Execute();
        var memento = original.CaptureExecution(codecs);
        var otherRoot = LSProcessManager.CreateRootNode("root");
        new LSProcessTreeBuilder(otherRoot).Handler("different", _ => LSProcessResult.Waiting);
        var otherDefinition = LSProcessDefinition.Compile(otherRoot);

        Assert.Multiple(() => {
            Assert.That(() => new LSProcessSession(new LSProcessManager(), new PipelineTestProcess(),
                otherDefinition, LSProcessManager.LSProcessContextMode.LOCAL, null, null, memento, codecs),
                Throws.InvalidOperationException);
            Assert.That(() => new LSProcessSession(original.Manager, new PipelineTestProcess(),
                original.Definition, LSProcessManager.LSProcessContextMode.LOCAL, null, null,
                memento with { FormatVersion = 99 }, codecs), Throws.TypeOf<NotSupportedException>());
        });
    }

    [Test]
    public void RestoreRejectsStructurallyInvalidNodeState() {
        var codecs = StringCodecs();
        var original = WaitingSession("root", "wait", LSProcessResult.WaitingFor("approval"));
        original.Execute();
        var memento = original.CaptureExecution(codecs);
        var invalidRoot = memento.Root with { Cursor = -1 };

        Assert.That(() => new LSProcessSession(original.Manager, new PipelineTestProcess(),
            original.Definition, LSProcessManager.LSProcessContextMode.LOCAL, null, null,
            memento with { Root = invalidRoot }, codecs), Throws.InvalidOperationException);
    }

    [Test]
    public void EquivalentDefinitionsHaveStableFingerprint() {
        var first = WaitingSession("root", "wait", LSProcessResult.Waiting);
        var second = WaitingSession("root", "wait", LSProcessResult.Waiting);
        var different = WaitingSession("root", "other", LSProcessResult.Waiting);

        Assert.Multiple(() => {
            Assert.That(first.Definition.Fingerprint, Is.EqualTo(second.Definition.Fingerprint));
            Assert.That(first.Definition.Fingerprint, Is.Not.EqualTo(different.Definition.Fingerprint));
        });
    }

    [Test]
    public void ProcessApiRestoresItsOwnWaitingExecution() {
        var completed = 0;
        var original = Process(() => completed++);
        Assert.That(original.Execute(new LSProcessManager(), LSProcessManager.LSProcessContextMode.LOCAL),
            Is.EqualTo(LSProcessResult.Waiting));
        var codecs = StringCodecs();
        var memento = original.CaptureExecution(codecs);

        var restored = Process(() => completed++);
        restored.RestoreExecution(memento, codecs, new LSProcessManager(),
            LSProcessManager.LSProcessContextMode.LOCAL);

        Assert.Multiple(() => {
            Assert.That(restored.Resume(), Is.EqualTo(LSProcessResult.Success));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(restored.ID, Is.EqualTo(original.ID));
            Assert.That(restored.CreatedAt, Is.EqualTo(original.CreatedAt));
        });
    }

    [Test]
    public void ExecutionMementoSurvivesJsonRoundTrip() {
        var process = Process(() => { });
        process.Execute(new LSProcessManager(), LSProcessManager.LSProcessContextMode.LOCAL);
        var codecs = StringCodecs();
        var original = process.CaptureExecution(codecs);

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<LSProcessExecutionMemento>(json);

        Assert.Multiple(() => {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.ProcessId, Is.EqualTo(original.ProcessId));
            Assert.That(restored.DefinitionFingerprint, Is.EqualTo(original.DefinitionFingerprint));
            Assert.That(restored.Root.Children[0].Status.Payload, Is.EqualTo(original.Root.Children[0].Status.Payload));
        });
    }

    private static LSProcessSession WaitingSession(string rootId, string handlerId, LSProcessResult result) {
        var root = LSProcessManager.CreateRootNode(rootId);
        new LSProcessTreeBuilder(root).Handler(handlerId, _ => result);
        return new LSProcessSession(new LSProcessManager(), new PipelineTestProcess(), root,
            LSProcessManager.LSProcessContextMode.LOCAL, null, null);
    }

    private static LSProcessPayloadCodecRegistry StringCodecs() =>
        new LSProcessPayloadCodecRegistry().Register<string>("system.string", 1, value => value, value => value);

    private static PipelineTestProcess Process(Action complete) {
        var process = new PipelineTestProcess();
        process.WithProcessing(builder => builder
            .Handler("wait", _ => LSProcessResult.WaitingFor("approval"))
            .Handler("complete", _ => { complete(); return LSProcessResult.Success; }));
        return process;
    }
}
