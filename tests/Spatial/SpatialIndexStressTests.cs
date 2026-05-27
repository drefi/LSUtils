namespace LSUtils.Tests.Spatial;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LSUtils.Spatial;
using NUnit.Framework;

/// <summary>
/// Stress tests and benchmark-style comparisons for the spatial index implementations.
///
/// Run individually:
///   dotnet test --filter "FullyQualifiedName~SpatialIndexStressTests" --verbosity normal
///   dotnet test --filter "Category=StressTest" --verbosity normal
/// </summary>
[TestFixture]
[Category("StressTest")]
public class SpatialIndexStressTests {
    private const int WarmupIterations = 2;
    private const int LargeDatasetSize = 10_000;
    private const int QueryCount = 2_000;
    private const int UpdateCount = 3_000;
    private const int RemoveCount = 2_500;
    private const float WorldSize = 4_096f;
    private const float CellSize = 32f;
    private static readonly Bounds WorldBounds = new(0, 0, WorldSize, WorldSize);

    private static readonly Func<ISpatialIndex<int>> QuadTreeFactory =
        () => new QuadTree<int>(WorldBounds, capacity: 8);

    private static readonly Func<ISpatialIndex<int>> SpatialHashGridFactory =
        () => new SpatialHashGrid<int>(CellSize);

    private readonly record struct IndexedItem(int Id, Bounds Bounds);

    private readonly record struct BenchResult(
        long ElapsedMs,
        long OpsPerSec,
        long AllocatedBytes,
        int GCGen0,
        int ResultChecksum);

    [Test]
    public void UniformDistribution_StressComparison() {
        var items = CreateUniformItems(LargeDatasetSize);
        var queries = CreateQueryAreas(QueryCount, areaWidth: 96, areaHeight: 96, seed: 101);
        var updatedBounds = CreateUpdatedBounds(items, maxDelta: 24, seed: 202);

        var quadTree = RunScenario("QuadTree", QuadTreeFactory, items, queries, updatedBounds, RemoveCount);
        var grid = RunScenario("SpatialHashGrid", SpatialHashGridFactory, items, queries, updatedBounds, RemoveCount);

        AssertScenarioConsistency(quadTree, grid, RemoveCount);
        Assert.Pass(BuildComparisonTable(
            "Uniform spatial workload",
            items.Length,
            queries.Length,
            UpdateCount,
            RemoveCount,
            quadTree,
            grid));
    }

    [Test]
    public void ClusteredDistribution_StressComparison() {
        var items = CreateClusteredItems(LargeDatasetSize);
        var queries = CreateClusteredQueryAreas(QueryCount, areaWidth: 128, areaHeight: 128);
        var updatedBounds = CreateUpdatedBounds(items, maxDelta: 18, seed: 303);

        var quadTree = RunScenario("QuadTree", QuadTreeFactory, items, queries, updatedBounds, RemoveCount);
        var grid = RunScenario("SpatialHashGrid", SpatialHashGridFactory, items, queries, updatedBounds, RemoveCount);

        AssertScenarioConsistency(quadTree, grid, RemoveCount);
        Assert.Pass(BuildComparisonTable(
            "Clustered spatial workload",
            items.Length,
            queries.Length,
            UpdateCount,
            RemoveCount,
            quadTree,
            grid));
    }

    [Test]
    public void RepeatedUpdateAndQuery_PreservesConsistency() {
        var items = CreateUniformItems(2_500);
        var quadTree = QuadTreeFactory();
        var grid = SpatialHashGridFactory();

        SeedIndex(quadTree, items);
        SeedIndex(grid, items);

        var currentBounds = items.ToDictionary(item => item.Id, item => item.Bounds);
        var random = new Random(404);

        for (int step = 0; step < 250; step++) {
            int id = step % items.Length;
            Bounds oldBounds = currentBounds[id];
            Bounds newBounds = NudgeBounds(oldBounds, random, maxDelta: 20);

            bool quadUpdated = quadTree.Update(id, oldBounds, newBounds);
            bool gridUpdated = grid.Update(id, oldBounds, newBounds);

            Assert.That(quadUpdated, Is.True);
            Assert.That(gridUpdated, Is.True);
            currentBounds[id] = newBounds;

            Bounds probe = new Bounds(newBounds.X, newBounds.Y, 80, 80);
            var quadResults = quadTree.Query(probe).OrderBy(value => value).ToArray();
            var gridResults = grid.Query(probe).OrderBy(value => value).ToArray();

            Assert.That(gridResults, Is.EqualTo(quadResults), $"Mismatch after step {step}");
        }
    }

