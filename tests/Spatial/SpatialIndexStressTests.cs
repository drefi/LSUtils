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
    public void DenseDistribution_StressComparison() {
        var items = CreateDenseItems(1000);  // 1000+ items in same/nearby positions
        var queries = CreateQueryAreas(QueryCount / 2, areaWidth: 32, areaHeight: 32, seed: 404);
        var updatedBounds = CreateUpdatedBounds(items, maxDelta: 2, seed: 505);

        int denseUpdateCount = Math.Min(500, items.Length);  // Limit updates to items we have
        int denseRemoveCount = Math.Min(250, items.Length / 4);
        
        var quadTree = RunScenarioWithCustomCounts("QuadTree", QuadTreeFactory, items, queries, updatedBounds, denseUpdateCount, denseRemoveCount);
        var grid = RunScenarioWithCustomCounts("SpatialHashGrid", SpatialHashGridFactory, items, queries, updatedBounds, denseUpdateCount, denseRemoveCount);

        AssertScenarioConsistencyCustom(quadTree, grid, denseUpdateCount, denseRemoveCount);
        Assert.Pass(BuildComparisonTable(
            "Dense spatial workload (1000+ items in same area)",
            items.Length,
            queries.Length / 2,
            denseUpdateCount,
            denseRemoveCount,
            quadTree,
            grid));
    }

    [Test]
    public void CollisionStress_UniformDistribution() {
        var items = CreateUniformParticleItems(1_000);
        var quadTree = QuadTreeFactory();
        var grid = SpatialHashGridFactory();

        SeedIndex(quadTree, items);
        SeedIndex(grid, items);

        AssertCollisionQueryConsistency("uniform", quadTree, grid, items);
        var quadMetrics = MeasureCollisionQueryScenario("QuadTree", quadTree, items);
        var gridMetrics = MeasureCollisionQueryScenario("SpatialHashGrid", grid, items);

        Assert.Pass(BuildCollisionQueryComparisonTable("Uniform collision stress workload", quadMetrics, gridMetrics));
    }

    [Test]
    public void CollisionStress_ClusteredDistribution() {
        var items = CreateClusteredParticleItems(1_000);
        var quadTree = QuadTreeFactory();
        var grid = SpatialHashGridFactory();

        SeedIndex(quadTree, items);
        SeedIndex(grid, items);

        AssertCollisionQueryConsistency("clustered", quadTree, grid, items);
        var quadMetrics = MeasureCollisionQueryScenario("QuadTree", quadTree, items);
        var gridMetrics = MeasureCollisionQueryScenario("SpatialHashGrid", grid, items);

        Assert.Pass(BuildCollisionQueryComparisonTable("Clustered collision stress workload", quadMetrics, gridMetrics));
    }

    [Test]
    public void CollisionStress_DenseDistribution() {
        var items = CreateDenseParticleItems(1_000);
        var quadTree = QuadTreeFactory();
        var grid = SpatialHashGridFactory();

        SeedIndex(quadTree, items);
        SeedIndex(grid, items);

        AssertCollisionQueryConsistency("dense", quadTree, grid, items);
        var quadMetrics = MeasureCollisionQueryScenario("QuadTree", quadTree, items);
        var gridMetrics = MeasureCollisionQueryScenario("SpatialHashGrid", grid, items);

        Assert.Pass(BuildCollisionQueryComparisonTable("Dense collision stress workload", quadMetrics, gridMetrics));
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

            // Remove old position and insert at new position (no in-place updates)
            bool quadRemoved = quadTree.Remove(id);
            bool gridRemoved = grid.Remove(id);
            Assert.That(quadRemoved, Is.True, $"Failed to remove quad item {id}");
            Assert.That(gridRemoved, Is.True, $"Failed to remove grid item {id}");

            bool quadInserted = quadTree.Insert(id, newBounds);
            bool gridInserted = grid.Insert(id, newBounds);
            Assert.That(quadInserted, Is.True, $"Failed to insert quad item {id}");
            Assert.That(gridInserted, Is.True, $"Failed to insert grid item {id}");
            currentBounds[id] = newBounds;

            Bounds probe = new Bounds(newBounds.X, newBounds.Y, 80, 80);
            var hitsQuad = new HashSet<int>();
            var hitsGrid = new HashSet<int>();
            quadTree.Query(probe, hitsQuad);
            var quadResults = hitsQuad.OrderBy(value => value).ToArray();
            grid.Query(probe, hitsGrid);
            var gridResults = hitsGrid.OrderBy(value => value).ToArray();

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

    private static ScenarioResult RunScenarioWithCustomCounts(
        string name,
        Func<ISpatialIndex<int>> factory,
        IndexedItem[] items,
        Bounds[] queries,
        Bounds[] updatedBounds,
        int updateCount,
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
        int updatedCount = ExecuteUpdatesWithCount(index, items, updatedBounds, updateCount);
        int queryChecksumAfter = ExecuteQueries(index, queries);
        int removedCount = ExecuteRemovals(index, items, removeCount);

        stopwatch.Stop();

        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - startAlloc;
        int gcGen0 = GC.CollectionCount(0) - startGC;
        int totalOperations = items.Length + queries.Length + updateCount + queries.Length + removeCount;
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

    private static void AssertCollisionQueryConsistency(
        string scenarioName,
        ISpatialIndex<int> quadTree,
        ISpatialIndex<int> grid,
        IndexedItem[] items) {
        var quadPairs = CollectCollisionPairs(quadTree, items);
        var gridPairs = CollectCollisionPairs(grid, items);

        Assert.That(gridPairs, Is.EquivalentTo(quadPairs), $"{scenarioName} collision mismatch");

        if (scenarioName == "dense") {
            Assert.That(quadPairs.Count, Is.GreaterThan(0), $"{scenarioName} should discover collisions");
        }
    }

    private static CollisionQueryMetrics MeasureCollisionQueryScenario(
        string name,
        ISpatialIndex<int> index,
        IndexedItem[] items) {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        long startAlloc = GC.GetTotalAllocatedBytes(precise: false);
        int startGC = GC.CollectionCount(0);
        var stopwatch = Stopwatch.StartNew();

        var pairs = CollectCollisionPairs(index, items);

        stopwatch.Stop();

        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - startAlloc;
        int gcGen0 = GC.CollectionCount(0) - startGC;
        long opsPerSec = stopwatch.ElapsedMilliseconds > 0
            ? (long)(items.Length / stopwatch.Elapsed.TotalSeconds)
            : long.MaxValue;

        return new CollisionQueryMetrics(
            name,
            new BenchResult(
                stopwatch.ElapsedMilliseconds,
                opsPerSec,
                allocatedBytes,
                gcGen0,
                pairs.Count),
            items.Length,
            pairs.Count);
    }

    private static HashSet<(int First, int Second)> CollectCollisionPairs(ISpatialIndex<int> index, IndexedItem[] items) {
        var discovered = new HashSet<(int First, int Second)>();

        foreach (var item in items) {
            var queryArea = item.Bounds;
            var hits = new HashSet<int>();
            index.Query(queryArea, hits);

            foreach (var hit in hits) {
                if (hit == item.Id) continue;

                var pair = hit < item.Id ? (hit, item.Id) : (item.Id, hit);
                discovered.Add(pair);
            }
        }

        return discovered;
    }

    private static int ExecuteQueries(ISpatialIndex<int> index, Bounds[] queries) {
        int checksum = 0;
        HashSet<int> hits = new();
        foreach (var query in queries) {
            hits.Clear();
            index.Query(query, hits);
            var results = hits.OrderBy(item => item);
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
            // Remove old position and insert at new position (no in-place updates)
            bool removed = index.Remove(items[i].Id);
            Assert.That(removed, Is.True, $"Failed to remove item {items[i].Id}");
            
            bool ok = index.Insert(items[i].Id, updatedBounds[i]);
            Assert.That(ok, Is.True, $"Failed to update item {items[i].Id}");
            updated++;
        }

        return updated;
    }

    private static int ExecuteUpdatesWithCount(ISpatialIndex<int> index, IndexedItem[] items, Bounds[] updatedBounds, int updateCount) {
        int updated = 0;
        int actualUpdateCount = Math.Min(updateCount, Math.Min(items.Length, updatedBounds.Length));
        for (int i = 0; i < actualUpdateCount; i++) {
            // Remove old position and insert at new position (no in-place updates)
            bool removed = index.Remove(items[i].Id);
            Assert.That(removed, Is.True, $"Failed to remove item {items[i].Id}");
            
            bool ok = index.Insert(items[i].Id, updatedBounds[i]);
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

    private static void AssertScenarioConsistencyCustom(ScenarioResult quadTree, ScenarioResult grid, int updateCount, int removeCount) {
        Assert.That(quadTree.UpdatedCount, Is.EqualTo(updateCount));
        Assert.That(grid.UpdatedCount, Is.EqualTo(updateCount));
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

    private static string BuildCollisionQueryComparisonTable(
        string title,
        CollisionQueryMetrics quadTree,
        CollisionQueryMetrics grid) {
        long baseline = Math.Max(quadTree.Bench.OpsPerSec, grid.Bench.OpsPerSec);
        var builder = new StringBuilder();

        builder.AppendLine()
            .AppendLine($"╔═══ {title} ═══╗")
            .AppendLine($"  Queries: {quadTree.QueryCount:N0} | Collision pairs: {quadTree.CollisionPairs:N0} / {grid.CollisionPairs:N0}")
            .AppendLine($"  {"Index",-20} | {"ms",8} | {"ops/s",14} | {"alloc KB",10} | {"GC g0",6} | {"overhead",9} | {"pairs",10} | {"checksum",10}")
            .AppendLine(new string('─', 120))
            .AppendLine(FormatCollisionQueryRow(quadTree, baseline))
            .AppendLine(FormatCollisionQueryRow(grid, baseline));

        TestContext.Out.WriteLine(builder.ToString());
        return builder.ToString();
    }

    private static string FormatCollisionQueryRow(CollisionQueryMetrics result, long baselineOps) {
        double overhead = baselineOps > 0 ? (double)baselineOps / result.Bench.OpsPerSec : 1.0;
        return $"  {result.Name,-20} | {result.Bench.ElapsedMs,8} | {result.Bench.OpsPerSec,14:N0} | " +
               $"{result.Bench.AllocatedBytes / 1024.0,10:F1} | {result.Bench.GCGen0,6} | {overhead,8:F2}x | {result.CollisionPairs,10:N0} | {result.Bench.ResultChecksum,10}";
    }

    private readonly record struct CollisionQueryMetrics(
        string Name,
        BenchResult Bench,
        int QueryCount,
        int CollisionPairs);

    private static IndexedItem[] CreateUniformItems(int count) {
        var items = new IndexedItem[count];
        const int columns = 100;
        const int rows = 100;
        float spacingX = WorldSize / columns;
        float spacingY = WorldSize / rows;
        float originX = -WorldSize / 2f + spacingX / 2f;
        float originY = -WorldSize / 2f + spacingY / 2f;

        for (int i = 0; i < count; i++) {
            int column = i % columns;
            int row = i / columns;
            float x = originX + column * spacingX;
            float y = originY + row * spacingY;
            items[i] = new IndexedItem(i, new Bounds(x, y, 0f, 0f));
        }

        return items;
    }

    private static IndexedItem[] CreateClusteredItems(int count) {
        var items = new IndexedItem[count];
        const int clusterColumns = 50;
        const int clusterRows = 50;
        const float spacing = 10f;
        float clusterWidth = (clusterColumns - 1) * spacing;
        float clusterHeight = (clusterRows - 1) * spacing;
        var clusterCenters = new[] {
            (-700f, -700f),
            (700f, -700f),
            (-700f, 700f),
            (700f, 700f)
        };

        for (int i = 0; i < count; i++) {
            int clusterIndex = i / (clusterColumns * clusterRows);
            int clusterItemIndex = i % (clusterColumns * clusterRows);
            int column = clusterItemIndex % clusterColumns;
            int row = clusterItemIndex / clusterColumns;

            var cluster = clusterCenters[clusterIndex % clusterCenters.Length];
            float x = cluster.Item1 - clusterWidth / 2f + column * spacing;
            float y = cluster.Item2 - clusterHeight / 2f + row * spacing;
            items[i] = new IndexedItem(i, new Bounds(x, y, 0f, 0f));
        }

        return items;
    }

    private static IndexedItem[] CreateDenseItems(int count) {
        // Create 500+ items in the same area or very close together
        // This tests pathological cases where many objects occupy the same space
        var items = new IndexedItem[count];
        const float denseAreaRadius = 50f;
        float centerX = 100f;
        float centerY = 100f;
        var random = new Random(606);

        for (int i = 0; i < count; i++) {
            // Place items in a small dense area
            float angle = (float)(random.NextDouble() * 2 * Math.PI);
            float radius = (float)(random.NextDouble() * denseAreaRadius);
            float x = centerX + radius * (float)Math.Cos(angle);
            float y = centerY + radius * (float)Math.Sin(angle);
            items[i] = new IndexedItem(i, new Bounds(x, y, 0f, 0f));
        }

        return items;
    }

    private static IndexedItem[] CreateUniformParticleItems(int count) {
        var items = new IndexedItem[count];
        const int columns = 100;
        const int rows = 100;
        float spacingX = WorldSize / columns;
        float spacingY = WorldSize / rows;
        float originX = -WorldSize / 2f + spacingX / 2f;
        float originY = -WorldSize / 2f + spacingY / 2f;

        for (int i = 0; i < count; i++) {
            int column = i % columns;
            int row = i / columns;
            float x = originX + column * spacingX;
            float y = originY + row * spacingY;
            items[i] = new IndexedItem(i, new Bounds(x, y, 5f, 5f));
        }

        return items;
    }

    private static IndexedItem[] CreateClusteredParticleItems(int count) {
        var items = new IndexedItem[count];
        const int clusterColumns = 50;
        const int clusterRows = 50;
        const float spacing = 6f;
        float clusterWidth = (clusterColumns - 1) * spacing;
        float clusterHeight = (clusterRows - 1) * spacing;
        var clusterCenters = new[] {
            (-700f, -700f),
            (700f, -700f),
            (-700f, 700f),
            (700f, 700f)
        };

        for (int i = 0; i < count; i++) {
            int clusterIndex = i / (clusterColumns * clusterRows);
            int clusterItemIndex = i % (clusterColumns * clusterRows);
            int column = clusterItemIndex % clusterColumns;
            int row = clusterItemIndex / clusterColumns;

            var cluster = clusterCenters[clusterIndex % clusterCenters.Length];
            float x = cluster.Item1 - clusterWidth / 2f + column * spacing;
            float y = cluster.Item2 - clusterHeight / 2f + row * spacing;
            items[i] = new IndexedItem(i, new Bounds(x, y, 5f, 5f));
        }

        return items;
    }

    private static IndexedItem[] CreateDenseParticleItems(int count) {
        var items = new IndexedItem[count];
        const float denseAreaRadius = 50f;
        float centerX = 100f;
        float centerY = 100f;
        var random = new Random(606);

        for (int i = 0; i < count; i++) {
            float angle = (float)(random.NextDouble() * 2 * Math.PI);
            float radius = (float)(random.NextDouble() * denseAreaRadius);
            float x = centerX + radius * (float)Math.Cos(angle);
            float y = centerY + radius * (float)Math.Sin(angle);
            items[i] = new IndexedItem(i, new Bounds(x, y, 5f, 5f));
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
        var updated = new Bounds[Math.Min(UpdateCount, items.Length)];
        var random = new Random(seed);
        for (int i = 0; i < updated.Length; i++) {
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
