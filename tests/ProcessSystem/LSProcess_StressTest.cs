using System;
using System.Diagnostics;
using System.Text;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Compares LSProcess pipeline overhead against direct and event-based dispatch.
///
/// Metrics per benchmark:
///   • elapsed ms     — wall-clock time for all iterations
///   • ops/s          — throughput
///   • alloc KB       — heap bytes allocated (GC.GetTotalAllocatedBytes delta)
///   • GC gen-0       — number of gen-0 collections triggered
///   • overhead       — ratio relative to the declared baseline (lower = less overhead)
///
/// Run individually:
///   dotnet test --filter "Category=StressTest" --verbosity normal
/// </summary>
[TestFixture]
[Category("StressTest")]
public class LSProcess_StressTest {

    // ── tuning ─────────────────────────────────────────────────────────────────
    private const int WARMUP     =    500;
    private const int ITERATIONS = 10_000;
    private const int HIGH_VOL   = 100_000;

    // Static field — prevents dead-code elimination without adding synchronisation cost.
    // Reset in SetUp so tests are independent.
    private static int _sink;

    private LSProcessManager _manager = null!;

    [SetUp]
    public void SetUp() {
        _manager = new LSProcessManager();
        _sink = 0;
    }

    // ── process types ──────────────────────────────────────────────────────────