    private static ScenarioResult RunScenario(
        string name,
        Func<ISpatialIndex<int>> factory,
        IndexedItem[] items,
        Bounds[] queries,
        Bounds[] updatedBounds,
        int removeCount) {

        Warmup(factory, items, queries, updatedBounds, Math.Min(removeCount, 64));

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        long startAlloc = GC.GetTotalAllocatedBytes(precise: false);
        int startGC = GC.CollectionCount(0);
        var stopwatch = Stopwatch.StartNew();

        ISpatialIndex<int> index = factory();
        SeedIndex(index, items);

        int queryChecksumBefore = ExecuteQueries(index, queries);
        int updatedCount = ExecuteUpdates(index, items, updatedBounds);
        int queryChecksumAfter = ExecuteQueries(index, queries);
        int removedCount = ExecuteRemovals(index, items, removeCount);

        stopwatch.Stop();

        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - startAlloc;
        int gcGen0 = GC.CollectionCount(0) - startGC;
        int totalOperations = items.Length + queries.Length + UpdateCount + queries.Length + removeCount;
        long opsPerSec = stopwatch.ElapsedMilliseconds > 0
            ? (long)(totalOperations / stopwatch.Elapsed.TotalSeconds)
            : long.MaxValue;

        return new ScenarioResult(
            name,
            new BenchResult(
                stopwatch.ElapsedMilliseconds,
                opsPerSec,
                allocatedBytes,
                gcGen0,
                queryChecksumBefore ^ queryChecksumAfter),
            index.Count,
            updatedCount,
            removedCount,
            queryChecksumBefore,
            queryChecksumAfter);
    }

    private static void Warmup(
        Func<ISpatialIndex<int>> factory,
        IndexedItem[] items,
        Bounds[] queries,
        Bounds[] updatedBounds,
        int removeCount) {

        for (int i = 0; i < WarmupIterations; i++) {
            ISpatialIndex<int> index = factory();
            SeedIndex(index, items.Take(256).ToArray());
            ExecuteQueries(index, queries.Take(64).ToArray());
            ExecuteUpdates(index, items.Take(64).ToArray(), updatedBounds.Take(64).ToArray());
            ExecuteRemovals(index, items.Take(removeCount).ToArray(), Math.Min(removeCount, 32));
        }
    }

    private static void SeedIndex(ISpatialIndex<int> index, IndexedItem[] items) {
        foreach (var item in items) {
            bool inserted = index.Insert(item.Id, item.Bounds);
            Assert.That(inserted, Is.True, $"Failed to insert item {item.Id}");
        }
    }

    private static int ExecuteQueries(ISpatialIndex<int> index, Bounds[] queries) {
        int checksum = 0;
        foreach (var query in queries) {
            var results = index.Query(query).OrderBy(item => item);
            checksum ^= results.Count();
            foreach (var item in results) {
                checksum = unchecked((checksum * 397) ^ item);
            }
        }

        return checksum;
    }

    private static int ExecuteUpdates(ISpatialIndex<int> index, IndexedItem[] items, Bounds[] updatedBounds) {
        int updated = 0;
        int updateCount = Math.Min(UpdateCount, Math.Min(items.Length, updatedBounds.Length));
        for (int i = 0; i < updateCount; i++) {
            bool ok = index.Update(items[i].Id, items[i].Bounds, updatedBounds[i]);
            Assert.That(ok, Is.True, $"Failed to update item {items[i].Id}");
            updated++;
        }

        return updated;
    }

    private static int ExecuteRemovals(ISpatialIndex<int> index, IndexedItem[] items, int removeCount) {
        int removed = 0;
        for (int i = 0; i < removeCount; i++) {
            bool ok = index.Remove(items[i].Id);
            Assert.That(ok, Is.True, $"Failed to remove item {items[i].Id}");
            removed++;
        }

        return removed;
    }

    private static void AssertScenarioConsistency(ScenarioResult quadTree, ScenarioResult grid, int removeCount) {
        Assert.That(quadTree.UpdatedCount, Is.EqualTo(UpdateCount));
        Assert.That(grid.UpdatedCount, Is.EqualTo(UpdateCount));
        Assert.That(quadTree.RemovedCount, Is.EqualTo(removeCount));
        Assert.That(grid.RemovedCount, Is.EqualTo(removeCount));
        Assert.That(grid.RemainingCount, Is.EqualTo(quadTree.RemainingCount));
        Assert.That(grid.QueryChecksumBeforeUpdates, Is.EqualTo(quadTree.QueryChecksumBeforeUpdates));
        Assert.That(grid.QueryChecksumAfterUpdates, Is.EqualTo(quadTree.QueryChecksumAfterUpdates));
    }

    private static string BuildComparisonTable(
        string title,
        int itemCount,
        int queryCount,
        int updateCount,
        int removeCount,
        ScenarioResult quadTree,
        ScenarioResult grid) {

        long baseline = Math.Max(quadTree.Bench.OpsPerSec, grid.Bench.OpsPerSec);
        var builder = new StringBuilder();

        builder.AppendLine()
            .AppendLine($"╔═══ {title} ═══╗")
            .AppendLine($"  Items: {itemCount:N0} | Queries: {queryCount:N0} | Updates: {updateCount:N0} | Removes: {removeCount:N0}")
            .AppendLine($"  {"Index",-20} | {"ms",8} | {"ops/s",14} | {"alloc KB",10} | {"GC g0",6} | {"overhead",9} | {"checksum",10}")
            .AppendLine(new string('─', 98))
            .AppendLine(FormatRow(quadTree, baseline))
            .AppendLine(FormatRow(grid, baseline))
            .AppendLine($"  Remaining items: {quadTree.RemainingCount:N0}")
            .AppendLine($"  Query checksum before updates: {quadTree.QueryChecksumBeforeUpdates}")
            .AppendLine($"  Query checksum after updates : {quadTree.QueryChecksumAfterUpdates}");

        TestContext.Out.WriteLine(builder.ToString());
        return builder.ToString();
    }

