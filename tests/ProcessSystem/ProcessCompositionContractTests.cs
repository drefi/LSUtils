using System.Collections.Generic;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture]
public class ProcessCompositionContractTests {
    private sealed class ComposedProcess : LSProcess {
        public List<string> Trace { get; } = new();
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder b) => b
            .Handler("builtin", s => Record(s, "builtin"));
    }

    private static LSProcessResultStatus Record(LSProcessSession s, string value) {
        ((ComposedProcess)s.Process).Trace.Add(value);
        return LSProcessResultStatus.SUCCESS;
    }

    [TestCase(LSProcessManager.LSProcessContextMode.LOCAL, "local,builtin")]
    [TestCase(LSProcessManager.LSProcessContextMode.GLOBAL, "local,builtin,global")]
    [TestCase(LSProcessManager.LSProcessContextMode.MATCH_FIRST, "local,builtin,one")]
    [TestCase(LSProcessManager.LSProcessContextMode.ALL_INSTANCES, "local,builtin,one,two")]
    [TestCase(LSProcessManager.LSProcessContextMode.ANY, "local,builtin,one,global")]
    [TestCase(LSProcessManager.LSProcessContextMode.ALL, "local,builtin,one,two,global")]
    public void AllSources_PreserveCompositionOrderAndContextSelection(
        LSProcessManager.LSProcessContextMode mode, string expected) {
        var manager = new LSProcessManager();
        var one = new TestProcessable();
        var two = new TestProcessable();
        manager.Register<ComposedProcess>(b => b.Handler("one", s => Record(s, "one")), one);
        manager.Register<ComposedProcess>(b => b.Handler("two", s => Record(s, "two")), two);
        manager.Register<ComposedProcess>(b => b.Handler("global", s => Record(s, "global")));
        var process = new ComposedProcess();
        process.WithProcessing(b => b.Handler("local", s => Record(s, "local")));

        Assert.That(process.Execute(manager, mode, new TestProcessable(), one, two),
            Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(string.Join(",", process.Trace), Is.EqualTo(expected));
    }

    [TestCase(NodeUpdatePolicy.DEFAULT_HANDLER, "global")]
    [TestCase(NodeUpdatePolicy.READONLY, "local")]
    public void SameId_GlobalReplacementRespectsProtectedLocalHandler(NodeUpdatePolicy policy, string expected) {
        var manager = new LSProcessManager();
        var actor = new TestProcessable();
        manager.Register<ComposedProcess>(b => b.Handler("builtin", s => Record(s, "instance")), actor);
        manager.Register<ComposedProcess>(b => b.Handler("builtin", s => Record(s, "global")));
        var process = new ComposedProcess();
        process.WithProcessing(b => b.Handler("builtin", s => Record(s, "local"), policy));

        Assert.That(process.Execute(manager, LSProcessManager.LSProcessContextMode.ALL, actor),
            Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.Trace, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void ManagerChangesDuringWait_OnlyAffectNextExecution() {
        var manager = new LSProcessManager();
        manager.Register<ComposedProcess>(b => b
            .Handler("wait", _ => LSProcessResultStatus.WAITING)
            .Handler("result", s => Record(s, "original")));
        var first = new ComposedProcess();
        Assert.That(first.Execute(manager), Is.EqualTo(LSProcessResultStatus.WAITING));
        manager.Register<ComposedProcess>(b => b.Handler("result", s => Record(s, "replacement")));
        var second = new ComposedProcess();
        Assert.That(second.Execute(manager), Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(second.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(first.Resume(), Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(first.Trace, Is.EqualTo(new[] { "builtin", "original" }));
        Assert.That(second.Trace, Is.EqualTo(new[] { "builtin", "replacement" }));
    }
}