    /// One handler defined via protected override (no manager registration needed).
    private class SingleHandlerProcess : LSProcess {
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder b) =>
            b.Handler("work", _ => { _sink++; return LSProcessResultStatus.SUCCESS; });
    }

    /// Three handlers in a Sequence defined via protected override.
    private class TripleSeqProcess : LSProcess {
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder b) =>
            b.Sequence("seq", seq => seq
                .Handler("h1", _ => { _sink++; return LSProcessResultStatus.SUCCESS; })
                .Handler("h2", _ => { _sink++; return LSProcessResultStatus.SUCCESS; })
                .Handler("h3", _ => { _sink++; return LSProcessResultStatus.SUCCESS; }));
    }

    /// No built-in handlers — executes whatever is registered in the manager.
    private class RegisteredProcess : LSProcess { }

    // ── benchmark infrastructure ───────────────────────────────────────────────

    private readonly record struct BenchResult(
        long ElapsedMs,
        long OpsPerSec,
        long AllocatedBytes,
        int  GCGen0);

    private static BenchResult Measure(int iterations, Action body) {
        // Bring GC to a clean state before measuring allocations.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        long startAlloc = GC.GetTotalAllocatedBytes(precise: false);
        int  startGC    = GC.CollectionCount(0);
        var  sw         = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++) body();

        sw.Stop();
        return new BenchResult(
            sw.ElapsedMilliseconds,
            sw.ElapsedMilliseconds > 0 ? (long)(iterations / sw.Elapsed.TotalSeconds) : long.MaxValue,
            GC.GetTotalAllocatedBytes(precise: false) - startAlloc,
            GC.CollectionCount(0) - startGC);
    }

    private static void Warmup(int n, Action body) {
        for (int i = 0; i < n; i++) body();
    }

    private static string TableHeader() =>
        $"  {"Approach",-42} | {"ms",8} | {"ops/s",14} | {"alloc KB",10} | {"GC g0",6} | {"overhead",9}";

    private static string TableRow(string name, BenchResult r, long baseOps) {
        double x  = baseOps > 0 ? (double)baseOps / r.OpsPerSec : 1.0;
        string xs = x < 10_000 ? $"{x:F2}x" : ">10000x";
        return $"  {name,-42} | {r.ElapsedMs,8} | {r.OpsPerSec,14:N0} | " +
               $"{r.AllocatedBytes / 1024.0,10:F1} | {r.GCGen0,6} | {xs,9}";
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Single-operation dispatch comparison.
    ///
    /// Ladder:
    ///   1. Direct field write          — absolute baseline, zero abstraction
    ///   2. Action delegate (1 sub)     — single-level indirection
    ///   3. EventHandler (2 subs)       — typical C# event broadcast
    ///   4. LSProcess override, ALL     — override handler, full context merge
    ///   5. LSProcess override, LOCAL   — override handler, skip global lookup
    ///   6. LSProcess global-registered — handler from manager registry, ALL mode
    /// </summary>
    [Test]
    public void SingleDispatch_Overhead_Comparison() {
        int n = ITERATIONS;

        // 1 – direct
        Warmup(WARMUP, () => _sink++);
        var direct = Measure(n, () => _sink++);

        // 2 – Action delegate
        Action action = () => _sink++;
        Warmup(WARMUP, () => action());
        var actionOne = Measure(n, () => action());

        // 3 – EventHandler, 2 subscribers
        EventHandler evt2 = null!;
        evt2 += (_, _) => _sink++;
        evt2 += (_, _) => _sink++;
        Warmup(WARMUP, () => evt2.Invoke(this, EventArgs.Empty));
        var eventTwo = Measure(n, () => evt2.Invoke(this, EventArgs.Empty));

        // 4 – LSProcess override, ALL context mode
        Warmup(WARMUP, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));
        var lsOverrideAll = Measure(n, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));

        // 5 – LSProcess override, LOCAL only (no global context lookup)
        Warmup(WARMUP, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.LOCAL));
        var lsOverrideLocal = Measure(n, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.LOCAL));

        // 6 – LSProcess with globally registered handler
        _manager.Register<RegisteredProcess>(b =>
            b.Handler("global-work", _ => { _sink++; return LSProcessResultStatus.SUCCESS; }));
        Warmup(WARMUP, () =>
            new RegisteredProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));
        var lsGlobal = Measure(n, () =>
            new RegisteredProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));

        long baseline = direct.OpsPerSec;
        var  sb       = new StringBuilder();
        sb.AppendLine()
          .AppendLine($"╔═══ Single-Dispatch Overhead  ({n:N0} iterations) ═══╗")
          .AppendLine(TableHeader())
          .AppendLine(new string('─', 114))
          .AppendLine(TableRow("Direct increment",                     direct,          baseline))
          .AppendLine(TableRow("Action delegate  (1 sub)",             actionOne,       baseline))
          .AppendLine(TableRow("EventHandler     (2 subs)",            eventTwo,        baseline))
          .AppendLine(TableRow("LSProcess override  — ALL mode",       lsOverrideAll,   baseline))
          .AppendLine(TableRow("LSProcess override  — LOCAL mode",     lsOverrideLocal, baseline))
          .AppendLine(TableRow("LSProcess global-registered — ALL",    lsGlobal,        baseline))
          .AppendLine($"  _sink = {_sink}");

        TestContext.Out.WriteLine(sb);
        Assert.Pass(sb.ToString());
    }

    /// <summary>
    /// Multi-handler dispatch: EventHandler with 3 subscribers vs LSProcess Sequence with 3 handlers.
    ///
    /// Reveals the per-node routing cost inside the behaviour-tree chain compared to
    /// a flat multicast delegate invocation.
    /// </summary>
    [Test]
    public void MultiHandler_Sequence_Comparison() {
        int n = ITERATIONS;

        // 1 – EventHandler, 3 subscribers (baseline for this section)
        EventHandler evt3 = null!;
        evt3 += (_, _) => _sink++;
        evt3 += (_, _) => _sink++;
        evt3 += (_, _) => _sink++;
        Warmup(WARMUP, () => evt3.Invoke(this, EventArgs.Empty));
        var eventThree = Measure(n, () => evt3.Invoke(this, EventArgs.Empty));

        // 2 – LSProcess Sequence, 3 handlers via override
        Warmup(WARMUP, () =>
            new TripleSeqProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));
        var lsSeqOverride = Measure(n, () =>
            new TripleSeqProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));

        // 3 – LSProcess Sequence, 3 handlers registered globally
        _manager.Register<RegisteredProcess>(b => b
            .Sequence("seq", seq => seq
                .Handler("g1", _ => { _sink++; return LSProcessResultStatus.SUCCESS; })
                .Handler("g2", _ => { _sink++; return LSProcessResultStatus.SUCCESS; })
                .Handler("g3", _ => { _sink++; return LSProcessResultStatus.SUCCESS; })));
        Warmup(WARMUP, () =>
            new RegisteredProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));
        var lsSeqGlobal = Measure(n, () =>
            new RegisteredProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));

        long baseline = eventThree.OpsPerSec;
        var  sb       = new StringBuilder();
        sb.AppendLine()
          .AppendLine($"╔═══ Multi-Handler Sequence  ({n:N0} iterations) ════╗")
          .AppendLine(TableHeader())
          .AppendLine(new string('─', 114))
          .AppendLine(TableRow("EventHandler       (3 subs)",           eventThree,    baseline))
          .AppendLine(TableRow("LSProcess Sequence (3 handlers, override)", lsSeqOverride, baseline))
          .AppendLine(TableRow("LSProcess Sequence (3 handlers, global)",   lsSeqGlobal,   baseline))
          .AppendLine($"  _sink = {_sink}");

        TestContext.Out.WriteLine(sb);
        Assert.Pass(sb.ToString());
    }

    /// <summary>
    /// 100 k LSProcess executions — absolute throughput ceiling and per-op allocation cost.
    /// </summary>
    [Test]
    public void HighVolume_LSProcess_Throughput() {
        int n = HIGH_VOL;
        Warmup(WARMUP, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));
        var r = Measure(n, () =>
            new SingleHandlerProcess().Execute(_manager, LSProcessManager.LSProcessContextMode.ALL));

        var sb = new StringBuilder();
        sb.AppendLine()
          .AppendLine($"╔═══ High-Volume LSProcess  ({n:N0} iterations) ══════════╗")
          .AppendLine($"  Total time   : {r.ElapsedMs} ms")
          .AppendLine($"  Throughput   : {r.OpsPerSec:N0} ops/s")
          .AppendLine($"  Avg latency  : {(r.ElapsedMs > 0 ? r.ElapsedMs * 1000.0 / n : 0):F2} µs / op")
          .AppendLine($"  Total alloc  : {r.AllocatedBytes / 1024.0:F1} KB")
          .AppendLine($"  Alloc / op   : {r.AllocatedBytes / (double)n:F0} bytes")
          .AppendLine($"  GC Gen-0     : {r.GCGen0} collections");

        TestContext.Out.WriteLine(sb);
        Assert.Pass(sb.ToString());
    }

    /// <summary>
    /// 100 k EventHandler invocations — reference throughput for the LSProcess high-volume test.
    /// </summary>
    [Test]
    public void HighVolume_EventHandler_Throughput() {
        int n = HIGH_VOL;
        EventHandler evt = null!;
        evt += (_, _) => _sink++;
        Warmup(WARMUP, () => evt.Invoke(this, EventArgs.Empty));
        var r = Measure(n, () => evt.Invoke(this, EventArgs.Empty));

        var sb = new StringBuilder();
        sb.AppendLine()
          .AppendLine($"╔═══ High-Volume EventHandler  ({n:N0} iterations) ═══════════╗")
          .AppendLine($"  Total time   : {r.ElapsedMs} ms")
          .AppendLine($"  Throughput   : {r.OpsPerSec:N0} ops/s")
          .AppendLine($"  Avg latency  : {(r.ElapsedMs > 0 ? r.ElapsedMs * 1000.0 / n : 0):F2} µs / op")
          .AppendLine($"  Total alloc  : {r.AllocatedBytes / 1024.0:F1} KB")
          .AppendLine($"  Alloc / op   : {r.AllocatedBytes / (double)n:F0} bytes")
          .AppendLine($"  GC Gen-0     : {r.GCGen0} collections")
          .AppendLine($"  _sink = {_sink}");

        TestContext.Out.WriteLine(sb);
        Assert.Pass(sb.ToString());
    }
}