    private static string FormatRow(ScenarioResult result, long baselineOps) {
        double overhead = baselineOps > 0 ? (double)baselineOps / result.Bench.OpsPerSec : 1.0;
        return $"  {result.Name,-20} | {result.Bench.ElapsedMs,8} | {result.Bench.OpsPerSec,14:N0} | " +
               $"{result.Bench.AllocatedBytes / 1024.0,10:F1} | {result.Bench.GCGen0,6} | {overhead,8:F2}x | {result.Bench.ResultChecksum,10}";
    }

    private static IndexedItem[] CreateUniformItems(int count) {
        var items = new IndexedItem[count];
        var random = new Random(11);
        float half = WorldSize / 2f;

        for (int i = 0; i < count; i++) {
            float x = (float)(random.NextDouble() * (WorldSize - 64) - (half - 32));
            float y = (float)(random.NextDouble() * (WorldSize - 64) - (half - 32));
            float width = 6 + random.Next(0, 18);
            float height = 6 + random.Next(0, 18);
            items[i] = new IndexedItem(i, new Bounds(x, y, width, height));
        }

        return items;
    }

    private static IndexedItem[] CreateClusteredItems(int count) {
        var items = new IndexedItem[count];
        var random = new Random(22);
        var clusterCenters = new[] {
            new Bounds(-1200, -900, 0, 0),
            new Bounds(900, -700, 0, 0),
            new Bounds(-600, 1000, 0, 0),
            new Bounds(1100, 850, 0, 0)
        };

        for (int i = 0; i < count; i++) {
            Bounds cluster = clusterCenters[i % clusterCenters.Length];
            float x = cluster.X + (float)(random.NextDouble() * 220 - 110);
            float y = cluster.Y + (float)(random.NextDouble() * 220 - 110);
            float size = 8 + random.Next(0, 20);
            items[i] = new IndexedItem(i, new Bounds(x, y, size, size));
        }

        return items;
    }

    private static Bounds[] CreateQueryAreas(int count, float areaWidth, float areaHeight, int seed) {
        var queries = new Bounds[count];
        var random = new Random(seed);
        float half = WorldSize / 2f;

        for (int i = 0; i < count; i++) {
            float x = (float)(random.NextDouble() * (WorldSize - areaWidth) - (half - areaWidth / 2f));
            float y = (float)(random.NextDouble() * (WorldSize - areaHeight) - (half - areaHeight / 2f));
            queries[i] = new Bounds(x, y, areaWidth, areaHeight);
        }

        return queries;
    }

    private static Bounds[] CreateClusteredQueryAreas(int count, float areaWidth, float areaHeight) {
        var queries = new Bounds[count];
        var random = new Random(33);
        var centers = new[] {
            (-1150f, -850f),
            (950f, -650f),
            (-650f, 950f),
            (1050f, 900f)
        };

        for (int i = 0; i < count; i++) {
            var center = centers[i % centers.Length];
            float x = center.Item1 + (float)(random.NextDouble() * 180 - 90);
            float y = center.Item2 + (float)(random.NextDouble() * 180 - 90);
            queries[i] = new Bounds(x, y, areaWidth, areaHeight);
        }

        return queries;
    }

    private static Bounds[] CreateUpdatedBounds(IndexedItem[] items, float maxDelta, int seed) {
        var updated = new Bounds[UpdateCount];
        var random = new Random(seed);
        for (int i = 0; i < UpdateCount; i++) {
            updated[i] = NudgeBounds(items[i].Bounds, random, maxDelta);
        }

        return updated;
    }

    private static Bounds NudgeBounds(Bounds source, Random random, float maxDelta) {
        float dx = (float)(random.NextDouble() * maxDelta * 2 - maxDelta);
        float dy = (float)(random.NextDouble() * maxDelta * 2 - maxDelta);

        float half = WorldSize / 2f;
        float minX = -half + source.Width / 2f;
        float maxX = half - source.Width / 2f;
        float minY = -half + source.Height / 2f;
        float maxY = half - source.Height / 2f;

        float x = Math.Clamp(source.X + dx, minX, maxX);
        float y = Math.Clamp(source.Y + dy, minY, maxY);
        return new Bounds(x, y, source.Width, source.Height);
    }

    private readonly record struct ScenarioResult(
        string Name,
        BenchResult Bench,
        int RemainingCount,
        int UpdatedCount,
        int RemovedCount,
        int QueryChecksumBeforeUpdates,
        int QueryChecksumAfterUpdates);
}
