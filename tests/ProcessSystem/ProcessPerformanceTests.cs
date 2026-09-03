using System;
using System.Diagnostics;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

[TestFixture, NonParallelizable]
public class ProcessPerformanceTests {
    [Test, Explicit("Diagnostic measurement; no timing threshold in the regression suite.")]
    public void MeasureConstructionAndContinuation() {
        var manager = new LSProcessManager();
        manager.Register<PipelineTestProcess>(b => {
            for (var i = 0; i < 32; i++) {
                var waiting = i == 16;
                b.Handler("step-" + i, _ => waiting ? LSProcessResultStatus.WAITING : LSProcessResultStatus.SUCCESS);
            }
            return b;
        });
        for (var i = 0; i < 30; i++) { var warmup = new PipelineTestProcess(); warmup.Execute(manager); warmup.Resume(); }
        const int count = 500;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < count; i++) {
            var process = new PipelineTestProcess();
            process.Execute(manager);
            process.Resume();
        }
        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"Construct+execute+resume: {count} x 32 nodes, {watch.Elapsed.TotalMilliseconds:F2} ms, {allocated / count} bytes/operation");
    }
}
